using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PersonalCloudLibrarySource
{
    public class RcloneFileCopier
    {
        public RcloneCopyResult CopyRemoteFileToLocalPath(
            PersonalCloudLibrarySourceSettings settings,
            string remotePath,
            string localFullFilePath)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(remotePath))
            {
                return RcloneCopyResult.Fail("Remote path is empty.");
            }

            if (string.IsNullOrWhiteSpace(localFullFilePath))
            {
                return RcloneCopyResult.Fail("Local destination file path could not be resolved.");
            }

            var executablePath = string.IsNullOrWhiteSpace(settings.RcloneExecutablePath)
                ? "rclone"
                : settings.RcloneExecutablePath.Trim();
            var remoteName = (settings.RcloneRemoteName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(remoteName))
            {
                return RcloneCopyResult.Fail("Rclone remote name is required.");
            }

            var destinationFolder = Path.GetDirectoryName(localFullFilePath);
            if (string.IsNullOrWhiteSpace(destinationFolder))
            {
                return RcloneCopyResult.Fail("Local destination folder could not be resolved.");
            }

            Directory.CreateDirectory(destinationFolder);
            var remoteItemPath = $"{remoteName}:{remotePath.Trim()}";
            return RunRcloneCopy(settings, executablePath, remoteItemPath, localFullFilePath, "copyto");
        }

        public RcloneCopyResult CopyRemoteDirectoryToLocalPath(
            PersonalCloudLibrarySourceSettings settings,
            string remotePath,
            string localDirectoryPath)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(remotePath))
            {
                return RcloneCopyResult.Fail("Remote path is empty.");
            }

            if (string.IsNullOrWhiteSpace(localDirectoryPath))
            {
                return RcloneCopyResult.Fail("Local destination directory path could not be resolved.");
            }

            var executablePath = string.IsNullOrWhiteSpace(settings.RcloneExecutablePath)
                ? "rclone"
                : settings.RcloneExecutablePath.Trim();
            var remoteName = (settings.RcloneRemoteName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(remoteName))
            {
                return RcloneCopyResult.Fail("Rclone remote name is required.");
            }

            Directory.CreateDirectory(localDirectoryPath);
            var remoteItemPath = $"{remoteName}:{remotePath.Trim()}";
            return RunRcloneCopy(settings, executablePath, remoteItemPath, localDirectoryPath, "copy");
        }

        private static RcloneCopyResult RunRcloneCopy(
            PersonalCloudLibrarySourceSettings settings,
            string executablePath,
            string remoteItemPath,
            string localDestinationPath,
            string commandName)
        {
            var timeoutSeconds = settings.RcloneTimeoutSeconds < 5 ? 30 : settings.RcloneTimeoutSeconds;

            using (var process = new Process())
            {
                var output = new StringBuilder();
                var error = new StringBuilder();

                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = commandName + " " +
                        RcloneManifestReader.QuoteArgument(remoteItemPath) +
                        " " +
                        RcloneManifestReader.QuoteArgument(localDestinationPath),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        output.AppendLine(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        error.AppendLine(args.Data);
                    }
                };

                try
                {
                    process.Start();
                }
                catch (Exception ex)
                {
                    return RcloneCopyResult.Fail("Unable to start rclone. Check the rclone executable path.", ex);
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(timeoutSeconds * 1000))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                    }

                    return RcloneCopyResult.Fail($"rclone {commandName} timed out after {timeoutSeconds} seconds.");
                }

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    return RcloneCopyResult.Fail(
                        $"rclone {commandName} failed with exit code {process.ExitCode}: {RcloneManifestReader.TrimForLog(error.ToString())}");
                }

                return RcloneCopyResult.Success(RcloneManifestReader.TrimForLog(output.ToString()), commandName);
            }
        }
    }

    public class RcloneCopyResult
    {
        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
        public Exception Exception { get; private set; }

        public static RcloneCopyResult Success(string message, string commandName = "copyto")
        {
            return new RcloneCopyResult
            {
                Succeeded = true,
                Message = string.IsNullOrWhiteSpace(message) ? $"rclone {commandName} completed." : message
            };
        }

        public static RcloneCopyResult Fail(string message, Exception exception = null)
        {
            return new RcloneCopyResult
            {
                Succeeded = false,
                Message = message,
                Exception = exception
            };
        }
    }
}
