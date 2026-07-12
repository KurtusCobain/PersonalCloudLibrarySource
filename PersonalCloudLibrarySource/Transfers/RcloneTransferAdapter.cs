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
            settings = settings ?? new PersonalCloudLibrarySourceSettings();
            var normalizedDestination = isDirectory
                ? (destinationPath ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : destinationPath ?? string.Empty;
            var partialPath = string.IsNullOrWhiteSpace(normalizedDestination)
                ? string.Empty
                : normalizedDestination + ".pcls-partial";

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

                DeletePartial(partialPath);
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
                    TimeoutSeconds = settings.RcloneTimeoutSeconds
                };
                var processResult = processRunner.Run(request, cancellationToken, progress);
                if (processResult.WasCancelled || cancellationToken.IsCancellationRequested)
                {
                    DeletePartial(partialPath);
                    return CloudTransferExecutionResult.CancelledResult(processResult.Message);
                }

                if (!processResult.Succeeded)
                {
                    DeletePartial(partialPath);
                    var detail = string.IsNullOrWhiteSpace(processResult.Error)
                        ? processResult.Message
                        : processResult.Message + " " + RcloneManifestReader.TrimForLog(processResult.Error);
                    return CloudTransferExecutionResult.Failure(detail, processResult.Exception);
                }

                long copiedBytes;
                if (!VerifyPartial(partialPath, isDirectory, out copiedBytes))
                {
                    DeletePartial(partialPath);
                    return CloudTransferExecutionResult.Failure(
                        "rclone completed, but the partial destination did not pass verification.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (isDirectory)
                {
                    Directory.Move(partialPath, normalizedDestination);
                }
                else
                {
                    File.Move(partialPath, normalizedDestination);
                }

                progress?.Invoke(copiedBytes, copiedBytes);
                return CloudTransferExecutionResult.Success(copiedBytes, copiedBytes);
            }
            catch (OperationCanceledException)
            {
                DeletePartial(partialPath);
                return CloudTransferExecutionResult.CancelledResult();
            }
            catch (Exception ex)
            {
                DeletePartial(partialPath);
                return CloudTransferExecutionResult.Failure(ex.Message, ex);
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

        private static void DeletePartial(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
    }
}
