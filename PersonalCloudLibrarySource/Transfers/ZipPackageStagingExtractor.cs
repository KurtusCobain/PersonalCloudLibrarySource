using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    // Opt-in ZIP-only extraction engine. NOT connected to Playnite's install
    // controller or permitted to launch installers. 7z/RAR are unsupported.
    // The caller must get the user's package/extraction choice first.
    public sealed class ZipPackageStagingExtractor
    {
        private const int CopyBufferBytes = 64 * 1024;

        public CloudTransferExecutionResult ExtractApprovedZip(
            string archivePath, string cacheRoot, string destinationDirectory,
            Guid jobId, CancellationToken cancellationToken,
            Action<long, long?> progress = null)
        {
            var createdFiles = new List<string>();
            var createdDirectories = new List<string>();
            string staging = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (jobId == Guid.Empty)
                    return CloudTransferExecutionResult.Failure("An owned extraction job ID is required.");
                if (!string.Equals(Path.GetExtension(archivePath), ".zip",
                    StringComparison.OrdinalIgnoreCase))
                    return CloudTransferExecutionResult.Failure(
                        "Only ZIP packages are supported by this extractor.");
                if (string.IsNullOrWhiteSpace(cacheRoot) || !Directory.Exists(cacheRoot))
                    return CloudTransferExecutionResult.Failure(
                        "The configured cache root must already exist.");
                if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath) ||
                    (File.GetAttributes(archivePath) & FileAttributes.ReparsePoint) != 0)
                    return CloudTransferExecutionResult.Failure(
                        "Archive source is missing or is a filesystem link.");

                var policy = new SafeCacheDeletionPolicy();
                var destinationCheck = policy.Authorize(cacheRoot, destinationDirectory, false);
                if (!destinationCheck.Allowed)
                    return CloudTransferExecutionResult.Failure(
                        "Unsafe cache destination: " + destinationCheck.Reason);
                var destination = destinationCheck.TargetPath;
                var parent = Path.GetDirectoryName(destination);
                if (!Directory.Exists(parent))
                    return CloudTransferExecutionResult.Failure(
                        "The destination parent must already exist inside the cache.");
                if (File.Exists(destination) || Directory.Exists(destination))
                    return CloudTransferExecutionResult.Failure(
                        "Destination already exists; existing game files will not be overwritten.");

                staging = TransferPartialPathPolicy.Create(destination, jobId);
                var stagingCheck = policy.Authorize(cacheRoot, staging, false);
                if (!stagingCheck.Allowed ||
                    File.Exists(staging) || Directory.Exists(staging))
                    return CloudTransferExecutionResult.Failure(
                        "Private extraction staging path is unsafe or already exists.");

                using (var source = new FileStream(archivePath, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                {
                    // .NET Framework 4.6.2 ZipArchiveEntry does not expose
                    // external attributes. Read and validate ZIP32 metadata
                    // separately so Unix links cannot be disguised as files.
                    var central = ZipCentralDirectoryInspector.Read(source);
                    source.Position = 0;
                    using (var archive = new ZipArchive(source, ZipArchiveMode.Read, true))
                    {
                    if (archive.Entries.Count != central.Count)
                        throw new InvalidDataException("ZIP entry count differs from central metadata.");
                    var records = new List<ArchiveEntryObservation>();
                    for (var index = 0; index < archive.Entries.Count; index++)
                    {
                        var entry = archive.Entries[index];
                        var header = central[index];
                        if (!string.Equals(entry.FullName, header.Name, StringComparison.Ordinal))
                            throw new InvalidDataException("ZIP entry name differs from central metadata.");
                        var isDirectory = entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                            entry.FullName.EndsWith("\\", StringComparison.Ordinal);
                        records.Add(new ArchiveEntryObservation
                        {
                            EntryPath = entry.FullName,
                            IsDirectory = isDirectory,
                            IsLink = header.IsUnsafeLinkOrSpecialFile,
                            CompressedBytes = entry.CompressedLength,
                            UncompressedBytes = entry.Length
                        });
                    }
                    var inspected = new ArchiveInspectionPolicy().Inspect(records);
                    if (!inspected.AllowedForStagedExtraction)
                        return CloudTransferExecutionResult.Failure(
                            "Archive preflight rejected ZIP: " + inspected.Reason);

                    // No filesystem mutation has occurred until ALL ZIP metadata
                    // passes bounded, Windows-aware path validation.
                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(staging);
                    createdDirectories.Add(staging);
                    long copiedTotal = 0;
                    var buffer = new byte[CopyBufferBytes];

                    for (var index = 0; index < archive.Entries.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var entry = archive.Entries[index];
                        var observation = records[index];
                        var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar)
                            .Replace('\\', Path.DirectorySeparatorChar)
                            .TrimEnd(Path.DirectorySeparatorChar);
                        var target = Path.GetFullPath(Path.Combine(staging, relative));
                        if (!PathBoundary.IsContained(staging, target))
                            throw new IOException("Archive entry escaped its private staging root.");

                        if (observation.IsDirectory)
                        {
                            EnsurePrivateDirectory(staging, target, createdDirectories);
                            continue;
                        }

                        EnsurePrivateDirectory(staging, Path.GetDirectoryName(target), createdDirectories);
                        if (File.Exists(target) || Directory.Exists(target))
                            throw new IOException("Archive entry collided with another staged path.");
                        using (var input = entry.Open())
                        using (var output = new FileStream(target, FileMode.CreateNew,
                            FileAccess.Write, FileShare.None))
                        {
                            createdFiles.Add(target);
                            long copiedEntry = 0;
                            int read;
                            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (copiedEntry > observation.UncompressedBytes - read ||
                                    copiedTotal > ArchiveInspectionPolicy.MaxTotalBytes - read)
                                    throw new InvalidDataException(
                                        "Archive output exceeds its declared or permitted size.");
                                output.Write(buffer, 0, read);
                                copiedEntry += read;
                                copiedTotal += read;
                                progress?.Invoke(copiedTotal, inspected.TotalUncompressedBytes);
                            }
                            if (copiedEntry != observation.UncompressedBytes)
                                throw new InvalidDataException(
                                    "Archive output length differs from the inspected metadata.");
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    if (copiedTotal != inspected.TotalUncompressedBytes)
                        throw new InvalidDataException("Extracted byte totals failed verification.");
                    if (File.Exists(destination) || Directory.Exists(destination) ||
                        !policy.Authorize(cacheRoot, destination, false).Allowed)
                        throw new IOException("Destination changed before publishing the staged game.");

                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.Move(staging, destination);
                    return CloudTransferExecutionResult.Success(copiedTotal,
                        inspected.TotalUncompressedBytes);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                var cleaned = CleanupCreatedStaging(cacheRoot, staging,
                    createdFiles, createdDirectories);
                return cleaned
                    ? CloudTransferExecutionResult.CancelledResult()
                    : CloudTransferExecutionResult.Failure(
                        "Extraction cancelled, but private staging needs manual inspection.");
            }
            catch (Exception ex)
            {
                var cleaned = CleanupCreatedStaging(cacheRoot, staging,
                    createdFiles, createdDirectories);
                return CloudTransferExecutionResult.Failure(
                    "ZIP extraction failed: " + ex.Message +
                    (cleaned ? "" : " Private staging needs manual inspection."), ex);
            }
        }

        private static void EnsurePrivateDirectory(string staging, string directory,
            List<string> createdDirectories)
        {
            var full = Path.GetFullPath(directory);
            if (!string.Equals(staging, full, StringComparison.OrdinalIgnoreCase) &&
                !PathBoundary.IsContained(staging, full))
                throw new IOException("Archive attempted to create a directory outside staging.");

            if (string.Equals(staging, full, StringComparison.OrdinalIgnoreCase))
                return;
            var relative = full.Substring(staging.Length)
                .TrimStart(Path.DirectorySeparatorChar);
            var current = staging;
            foreach (var component in relative.Split(Path.DirectorySeparatorChar))
            {
                current = Path.Combine(current, component);
                if (File.Exists(current))
                    throw new IOException("Archive directory conflicts with an existing file.");
                if (Directory.Exists(current))
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Archive staging contains an unexpected filesystem link.");
                }
                else
                {
                    Directory.CreateDirectory(current);
                    createdDirectories.Add(current);
                }
            }
        }

        // Only delete files actually created during THIS job and then remove
        // empty directories. Never recursively delete unknown/unowned content.
        private static bool CleanupCreatedStaging(string cacheRoot, string staging,
            List<string> createdFiles, List<string> createdDirectories)
        {
            if (string.IsNullOrWhiteSpace(staging) || createdDirectories.Count == 0)
                return true;
            var clean = true;
            var policy = new SafeCacheDeletionPolicy();
            foreach (var file in createdFiles.AsEnumerable().Reverse())
            {
                try
                {
                    if (!policy.Authorize(cacheRoot, file, false).Allowed)
                    {
                        clean = false;
                        continue;
                    }
                    if (File.Exists(file))
                    {
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                        {
                            clean = false;
                            continue;
                        }
                        File.Delete(file);
                    }
                }
                catch (IOException) { clean = false; }
                catch (UnauthorizedAccessException) { clean = false; }
            }
            foreach (var directory in createdDirectories.AsEnumerable().Reverse())
            {
                try
                {
                    if (!policy.Authorize(cacheRoot, directory, false).Allowed)
                    {
                        clean = false;
                        continue;
                    }
                    if (Directory.Exists(directory))
                        Directory.Delete(directory, false); // empty only
                }
                catch (IOException) { clean = false; }
                catch (UnauthorizedAccessException) { clean = false; }
            }
            return clean && !Directory.Exists(staging);
        }
    }
}
