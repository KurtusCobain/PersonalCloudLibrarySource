using System;
using System.IO;
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
            var mayOwnPartial = false;
            TransferPartialPathPolicy.FileIdentity? stagingIdentity = null;
            var processExitConfirmed = true;

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

                cancellationToken.ThrowIfCancellationRequested();
                TransferPartialPathPolicy.RequireVacant(partialPath);
                string cleanupError;
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
                    // This destination is already private job-owned staging.
                    // Avoid rclone's second random sidecar, which survives a
                    // killed child process and escapes exact-path cleanup.
                    UseInPlace = true,
                    TimeoutSeconds = settings.RcloneTimeoutSeconds,
                    ConnectTimeoutSeconds = Math.Min(30, Math.Max(5, settings.RcloneTimeoutSeconds)),
                    InactivityTimeoutSeconds = settings.RcloneTimeoutSeconds
                };
                phase?.Invoke(CloudTransferState.Transferring);
                TransferPartialPathPolicy.RequireVacant(partialPath);
                // Create and identify the staging root before the external
                // producer can write. A pathname alone is not ownership.
                try
                {
                    if (isDirectory)
                        stagingIdentity = TransferPartialPathPolicy.CreateOwnedDirectory(partialPath);
                    else
                    {
                        using (var stream = new FileStream(partialPath, FileMode.CreateNew,
                            FileAccess.ReadWrite, FileShare.None))
                            stagingIdentity = TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle);
                    }
                    mayOwnPartial = true;
                }
                catch (Exception)
                {
                    return CloudTransferExecutionResult.Failure(
                        "rclone staging ownership could not be established; preserve staging for review.",
                        requiresManualRecovery: true);
                }
                cancellationToken.ThrowIfCancellationRequested();
                processExitConfirmed = false;
                var processResult = processRunner.Run(request, cancellationToken, progress);
                processExitConfirmed = processResult.ProcessExitConfirmed;
                if (!processExitConfirmed)
                    return CloudTransferExecutionResult.Failure(
                        "rclone exit was not confirmed. Partial data was preserved; stop the process and review staging before retrying.",
                        requiresManualRecovery: true);

                if (processResult.WasCancelled || cancellationToken.IsCancellationRequested)
                {
                    if (!TryDeletePartial(partialPath, isDirectory, stagingIdentity, out cleanupError))
                    {
                        return CloudTransferExecutionResult.Failure("Transfer cancellation cleanup failed: " + cleanupError, requiresManualRecovery: true);
                    }

                    return CloudTransferExecutionResult.CancelledResult(processResult.Message);
                }

                if (!processResult.Succeeded)
                {
                    if (!TryDeletePartial(partialPath, isDirectory, stagingIdentity, out cleanupError))
                    {
                        return CloudTransferExecutionResult.Failure(
                            "Transfer failed and partial cleanup failed: " + cleanupError,
                            requiresManualRecovery: true);
                    }
                    // Do not propagate arbitrary provider stderr or exception text.
                    return CloudTransferExecutionResult.Failure(processResult.Message);
                }

                long copiedBytes;
                phase?.Invoke(CloudTransferState.Verifying);
                if (!SafeFileTreeTraversal.TryMeasure(partialPath, isDirectory, cancellationToken, out copiedBytes))
                {
                    if (!TryDeletePartial(partialPath, isDirectory, stagingIdentity, out cleanupError))
                    {
                        return CloudTransferExecutionResult.Failure(
                            "Transfer verification failed and partial cleanup failed: " + cleanupError,
                            requiresManualRecovery: true);
                    }
                    return CloudTransferExecutionResult.Failure(
                        "rclone completed, but the partial destination did not pass verification.");
                }

                progress?.Invoke(copiedBytes, copiedBytes);
                cancellationToken.ThrowIfCancellationRequested();
                phase?.Invoke(CloudTransferState.Finalizing);
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    TransferPartialPathPolicy.RequirePublicationPaths(partialPath, normalizedDestination, isDirectory);
                    if (isDirectory)
                        TransferPartialPathPolicy.PublishOwnedDirectory(partialPath, normalizedDestination, stagingIdentity.Value);
                    else
                    {
                        using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(partialPath, true))
                        {
                            if (!TransferPartialPathPolicy.MatchesPrivateFileIdentity(input.SafeFileHandle, stagingIdentity.Value) ||
                                input.Length != copiedBytes)
                                throw new IOException("rclone staging file identity or length changed.");
                            cancellationToken.ThrowIfCancellationRequested();
                            TransferPartialPathPolicy.PublishVerifiedHandle(input.SafeFileHandle, partialPath, normalizedDestination);
                        }
                        TransferPartialPathPolicy.RequireVacant(partialPath);
                    }
                }
                catch (Exception)
                {
                    return CloudTransferExecutionResult.Failure(
                        "Transfer publication paths changed; staging was preserved for review.",
                        requiresManualRecovery: true);
                }

                return CloudTransferExecutionResult.Success(copiedBytes, copiedBytes);
            }
            catch (OperationCanceledException)
            {
                if (!processExitConfirmed)
                    return CloudTransferExecutionResult.Failure("rclone exit could not be confirmed; preserve staging.",
                        requiresManualRecovery: true);
                if (!mayOwnPartial) return CloudTransferExecutionResult.CancelledResult();
                string cleanupError;
                if (!TryDeletePartial(partialPath, isDirectory, stagingIdentity, out cleanupError))
                {
                    return CloudTransferExecutionResult.Failure("Transfer cancellation cleanup failed: " + cleanupError, requiresManualRecovery: true);
                }

                return CloudTransferExecutionResult.CancelledResult();
            }
            catch (Exception ex)
            {
                if (!processExitConfirmed)
                    return CloudTransferExecutionResult.Failure("rclone exit could not be confirmed; preserve staging.",
                        requiresManualRecovery: true);
                if (!mayOwnPartial) return CloudTransferExecutionResult.Failure(ex.Message, ex);
                string cleanupError;
                return TryDeletePartial(partialPath, isDirectory, stagingIdentity, out cleanupError)
                    ? CloudTransferExecutionResult.Failure(ex.Message, ex)
                    : CloudTransferExecutionResult.Failure(ex.Message + " Partial cleanup failed: " + cleanupError, ex,
                        requiresManualRecovery: true);
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

            string operand;
            if (!RcloneCommandBuilder.TryBuildRemoteOperand(settings.RcloneRemoteName, remoteSourcePath, true, out operand))
                return "The configured rclone source is invalid.";
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                return "The local destination path is empty.";
            }

            return string.Empty;
        }

        private static string NormalizeDestination(string destinationPath, bool isDirectory)
        {
            return isDirectory
                ? (destinationPath ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : destinationPath ?? string.Empty;
        }

        internal static bool IsSafeStagedEntry(FileAttributes attributes)
        {
            return (attributes & FileAttributes.ReparsePoint) == 0;
        }

        private static bool TryDeletePartial(string path, bool isDirectory,
            TransferPartialPathPolicy.FileIdentity? identity, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
            {
                return true;
            }

            try
            {
                TransferPartialPathPolicy.RequireSafePath(path);
                FileAttributes attributes;
                try { attributes = File.GetAttributes(path); }
                catch (FileNotFoundException) { return true; }
                catch (DirectoryNotFoundException) { return true; }
                if (!IsSafeStagedEntry(attributes) ||
                    ((attributes & FileAttributes.Directory) != 0) != isDirectory)
                    throw new IOException("Transfer staging type or link status changed; refusing cleanup.");

                if (!identity.HasValue)
                    throw new IOException("Staging identity was not recorded.");
                if (!isDirectory)
                {
                    using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(path, true))
                    {
                        if (!TransferPartialPathPolicy.MatchesPrivateFileIdentity(input.SafeFileHandle, identity.Value))
                            throw new IOException("Staging file identity changed.");
                        TransferPartialPathPolicy.DeleteVerifiedFileHandle(input.SafeFileHandle);
                    }
                    TransferPartialPathPolicy.RequireVacant(path);
                }
                else
                {
                    // rclone creates the descendants, not PCLS. A successful
                    // process wait cannot establish their creation ownership.
                    // Delete only the original empty root; preserve nonempty
                    // staging and its reservation for explicit recovery.
                    TransferPartialPathPolicy.DeleteOwnedEmptyDirectory(path, identity.Value);
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
