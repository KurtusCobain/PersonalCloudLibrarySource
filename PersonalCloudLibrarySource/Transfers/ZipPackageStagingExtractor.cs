using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    // Opt-in ZIP-only extraction engine. NOT connected to Playnite's install
    // controller or permitted to launch installers. 7z/RAR are unsupported.
    // The caller must get the user's package/extraction choice first.
    public sealed class ZipPackageStagingExtractor
    {
        private const int CopyBufferBytes = 64 * 1024;
        private static readonly uint[] Crc32Table = BuildCrc32Table();

        // Owned file names are not enough to authorize rollback deletion:
        // another process may have changed a created file in the meantime.
        private sealed class CreatedFileRecord
        {
            public string Path;
            public long WrittenBytes;
            public byte[] WrittenSha256;
        }

        public CloudTransferExecutionResult ExtractApprovedZip(
            string archivePath, string cacheRoot, string destinationDirectory,
            Guid jobId, CancellationToken cancellationToken,
            Action<long, long?> progress = null)
        {
            var createdFiles = new List<CreatedFileRecord>();
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
                        // Defense in depth against ZIP Slip: never pass a raw ZIP
                        // entry name to Path.Combine. Revalidate its individual
                        // components here, at the filesystem write boundary,
                        // even though the archive-wide preflight already ran.
                        var rawName = entry.FullName;
                        if (string.IsNullOrWhiteSpace(rawName) ||
                            rawName.Length > ArchiveInspectionPolicy.MaxPathCharacters ||
                            Path.IsPathRooted(rawName) ||
                            rawName.StartsWith("/", StringComparison.Ordinal) ||
                            rawName.StartsWith("\\", StringComparison.Ordinal) ||
                            rawName.IndexOf(':') >= 0 ||
                            rawName.IndexOf('\0') >= 0 ||
                            rawName.Any(ch => ch < 32))
                            throw new InvalidDataException("Unsafe ZIP entry name.");

                        var parts = rawName.Replace('\\', '/')
                            .TrimEnd('/').Split('/');
                        if (parts.Length == 0 || parts.Any(part =>
                            part.Length == 0 || part == "." || part == ".." ||
                            part.EndsWith(".", StringComparison.Ordinal) ||
                            part.EndsWith(" ", StringComparison.Ordinal)))
                            throw new InvalidDataException(
                                "ZIP entry contains unsafe path components.");

                        var relative = Path.Combine(parts);
                        var stageRoot = Path.GetFullPath(staging)
                            .TrimEnd(Path.DirectorySeparatorChar,
                                Path.AltDirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
                        var target = Path.GetFullPath(Path.Combine(staging, relative));
                        // A canonical child path MUST be below the staging
                        // root plus separator (not equal to root or a prefix
                        // sibling). This check is local to the filesystem sink.
                        if (!target.StartsWith(stageRoot,
                                StringComparison.OrdinalIgnoreCase) ||
                            !PathBoundary.IsContained(staging, target))
                            throw new IOException(
                                "Archive entry escaped its private staging root.");

                        if (observation.IsDirectory)
                        {
                            if (central[index].ExpectedCrc32 != 0)
                                throw new InvalidDataException("Archive directory has a nonzero CRC.");
                            EnsurePrivateDirectory(staging, target, createdDirectories);
                            continue;
                        }

                        EnsurePrivateDirectory(staging, Path.GetDirectoryName(target), createdDirectories);
                        if (!policy.Authorize(cacheRoot, target, false).Allowed)
                            throw new IOException("ZIP output path or ancestor became unsafe.");
                        if (File.Exists(target) || Directory.Exists(target))
                            throw new IOException("Archive entry collided with another staged path.");
                        var created = new CreatedFileRecord { Path = target };
                        using (var input = entry.Open())
                        using (var output = new FileStream(target, FileMode.CreateNew,
                            FileAccess.Write, FileShare.None))
                        {
                            createdFiles.Add(created);
                            using (var sha256 = SHA256.Create())
                            {
                                long copiedEntry = 0;
                                uint crc = 0xFFFFFFFFu;
                                try
                                {
                                    int read;
                                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        cancellationToken.ThrowIfCancellationRequested();
                                        if (copiedEntry > observation.UncompressedBytes - read ||
                                            copiedTotal > ArchiveInspectionPolicy.MaxTotalBytes - read)
                                            throw new InvalidDataException(
                                                "Archive output exceeds its declared or permitted size.");
                                        output.Write(buffer, 0, read);
                                        sha256.TransformBlock(buffer, 0, read, buffer, 0);
                                        created.WrittenBytes += read;
                                        crc = UpdateCrc32(crc, buffer, read);
                                        copiedEntry += read;
                                        copiedTotal += read;
                                        progress?.Invoke(copiedTotal, inspected.TotalUncompressedBytes);
                                    }
                                    if (copiedEntry != observation.UncompressedBytes)
                                        throw new InvalidDataException(
                                            "Archive output length differs from the inspected metadata.");
                                    if (~crc != central[index].ExpectedCrc32)
                                        throw new InvalidDataException(
                                            "ZIP member CRC32 differs from central directory; refusing corrupted game.");
                                }
                                finally
                                {
                                    sha256.TransformFinalBlock(new byte[0], 0, 0);
                                    created.WrittenSha256 = sha256.Hash;
                                }
                            }
                        }
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    if (copiedTotal != inspected.TotalUncompressedBytes)
                        throw new InvalidDataException("Extracted byte totals failed verification.");
                    if (!VerifyExactStagingTree(staging, records, cancellationToken))
                        throw new IOException("Private ZIP staging contains unrecognized or linked content.");
                    if (File.Exists(destination) || Directory.Exists(destination) ||
                        !policy.Authorize(cacheRoot, destination, false).Allowed ||
                        !policy.Authorize(cacheRoot, staging, false).Allowed)
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

        // Verify that the completed private stage contains exactly the
        // preflighted member paths and their implicit parent directories.
        // This prevents unexpected files injected during extraction from
        // becoming part of the published game and its ownership inventory.
        private static bool VerifyExactStagingTree(string staging,
            IList<ArchiveEntryObservation> records, CancellationToken token)
        {
            if (!Directory.Exists(staging) ||
                (File.GetAttributes(staging) & FileAttributes.ReparsePoint) != 0)
                return false;

            var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var expectedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in records)
            {
                token.ThrowIfCancellationRequested();
                var relative = record.EntryPath.Replace('\\', '/').TrimEnd('/');
                var parts = relative.Split('/');
                for (var i = 1; i < parts.Length; i++)
                    expectedDirs.Add(string.Join("/", parts.Take(i)));
                if (record.IsDirectory)
                    expectedDirs.Add(relative);
                else
                    expectedFiles.Add(relative);
            }

            var actualFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var actualDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            pending.Push(staging);
            var count = 0;
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var parent = pending.Pop();
                if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                    return false;
                foreach (var entry in Directory.EnumerateFileSystemEntries(parent))
                {
                    token.ThrowIfCancellationRequested();
                    if (++count > ArchiveInspectionPolicy.MaxEntries * 2 ||
                        !PathBoundary.IsContained(staging, entry))
                        return false;
                    var attrs = File.GetAttributes(entry);
                    if ((attrs & FileAttributes.ReparsePoint) != 0)
                        return false;
                    var relative = entry.Substring(staging.Length)
                        .TrimStart(Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar)
                        .Replace('\\', '/');
                    if ((attrs & FileAttributes.Directory) != 0)
                    {
                        if (!expectedDirs.Contains(relative) ||
                            !actualDirs.Add(relative))
                            return false;
                        pending.Push(entry);
                    }
                    else if (!expectedFiles.Contains(relative) ||
                             !actualFiles.Add(relative))
                        return false;
                }
            }
            return expectedFiles.SetEquals(actualFiles) &&
                expectedDirs.SetEquals(actualDirs);
        }

        private static uint[] BuildCrc32Table()
        {
            var table = new uint[256];
            for (var index = 0; index < table.Length; index++)
            {
                var value = (uint)index;
                for (var bit = 0; bit < 8; bit++)
                    value = (value & 1) != 0
                        ? (value >> 1) ^ 0xEDB88320u : value >> 1;
                table[index] = value;
            }
            return table;
        }

        private static uint UpdateCrc32(uint previous, byte[] buffer, int count)
        {
            var crc = previous;
            for (var index = 0; index < count; index++)
                crc = (crc >> 8) ^ Crc32Table[(int)((crc ^ buffer[index]) & 0xFFu)];
            return crc;
        }

        private static void EnsurePrivateDirectory(string staging, string directory,
            List<string> createdDirectories)
        {
            var full = Path.GetFullPath(directory);
            if (!string.Equals(staging, full, StringComparison.OrdinalIgnoreCase) &&
                !PathBoundary.IsContained(staging, full))
                throw new IOException("Archive attempted to create a directory outside staging.");

            // Check the staging root again. A directory can be swapped for
            // a junction by another process after our initial CreateDirectory.
            if (!Directory.Exists(staging) ||
                (File.GetAttributes(staging) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Private staging root is missing or linked.");
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
            List<CreatedFileRecord> createdFiles, List<string> createdDirectories)
        {
            if (string.IsNullOrWhiteSpace(staging) || createdDirectories.Count == 0)
                return true;
            var clean = true;
            var policy = new SafeCacheDeletionPolicy();
            foreach (var record in createdFiles.AsEnumerable().Reverse())
            {
                try
                {
                    var file = record.Path;
                    if (!policy.Authorize(cacheRoot, file, false).Allowed ||
                        record.WrittenSha256 == null)
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
                        // Content was recorded as bytes were written; an
                        // unexpected change cancels automatic rollback. Hold
                        // a read handle that denies ordinary concurrent writes
                        // until the exact verified file is removed.
                        using (var input = new FileStream(file, FileMode.Open,
                            FileAccess.Read, FileShare.Read | FileShare.Delete))
                        using (var digest = SHA256.Create())
                        {
                            if (input.Length != record.WrittenBytes ||
                                !digest.ComputeHash(input).SequenceEqual(
                                    record.WrittenSha256))
                            {
                                clean = false;
                                continue;
                            }
                            File.Delete(file);
                        }
                    }
                }
                catch (IOException) { clean = false; }
                catch (UnauthorizedAccessException) { clean = false; }
                catch (CryptographicException) { clean = false; }
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
