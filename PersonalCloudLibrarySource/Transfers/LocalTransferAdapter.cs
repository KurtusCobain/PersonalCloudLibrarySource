using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public sealed class LocalTransferAdapter
    {
        private readonly int bufferSize;

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
                    () => ownsPartial = true);

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
                try { TransferPartialPathPolicy.RequirePublicationPaths(partialPath, destinationPath, false); }
                catch (Exception)
                {
                    return CloudTransferExecutionResult.Failure(
                        "Transfer publication paths changed; staging was preserved for review.",
                        requiresManualRecovery: true);
                }
                File.Move(partialPath, destinationPath);
                return CloudTransferExecutionResult.Success(transferred, totalBytes);
            }
            catch (OperationCanceledException)
            {
                return CleanupOwnedPartial(ownsPartial, partialPath, false,
                    CloudTransferExecutionResult.CancelledResult());
            }
            catch (Exception ex)
            {
                return CleanupOwnedPartial(ownsPartial, partialPath, false,
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
            var ownsPartial = false;
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

                TransferPartialPathPolicy.CreateExclusiveDirectory(partialDirectory);
                ownsPartial = true;
                foreach (var directory in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        TransferPartialPathPolicy.CreateExclusiveDirectory(
                            Path.Combine(partialDirectory, GetRelativePath(sourceRoot, directory)));
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
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(targetDirectory))
                        {
                            TransferPartialPathPolicy.RequireSafePath(targetDirectory);
                            if (!Directory.Exists(targetDirectory))
                                throw new IOException("Private staging directory disappeared during transfer.");
                        }

                        transferred = CopyFileContents(
                            sourceFile,
                            destinationFile,
                            cancellationToken,
                            totalBytes,
                            transferred,
                            progress,
                            copyBuffer,
                            () => fileCreated = true);
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
                long copiedBytes;
                if (!SafeFileTreeTraversal.TryMeasure(partialDirectory, true, cancellationToken, out copiedBytes) ||
                    copiedBytes != totalBytes)
                {
                    throw new IOException("The copied directory did not pass size verification.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                phase?.Invoke(CloudTransferState.Finalizing);
                cancellationToken.ThrowIfCancellationRequested();
                try { TransferPartialPathPolicy.RequirePublicationPaths(partialDirectory, normalizedDestination, true); }
                catch (Exception)
                {
                    return CloudTransferExecutionResult.Failure(
                        "Transfer publication paths changed; staging was preserved for review.",
                        requiresManualRecovery: true);
                }
                Directory.Move(partialDirectory, normalizedDestination);
                return CloudTransferExecutionResult.Success(transferred, totalBytes);
            }
            catch (OperationCanceledException)
            {
                return CleanupOwnedPartial(ownsPartial, partialDirectory, true,
                    CloudTransferExecutionResult.CancelledResult());
            }
            catch (Exception ex)
            {
                return CleanupOwnedPartial(ownsPartial, partialDirectory, true,
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

        // Best-effort local destination preflight. When Windows cannot report free
        // space (some UNC providers), copy still follows normal IO failure/cleanup.
        private static string CheckDiskSpace(string destinationPath, long requiredBytes)
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(destinationPath));
                if (string.IsNullOrWhiteSpace(root))
                    return string.Empty;
                var drive = new DriveInfo(root);
                if (!drive.IsReady)
                    return string.Empty;
                if (!HasRequiredSpace(drive.AvailableFreeSpace, requiredBytes))
                    return "Not enough free space on the cache drive. Free space or select another cache folder.";
            }
            catch (ArgumentException) { }
            catch (IOException) { }
            catch (NotSupportedException) { }
            catch (UnauthorizedAccessException) { }
            return string.Empty;
        }

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
            Action<long, long?> progress, byte[] buffer, Action created = null)
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
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                FileOptions.SequentialScan))
            {
                created?.Invoke();
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var bytesRead = source.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    destination.Write(buffer, 0, bytesRead);
                    transferred += bytesRead;
                    progress?.Invoke(transferred, totalBytes);
                }

                destination.Flush(true);
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

        private static CloudTransferExecutionResult CleanupOwnedPartial(bool owned,
            string path, bool directory, CloudTransferExecutionResult result)
        {
            if (!owned) return result;
            try
            {
                TransferPartialPathPolicy.RequireSafePath(path);
                if (directory)
                {
                    if (File.Exists(path)) throw new IOException("Staging type changed.");
                    if (Directory.Exists(path))
                    {
                        var refusal = SafeCacheDeletionExecutor.GetUnsafeDescendantReason(path);
                        if (!string.IsNullOrWhiteSpace(refusal)) throw new IOException(refusal);
                        Directory.Delete(path, true);
                    }
                }
                else
                {
                    if (Directory.Exists(path)) throw new IOException("Staging type changed.");
                    if (File.Exists(path)) File.Delete(path);
                }
                return result;
            }
            catch (Exception)
            {
                return CloudTransferExecutionResult.Failure(
                    "Transfer cleanup could not be verified. Preserve staging for manual recovery.",
                    requiresManualRecovery: true);
            }
        }
    }
}
