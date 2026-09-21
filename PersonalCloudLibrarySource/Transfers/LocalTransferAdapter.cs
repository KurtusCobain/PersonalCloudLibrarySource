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

                DeleteFileIfExists(partialPath);
                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                var totalBytes = new FileInfo(sourcePath).Length;
                var transferred = CopyFileContents(
                    sourcePath,
                    partialPath,
                    cancellationToken,
                    totalBytes,
                    0,
                    progress);

                phase?.Invoke(CloudTransferState.Verifying);
                if (!File.Exists(partialPath) || new FileInfo(partialPath).Length != totalBytes)
                {
                    throw new IOException("The copied file did not pass size verification.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                phase?.Invoke(CloudTransferState.Finalizing);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(partialPath, destinationPath);
                return CloudTransferExecutionResult.Success(transferred, totalBytes);
            }
            catch (OperationCanceledException)
            {
                DeleteFileIfExists(partialPath);
                return CloudTransferExecutionResult.CancelledResult();
            }
            catch (Exception ex)
            {
                DeleteFileIfExists(partialPath);
                return CloudTransferExecutionResult.Failure(ex.Message, ex);
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

                DeleteDirectoryIfExists(partialDirectory);
                var destinationParent = Path.GetDirectoryName(normalizedDestination);
                if (!string.IsNullOrWhiteSpace(destinationParent))
                {
                    Directory.CreateDirectory(destinationParent);
                }

                var sourceRoot = Path.GetFullPath(sourceDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var directories = Directory.GetDirectories(sourceRoot, "*", SearchOption.AllDirectories);
                var files = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
                var totalBytes = files.Sum(file => new FileInfo(file).Length);
                var transferred = 0L;

                Directory.CreateDirectory(partialDirectory);
                foreach (var directory in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Directory.CreateDirectory(Path.Combine(partialDirectory, GetRelativePath(sourceRoot, directory)));
                }

                foreach (var sourceFile in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relativePath = GetRelativePath(sourceRoot, sourceFile);
                    var destinationFile = Path.Combine(partialDirectory, relativePath);
                    var targetDirectory = Path.GetDirectoryName(destinationFile);
                    if (!string.IsNullOrWhiteSpace(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    transferred = CopyFileContents(
                        sourceFile,
                        destinationFile,
                        cancellationToken,
                        totalBytes,
                        transferred,
                        progress);
                }

                phase?.Invoke(CloudTransferState.Verifying);
                var copiedBytes = Directory.GetFiles(partialDirectory, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
                if (copiedBytes != totalBytes)
                {
                    throw new IOException("The copied directory did not pass size verification.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                phase?.Invoke(CloudTransferState.Finalizing);
                cancellationToken.ThrowIfCancellationRequested();
                Directory.Move(partialDirectory, normalizedDestination);
                return CloudTransferExecutionResult.Success(transferred, totalBytes);
            }
            catch (OperationCanceledException)
            {
                DeleteDirectoryIfExists(partialDirectory);
                return CloudTransferExecutionResult.CancelledResult();
            }
            catch (Exception ex)
            {
                DeleteDirectoryIfExists(partialDirectory);
                return CloudTransferExecutionResult.Failure(ex.Message, ex);
            }
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
            Action<long, long?> progress)
        {
            var transferred = alreadyTransferred;
            var buffer = new byte[bufferSize];
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

        private static void DeleteFileIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
