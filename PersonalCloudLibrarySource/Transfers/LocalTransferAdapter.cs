using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public sealed class LocalTransferAdapter
    {
        private readonly int bufferSize;

        private sealed class WrittenFile
        {
            public TransferPartialPathPolicy.FileIdentity? Identity;
            public long Length;
            public byte[] Digest;
        }

        public LocalTransferAdapter(int bufferSize = 1024 * 1024)
        {
            this.bufferSize = Math.Max(4096, bufferSize);
        }

        public CloudTransferExecutionResult CopyFile(
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken,
            Action<long, long?> progress)
        {
            return CopyFileCore(
                sourcePath,
                destinationPath,
                string.IsNullOrWhiteSpace(destinationPath) ? string.Empty : destinationPath + ".pcls-partial",
                cancellationToken,
                progress,
                null);
        }

        public CloudTransferExecutionResult CopyFile(
            string sourcePath,
            string destinationPath,
            Guid jobId,
            CancellationToken cancellationToken,
            Action<long, long?> progress,
            Action<CloudTransferState> phase = null)
        {
            return CopyFileCore(
                sourcePath,
                destinationPath,
                TransferPartialPathPolicy.Create(destinationPath, jobId),
                cancellationToken,
                progress,
                phase);
        }

        private CloudTransferExecutionResult CopyFileCore(
            string sourcePath,
            string destinationPath,
            string partialPath,
            CancellationToken cancellationToken,
            Action<long, long?> progress,
            Action<CloudTransferState> phase)
        {
            var ownsPartial = false;
            var written = new WrittenFile();
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    return CloudTransferExecutionResult.Failure("Source file does not exist: " + sourcePath);
                }

                if (string.IsNullOrWhiteSpace(destinationPath))
                {
                    return CloudTransferExecutionResult.Failure("Destination file path is empty.");
                }

                if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
                {
                    return CloudTransferExecutionResult.Failure("Destination already exists: " + destinationPath);
                }

                TransferPartialPathPolicy.RequireSafePath(sourcePath);
                var totalBytes = new FileInfo(sourcePath).Length;
                var spaceError = CheckDiskSpace(destinationPath, totalBytes);
                if (!string.IsNullOrWhiteSpace(spaceError))
                    return CloudTransferExecutionResult.Failure(spaceError);

                cancellationToken.ThrowIfCancellationRequested();
                TransferPartialPathPolicy.RequireVacant(partialPath);
                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);
                var transferred = CopyFileContents(
                    sourcePath,
                    partialPath,
                    cancellationToken,
                    totalBytes,
                    0,
                    progress,
                    new byte[bufferSize],
                    () => ownsPartial = true,
                    written);

                phase?.Invoke(CloudTransferState.Verifying);
                long copiedBytes;
                if (!SafeFileTreeTraversal.TryMeasure(partialPath, false, cancellationToken, out copiedBytes) ||
                    copiedBytes != totalBytes)
                {
                    throw new IOException("The copied file did not pass size verification.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                phase?.Invoke(CloudTransferState.Finalizing);
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    TransferPartialPathPolicy.RequirePublicationPaths(partialPath, destinationPath, false);
                    using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(partialPath, true))
                    {
                        if (written.Length != totalBytes ||
                            !MatchesWrittenFile(input, written, cancellationToken))
                            throw new IOException("Private staging file identity or contents changed.");
                        cancellationToken.ThrowIfCancellationRequested();
                        TransferPartialPathPolicy.PublishVerifiedHandle(input.SafeFileHandle, partialPath, destinationPath);
                    }
                    TransferPartialPathPolicy.RequireVacant(partialPath);
                }
                catch (Exception)
                {
                    return CloudTransferExecutionResult.Failure(
                        "Transfer publication paths changed; staging was preserved for review.",
                        requiresManualRecovery: true);
                }
                return CloudTransferExecutionResult.Success(transferred, totalBytes);
            }
            catch (OperationCanceledException)
            {
                return CleanupOwnedFile(ownsPartial, partialPath, written,
                    CloudTransferExecutionResult.CancelledResult());
            }
            catch (Exception ex)
            {
                return CleanupOwnedFile(ownsPartial, partialPath, written,
                    CloudTransferExecutionResult.Failure(ex.Message, ex));
            }
        }

        public CloudTransferExecutionResult CopyDirectory(
            string sourceDirectory,
            string destinationDirectory,
            CancellationToken cancellationToken,
            Action<long, long?> progress)
        {
            var normalized = NormalizeDirectoryDestination(destinationDirectory);
            return CopyDirectoryCore(
                sourceDirectory,
                normalized,
                string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized + ".pcls-partial",
                cancellationToken,
                progress,
                null);
        }

        public CloudTransferExecutionResult CopyDirectory(
            string sourceDirectory,
            string destinationDirectory,
            Guid jobId,
            CancellationToken cancellationToken,
            Action<long, long?> progress,
            Action<CloudTransferState> phase = null)
        {
            var normalized = NormalizeDirectoryDestination(destinationDirectory);
            return CopyDirectoryCore(
                sourceDirectory,
                normalized,
                TransferPartialPathPolicy.Create(normalized, jobId),
                cancellationToken,
                progress,
                phase);
        }

        private CloudTransferExecutionResult CopyDirectoryCore(
            string sourceDirectory,
            string normalizedDestination,
            string partialDirectory,
            CancellationToken cancellationToken,
            Action<long, long?> progress,
            Action<CloudTransferState> phase)
        {
            var ownedDirectories = new List<string>();
            var directoryIdentities = new Dictionary<string, TransferPartialPathPolicy.FileIdentity>(StringComparer.OrdinalIgnoreCase);
            var ownedFiles = new Dictionary<string, WrittenFile>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
                {
                    return CloudTransferExecutionResult.Failure("Source directory does not exist: " + sourceDirectory);
                }

                if (string.IsNullOrWhiteSpace(normalizedDestination))
                {
                    return CloudTransferExecutionResult.Failure("Destination directory path is empty.");
                }

                if (Directory.Exists(normalizedDestination) || File.Exists(normalizedDestination))
                {
                    return CloudTransferExecutionResult.Failure("Destination already exists: " + normalizedDestination);
                }

                // Never copy a directory into itself (or into one of its children).
                // Otherwise enumeration can absorb a previous cache and copying
                // can recursively consume the source library or disk space.
                if (PathBoundary.IsContained(sourceDirectory, normalizedDestination) ||
                    PathBoundary.IsContained(normalizedDestination, sourceDirectory))
                {
                    return CloudTransferExecutionResult.Failure(
                        "Source directory overlaps the cache destination; choose a separate cache location.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                TransferPartialPathPolicy.RequireVacant(partialDirectory);
                var destinationParent = Path.GetDirectoryName(normalizedDestination);
                if (!string.IsNullOrWhiteSpace(destinationParent))
                {
                    Directory.CreateDirectory(destinationParent);
                }

                TransferPartialPathPolicy.RequireSafePath(sourceDirectory);
                var sourceRoot = Path.GetFullPath(sourceDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                // Never recursively follow Windows junctions/symlinks into a source
                // outside the selected library. Enumerate top-down and reject links.
                List<string> directories;
                List<string> files;
                CollectSafeSourceTree(sourceRoot, cancellationToken, out directories, out files);
                var totalBytes = files.Sum(file => new FileInfo(file).Length);
                var spaceError = CheckDiskSpace(normalizedDestination, totalBytes);
                if (!string.IsNullOrWhiteSpace(spaceError))
                    return CloudTransferExecutionResult.Failure(spaceError);
                var transferred = 0L;

                try
                {
                    directoryIdentities.Add(partialDirectory, TransferPartialPathPolicy.CreateOwnedDirectory(partialDirectory));
                    ownedDirectories.Add(partialDirectory);
                }
                catch (Exception)
                {
                    return CloudTransferExecutionResult.Failure(
                        "Private staging root ownership is uncertain; preserve staging for review.",
                        requiresManualRecovery: true);
                }
                foreach (var directory in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var target = Path.Combine(partialDirectory, GetRelativePath(sourceRoot, directory));
                        TransferPartialPathPolicy.RequireDirectoryIdentity(Path.GetDirectoryName(target),
                            directoryIdentities[Path.GetDirectoryName(target)]);
                        directoryIdentities.Add(target, TransferPartialPathPolicy.CreateOwnedDirectory(target));
                        ownedDirectories.Add(target);
                    }
                    catch (Exception)
                    {
                        // A failed exclusive create may expose an unrelated
                        // directory in our tree. Never recursively roll it back.
                        return CloudTransferExecutionResult.Failure(
                            "Private staging directory ownership is uncertain; preserve staging for review.",
                            requiresManualRecovery: true);
                    }
                }

                var copyBuffer = new byte[bufferSize];
                foreach (var sourceFile in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relativePath = GetRelativePath(sourceRoot, sourceFile);
                    var destinationFile = Path.Combine(partialDirectory, relativePath);
                    var targetDirectory = Path.GetDirectoryName(destinationFile);
                    var fileCreated = false;
                    var written = new WrittenFile();
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(targetDirectory))
                        {
                            TransferPartialPathPolicy.RequireDirectoryIdentity(targetDirectory,
                                directoryIdentities[targetDirectory]);
                        }

                        transferred = CopyFileContents(
                            sourceFile,
                            destinationFile,
                            cancellationToken,
                            totalBytes,
                            transferred,
                            progress,
                            copyBuffer,
                            () =>
                            {
                                fileCreated = true;
                                ownedFiles.Add(destinationFile, written);
                            },
                            written);
                    }
                    catch (Exception) when (!fileCreated)
                    {
                        // Failure before successful CreateNew does not give us
                        // ownership of an entry that may have appeared.
                        return CloudTransferExecutionResult.Failure(
                            "Private staging file ownership is uncertain; preserve staging for review.",
                            requiresManualRecovery: true);
                    }
                }

                phase?.Invoke(CloudTransferState.Verifying);
                VerifyOwnedDirectory(partialDirectory, directoryIdentities, ownedFiles, totalBytes, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();
                phase?.Invoke(CloudTransferState.Finalizing);
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    TransferPartialPathPolicy.RequirePublicationPaths(partialDirectory, normalizedDestination, true);
                    VerifyOwnedDirectory(partialDirectory, directoryIdentities, ownedFiles, totalBytes, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    TransferPartialPathPolicy.PublishOwnedDirectory(partialDirectory, normalizedDestination,
                        directoryIdentities[partialDirectory]);
                }
                catch (Exception)
                {
                    return CloudTransferExecutionResult.Failure(
                        "Transfer publication paths changed; staging was preserved for review.",
                        requiresManualRecovery: true);
                }
                return CloudTransferExecutionResult.Success(transferred, totalBytes);
            }
            catch (OperationCanceledException)
            {
                return CleanupOwnedDirectory(ownedDirectories, directoryIdentities, ownedFiles,
                    CloudTransferExecutionResult.CancelledResult());
            }
            catch (Exception ex)
            {
                return CleanupOwnedDirectory(ownedDirectories, directoryIdentities, ownedFiles,
                    CloudTransferExecutionResult.Failure(ex.Message, ex));
            }
        }

        // Directory.GetFiles(..., AllDirectories) may traverse junctions and
        // symbolic links. A user-selected directory must not implicitly pull
        // arbitrary content from outside the source tree.
        private static void CollectSafeSourceTree(
            string root, CancellationToken token,
            out List<string> directories, out List<string> files)
        {
            var directoryList = new List<string>();
            var fileList = new List<string>();
            var refusal = SafeFileTreeTraversal.Walk(root, SafeFileTreeTraversal.DefaultMaxEntries,
                token, (path, attributes) =>
                {
                    if ((attributes & FileAttributes.Directory) != 0) directoryList.Add(path);
                    else fileList.Add(path);
                });
            if (!string.IsNullOrEmpty(refusal)) throw new IOException(refusal);
            directories = directoryList;
            files = fileList;
        }

        internal static bool IsSafeCopyEntry(FileAttributes attributes)
        {
            return (attributes & FileAttributes.ReparsePoint) == 0;
        }

        // Best-effort capacity preflight. Query the nearest existing destination
        // ancestor, not just the drive letter: UNC shares and mounted volumes
        // may have their own capacity and caller-specific quotas. An unknown
        // capacity is not a promise of free space; normal IO and rollback still
        // handle exhaustion and changes after this non-reserving observation.
        internal static string CheckDiskSpace(string destinationPath, long requiredBytes)
        {
            try
            {
                if (requiredBytes < 0) return "Invalid required cache size.";
                var directory = Path.GetDirectoryName(Path.GetFullPath(destinationPath));
                while (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    directory = Path.GetDirectoryName(directory);
                if (string.IsNullOrEmpty(directory))
                    return string.Empty;
                // The Win32 API requires a trailing separator on a UNC share.
                if (!directory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    directory += Path.DirectorySeparatorChar;
                ulong available;
                ulong total;
                ulong free;
                if (!GetDiskFreeSpaceEx(directory, out available, out total, out free))
                    return string.Empty;
                var boundedAvailable = available > (ulong)long.MaxValue
                    ? long.MaxValue : (long)available;
                if (!HasRequiredSpace(boundedAvailable, requiredBytes))
                    return "Not enough free space on the cache drive. Free space or select another cache folder.";
            }
            catch (ArgumentException) { }
            catch (IOException) { }
            catch (NotSupportedException) { }
            catch (UnauthorizedAccessException) { }
            return string.Empty;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceEx(string directoryName,
            out ulong freeBytesAvailable, out ulong totalBytes, out ulong totalFreeBytes);

        internal static bool HasRequiredSpace(long available, long required)
        {
            if (available < 0 || required < 0 || available < required)
                return false;
            var reserve = Math.Min(64L * 1024 * 1024, required / 20);
            return available - required >= reserve;
        }

        private static string NormalizeDirectoryDestination(string destinationDirectory)
        {
            return string.IsNullOrWhiteSpace(destinationDirectory)
                ? string.Empty
                : destinationDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private long CopyFileContents(
            string sourcePath,
            string destinationPath,
            CancellationToken cancellationToken,
            long totalBytes,
            long alreadyTransferred,
            Action<long, long?> progress, byte[] buffer, Action created,
            WrittenFile written)
        {
            var transferred = alreadyTransferred;
            TransferPartialPathPolicy.RequireSafePath(sourcePath);
            TransferPartialPathPolicy.RequireVacant(destinationPath);
            using (var source = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize,
                FileOptions.SequentialScan))
            using (var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                bufferSize,
                FileOptions.SequentialScan))
            {
                created?.Invoke();
                written.Identity = TransferPartialPathPolicy.CapturePrivateFileIdentity(destination.SafeFileHandle);
                using (var digest = SHA256.Create())
                {
                    try
                    {
                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var bytesRead = source.Read(buffer, 0, buffer.Length);
                            if (bytesRead <= 0) break;
                            destination.Write(buffer, 0, bytesRead);
                            digest.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                            written.Length = checked(written.Length + bytesRead);
                            transferred = checked(transferred + bytesRead);
                            progress?.Invoke(transferred, totalBytes);
                        }
                        destination.Flush(true);
                    }
                    finally
                    {
                        digest.TransformFinalBlock(new byte[0], 0, 0);
                        written.Digest = digest.Hash;
                    }
                }
            }

            return transferred;
        }

        private static string GetRelativePath(string rootPath, string fullPath)
        {
            var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                 Path.DirectorySeparatorChar;
            var normalizedFullPath = Path.GetFullPath(fullPath);
            if (!normalizedFullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Source item is outside the selected source directory.");
            }

            return normalizedFullPath.Substring(normalizedRoot.Length);
        }

        private static CloudTransferExecutionResult CleanupOwnedFile(bool owned, string path,
            WrittenFile written, CloudTransferExecutionResult result, byte[] buffer = null)
        {
            if (!owned) return result;
            try
            {
                using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(path, true))
                {
                    if (!MatchesWrittenFile(input, written, CancellationToken.None, buffer))
                        throw new IOException("Private staging identity or contents changed.");
                    TransferPartialPathPolicy.DeleteVerifiedFileHandle(input.SafeFileHandle);
                }
                TransferPartialPathPolicy.RequireVacant(path);
                return result;
            }
            catch (Exception)
            {
                return CloudTransferExecutionResult.Failure(
                    "Transfer cleanup could not be verified. Preserve staging for manual recovery.",
                    requiresManualRecovery: true);
            }
        }

        private static bool MatchesWrittenFile(FileStream input, WrittenFile written, CancellationToken token,
            byte[] buffer = null)
        {
            if (!written.Identity.HasValue || written.Digest == null ||
                !TransferPartialPathPolicy.MatchesPrivateFileIdentity(input.SafeFileHandle, written.Identity.Value) ||
                input.Length != written.Length)
                return false;

            using (var digest = SHA256.Create())
            {
                buffer = buffer ?? new byte[64 * 1024];
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    digest.TransformBlock(buffer, 0, count, buffer, 0);
                }
                token.ThrowIfCancellationRequested();
                digest.TransformFinalBlock(new byte[0], 0, 0);
                return digest.Hash.SequenceEqual(written.Digest);
            }
        }

        private static void VerifyOwnedDirectory(string root,
            IDictionary<string, TransferPartialPathPolicy.FileIdentity> directories,
            IDictionary<string, WrittenFile> files, long expectedBytes,
            CancellationToken token)
        {
            var remainingDirectories = new HashSet<string>(directories.Keys, StringComparer.OrdinalIgnoreCase);
            TransferPartialPathPolicy.RequireDirectoryIdentity(root, directories[root]);
            if (!remainingDirectories.Remove(root))
                throw new IOException("Private staging root was not recorded.");
            var remainingFiles = new HashSet<string>(files.Keys, StringComparer.OrdinalIgnoreCase);
            long observedBytes = 0;
            var buffer = new byte[64 * 1024];
            var refusal = SafeFileTreeTraversal.Walk(root, SafeFileTreeTraversal.DefaultMaxEntries,
                token, (path, attributes) =>
                {
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (!remainingDirectories.Remove(path))
                            throw new IOException("Unexpected private staging directory.");
                        TransferPartialPathPolicy.RequireDirectoryIdentity(path, directories[path]);
                        return;
                    }
                    WrittenFile written;
                    if (!remainingFiles.Remove(path) || !files.TryGetValue(path, out written))
                        throw new IOException("Unexpected private staging file.");
                    using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
                    {
                        if (!MatchesWrittenFile(input, written, token, buffer))
                            throw new IOException("Private staging file identity or contents changed.");
                        observedBytes = checked(observedBytes + input.Length);
                    }
                });
            if (!string.IsNullOrEmpty(refusal) || remainingDirectories.Count != 0 ||
                remainingFiles.Count != 0 || observedBytes != expectedBytes)
                throw new IOException("Private staging tree did not match the recorded transfer.");
        }

        private static CloudTransferExecutionResult CleanupOwnedDirectory(IList<string> directories,
            IDictionary<string, TransferPartialPathPolicy.FileIdentity> directoryIdentities,
            IDictionary<string, WrittenFile> files, CloudTransferExecutionResult result)
        {
            var clean = true;
            var buffer = new byte[64 * 1024];
            foreach (var entry in files)
            {
                var outcome = CleanupOwnedFile(true, entry.Key, entry.Value, result, buffer);
                if (outcome.RequiresManualRecovery) clean = false;
            }

            // Only directories recorded after successful exclusive creation
            // are candidates, and only empty directories can be removed.
            for (var index = directories.Count - 1; index >= 0; index--)
            {
                try
                {
                    var path = directories[index];
                    TransferPartialPathPolicy.DeleteOwnedEmptyDirectory(path, directoryIdentities[path]);
                }
                catch (Exception) { clean = false; }
            }

            return clean ? result : CloudTransferExecutionResult.Failure(
                "Transfer cleanup could not be verified. Preserve staging for manual recovery.",
                requiresManualRecovery: true);
        }
    }
}
