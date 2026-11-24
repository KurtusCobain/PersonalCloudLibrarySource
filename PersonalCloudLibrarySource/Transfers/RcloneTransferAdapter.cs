using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public sealed class RcloneTransferAdapter
    {
        private readonly IRcloneProcessRunner processRunner;

        private sealed class StagedEntry
        {
            public TransferPartialPathPolicy.FileIdentity Identity;
            public bool Directory;
            public long Length;
            public byte[] Digest;
        }

        private sealed class StagedSnapshot
        {
            public readonly Dictionary<string, StagedEntry> Entries =
                new Dictionary<string, StagedEntry>(StringComparer.OrdinalIgnoreCase);
            public long Bytes;
        }

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
            StagedSnapshot snapshot = null;
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
                    if (!TryDeletePartial(partialPath, isDirectory, stagingIdentity, snapshot, out cleanupError))
                    {
                        return CloudTransferExecutionResult.Failure("Transfer cancellation cleanup failed: " + cleanupError, requiresManualRecovery: true);
                    }

                    return CloudTransferExecutionResult.CancelledResult(processResult.Message);
                }

                if (!processResult.Succeeded)
                {
                    if (!TryDeletePartial(partialPath, isDirectory, stagingIdentity, snapshot, out cleanupError))
                    {
                        return CloudTransferExecutionResult.Failure(
                            "Transfer failed and partial cleanup failed: " + cleanupError,
                            requiresManualRecovery: true);
                    }
                    // Do not propagate arbitrary provider stderr or exception text.
                    return CloudTransferExecutionResult.Failure(processResult.Message);
                }

                // Freeze the observed output after confirmed process-tree exit
                // and before calling external phase/progress handlers. This is
                // a post-run integrity observation, not descendant ownership.
                try
                {
                    snapshot = CaptureSnapshot(partialPath, isDirectory, stagingIdentity.Value, cancellationToken);
                }
                catch (Exception)
                {
                    return CloudTransferExecutionResult.Failure(
                        "Transfer verification failed; cleanup was refused because staging could not be verified.",
                        requiresManualRecovery: true);
                }
                var copiedBytes = snapshot.Bytes;
                phase?.Invoke(CloudTransferState.Verifying);
                progress?.Invoke(copiedBytes, copiedBytes);
                cancellationToken.ThrowIfCancellationRequested();
                phase?.Invoke(CloudTransferState.Finalizing);
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    TransferPartialPathPolicy.RequirePublicationPaths(partialPath, normalizedDestination, isDirectory);
                    if (isDirectory)
                    {
                        VerifyDirectorySnapshot(partialPath, snapshot, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        TransferPartialPathPolicy.PublishOwnedDirectory(partialPath, normalizedDestination, stagingIdentity.Value);
                    }
                    else
                    {
                        using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(partialPath, true))
                        {
                            VerifyFile(input, snapshot.Entries[partialPath], cancellationToken, new byte[64 * 1024]);
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
                if (!TryDeletePartial(partialPath, isDirectory, stagingIdentity, snapshot, out cleanupError))
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
                return TryDeletePartial(partialPath, isDirectory, stagingIdentity, snapshot, out cleanupError)
                    ? CloudTransferExecutionResult.Failure(ex.Message, ex)
                    : CloudTransferExecutionResult.Failure(ex.Message + " Partial cleanup failed: " + cleanupError, ex,
                        requiresManualRecovery: true);
            }
        }

        private static StagedSnapshot CaptureSnapshot(string root, bool directory,
            TransferPartialPathPolicy.FileIdentity expected, CancellationToken token)
        {
            var snapshot = new StagedSnapshot();
            var buffer = new byte[64 * 1024];
            Action<string, FileAttributes> observe = (path, attributes) =>
            {
                token.ThrowIfCancellationRequested();
                var entry = new StagedEntry { Directory = (attributes & FileAttributes.Directory) != 0 };
                if (entry.Directory)
                    entry.Identity = TransferPartialPathPolicy.ObserveDirectoryIdentity(path);
                else
                {
                    using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
                    {
                        entry.Identity = TransferPartialPathPolicy.CapturePrivateFileIdentity(input.SafeFileHandle);
                        entry.Length = input.Length;
                        entry.Digest = Digest(input, token, buffer);
                    }
                    snapshot.Bytes = checked(snapshot.Bytes + entry.Length);
                }
                snapshot.Entries.Add(path, entry);
            };
            observe(root, directory ? FileAttributes.Directory : FileAttributes.Normal);
            if (!TransferPartialPathPolicy.SameIdentity(snapshot.Entries[root].Identity, expected))
                throw new IOException("rclone staging root identity changed.");
            if (directory)
            {
                var refusal = SafeFileTreeTraversal.Walk(root, SafeFileTreeTraversal.DefaultMaxEntries, token, observe);
                if (!string.IsNullOrEmpty(refusal)) throw new IOException(refusal);
            }
            return snapshot;
        }

        private static byte[] Digest(FileStream input, CancellationToken token, byte[] buffer)
        {
            using (var digest = SHA256.Create())
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var count = input.Read(buffer, 0, buffer.Length);
                    if (count == 0) break;
                    digest.TransformBlock(buffer, 0, count, buffer, 0);
                }
                token.ThrowIfCancellationRequested();
                digest.TransformFinalBlock(new byte[0], 0, 0);
                return digest.Hash;
            }
        }

        private static void VerifyFile(FileStream input, StagedEntry entry, CancellationToken token, byte[] buffer)
        {
            if (entry.Directory ||
                !TransferPartialPathPolicy.MatchesPrivateFileIdentity(input.SafeFileHandle, entry.Identity) ||
                input.Length != entry.Length || !Digest(input, token, buffer).SequenceEqual(entry.Digest))
                throw new IOException("rclone staging file identity or content changed.");
        }

        private static void VerifyDirectorySnapshot(string root, StagedSnapshot snapshot, CancellationToken token)
        {
            TransferPartialPathPolicy.RequireDirectoryIdentity(root, snapshot.Entries[root].Identity);
            var remaining = new HashSet<string>(snapshot.Entries.Keys, StringComparer.OrdinalIgnoreCase);
            remaining.Remove(root);
            var buffer = new byte[64 * 1024];
            var refusal = SafeFileTreeTraversal.Walk(root, SafeFileTreeTraversal.DefaultMaxEntries, token,
                (path, attributes) =>
                {
                    StagedEntry entry;
                    if (!remaining.Remove(path) || !snapshot.Entries.TryGetValue(path, out entry) ||
                        entry.Directory != ((attributes & FileAttributes.Directory) != 0))
                        throw new IOException("rclone staging tree membership changed.");
                    if (entry.Directory)
                        TransferPartialPathPolicy.RequireDirectoryIdentity(path, entry.Identity);
                    else
                    {
                        using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
                            VerifyFile(input, entry, token, buffer);
                    }
                });
            if (!string.IsNullOrEmpty(refusal) || remaining.Count != 0)
                throw new IOException("rclone staging tree membership changed.");
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
            TransferPartialPathPolicy.FileIdentity? identity, StagedSnapshot snapshot, out string error)
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
                        if (snapshot != null)
                            VerifyFile(input, snapshot.Entries[path], CancellationToken.None, new byte[64 * 1024]);
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
