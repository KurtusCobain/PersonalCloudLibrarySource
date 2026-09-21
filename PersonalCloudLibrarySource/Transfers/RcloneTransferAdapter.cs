using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public sealed class RcloneTransferAdapter
    {
        private readonly IRcloneProcessRunner processRunner;

        public RcloneTransferAdapter(IRcloneProcessRunner processRunner)
        {
            this.processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        }

        public CloudTransferExecutionResult Copy(
            PersonalCloudLibrarySourceSettings settings,
            string remoteSourcePath,
            string destinationPath,
            bool isDirectory,
            CancellationToken cancellationToken,
            Action<long, long?> progress)
        {
            var normalized = NormalizeDestination(destinationPath, isDirectory);
            return CopyCore(
                settings,
                remoteSourcePath,
                normalized,
                isDirectory,
                string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized + ".pcls-partial",
                cancellationToken,
                progress,
                null);
        }

        public CloudTransferExecutionResult Copy(
            PersonalCloudLibrarySourceSettings settings,
            string remoteSourcePath,
            string destinationPath,
            bool isDirectory,
            Guid jobId,
            CancellationToken cancellationToken,
            Action<long, long?> progress,
            Action<CloudTransferState> phase = null)
        {
            var normalized = NormalizeDestination(destinationPath, isDirectory);
            return CopyCore(
                settings,
                remoteSourcePath,
                normalized,
                isDirectory,
                TransferPartialPathPolicy.Create(normalized, jobId),
                cancellationToken,
                progress,
                phase);
        }

        private CloudTransferExecutionResult CopyCore(
            PersonalCloudLibrarySourceSettings settings,
            string remoteSourcePath,
            string normalizedDestination,
            bool isDirectory,
            string partialPath,
            CancellationToken cancellationToken,
            Action<long, long?> progress,
            Action<CloudTransferState> phase)
        {
            settings = settings ?? new PersonalCloudLibrarySourceSettings();

            try
            {
                var validationError = Validate(settings, remoteSourcePath, normalizedDestination);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    return CloudTransferExecutionResult.Failure(validationError);
                }

                if (File.Exists(normalizedDestination) || Directory.Exists(normalizedDestination))
                {
                    return CloudTransferExecutionResult.Failure(
                        "Destination already exists: " + normalizedDestination);
                }

                string cleanupError;
                if (!TryDeletePartial(partialPath, out cleanupError))
                {
                    return CloudTransferExecutionResult.Failure("Transfer partial cleanup failed: " + cleanupError);
                }
                var parent = Path.GetDirectoryName(normalizedDestination);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                var request = new RcloneTransferRequest
                {
                    ExecutablePath = settings.RcloneExecutablePath,
                    RemoteName = settings.RcloneRemoteName,
                    RemoteSourcePath = remoteSourcePath,
                    DestinationPath = partialPath,
                    IsDirectory = isDirectory,
                    TimeoutSeconds = settings.RcloneTimeoutSeconds,
                    ConnectTimeoutSeconds = Math.Min(30, Math.Max(5, settings.RcloneTimeoutSeconds)),
                    InactivityTimeoutSeconds = settings.RcloneTimeoutSeconds
                };
                phase?.Invoke(CloudTransferState.Transferring);
                var processResult = processRunner.Run(request, cancellationToken, progress);
                if (processResult.WasCancelled || cancellationToken.IsCancellationRequested)
                {
                    if (!TryDeletePartial(partialPath, out cleanupError))
                    {
                        return CloudTransferExecutionResult.Failure("Transfer cancellation cleanup failed: " + cleanupError);
                    }

                    return CloudTransferExecutionResult.CancelledResult(processResult.Message);
                }

                if (!processResult.Succeeded)
                {
                    if (!TryDeletePartial(partialPath, out cleanupError))
                    {
                        return CloudTransferExecutionResult.Failure(
                            "Transfer failed and partial cleanup failed: " + cleanupError,
                            processResult.Exception);
                    }
                    var detail = string.IsNullOrWhiteSpace(processResult.Error)
                        ? processResult.Message
                        : processResult.Message + " " + RcloneManifestReader.TrimForLog(processResult.Error);
                    return CloudTransferExecutionResult.Failure(detail, processResult.Exception);
                }

                long copiedBytes;
                phase?.Invoke(CloudTransferState.Verifying);
                if (!VerifyPartial(partialPath, isDirectory, out copiedBytes))
                {
                    if (!TryDeletePartial(partialPath, out cleanupError))
                    {
                        return CloudTransferExecutionResult.Failure(
                            "Transfer verification failed and partial cleanup failed: " + cleanupError);
                    }
                    return CloudTransferExecutionResult.Failure(
                        "rclone completed, but the partial destination did not pass verification.");
                }

                progress?.Invoke(copiedBytes, copiedBytes);
                cancellationToken.ThrowIfCancellationRequested();
                phase?.Invoke(CloudTransferState.Finalizing);
                cancellationToken.ThrowIfCancellationRequested();
                if (isDirectory)
                {
                    Directory.Move(partialPath, normalizedDestination);
                }
                else
                {
                    File.Move(partialPath, normalizedDestination);
                }

                return CloudTransferExecutionResult.Success(copiedBytes, copiedBytes);
            }
            catch (OperationCanceledException)
            {
                string cleanupError;
                if (!TryDeletePartial(partialPath, out cleanupError))
                {
                    return CloudTransferExecutionResult.Failure("Transfer cancellation cleanup failed: " + cleanupError);
                }

                return CloudTransferExecutionResult.CancelledResult();
            }
            catch (Exception ex)
            {
                string cleanupError;
                return TryDeletePartial(partialPath, out cleanupError)
                    ? CloudTransferExecutionResult.Failure(ex.Message, ex)
                    : CloudTransferExecutionResult.Failure(ex.Message + " Partial cleanup failed: " + cleanupError, ex);
            }
        }

        private static string Validate(
            PersonalCloudLibrarySourceSettings settings,
            string remoteSourcePath,
            string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(settings.RcloneExecutablePath))
            {
                return "The rclone executable path is empty.";
            }

            if (string.IsNullOrWhiteSpace(settings.RcloneRemoteName))
            {
                return "The rclone remote name is empty.";
            }

            if (string.IsNullOrWhiteSpace(remoteSourcePath))
            {
                return "The remote source path is empty.";
            }

            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                return "The local destination path is empty.";
            }

            return string.Empty;
        }

        private static bool VerifyPartial(string partialPath, bool isDirectory, out long copiedBytes)
        {
            copiedBytes = 0;
            if (isDirectory)
            {
                if (!Directory.Exists(partialPath))
                {
                    return false;
                }

                var files = Directory.GetFiles(partialPath, "*", SearchOption.AllDirectories);
                copiedBytes = files.Sum(file => new FileInfo(file).Length);
                return files.Length > 0;
            }

            if (!File.Exists(partialPath))
            {
                return false;
            }

            copiedBytes = new FileInfo(partialPath).Length;
            return copiedBytes > 0;
        }

        private static string NormalizeDestination(string destinationPath, bool isDirectory)
        {
            return isDirectory
                ? (destinationPath ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : destinationPath ?? string.Empty;
        }

        private static bool TryDeletePartial(string path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
