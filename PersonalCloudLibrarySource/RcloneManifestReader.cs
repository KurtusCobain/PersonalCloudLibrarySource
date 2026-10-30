using System;
using System.Diagnostics;
using System.Text;

namespace PersonalCloudLibrarySource
{
    public class RcloneManifestReader
    {
        public string ReadManifestJson(PersonalCloudLibrarySourceSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var executablePath = string.IsNullOrWhiteSpace(settings.RcloneExecutablePath)
                ? "rclone"
                : settings.RcloneExecutablePath.Trim();
            var remoteName = (settings.RcloneRemoteName ?? string.Empty).Trim();
            var manifestPath = (settings.RcloneManifestPath ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(remoteName))
            {
                throw new InvalidOperationException("Rclone remote name is required.");
            }

            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                throw new InvalidOperationException("Rclone manifest path is required.");
            }

            var remoteManifestPath = $"{remoteName}:{manifestPath}";
            var timeoutSeconds = settings.RcloneTimeoutSeconds < 5 ? 30 : settings.RcloneTimeoutSeconds;

            using (var process = new Process())
            {
                var output = new StringBuilder();
                var error = new StringBuilder();

                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "cat " + QuoteArgument(remoteManifestPath),
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
                    throw new InvalidOperationException("Unable to start rclone. Check the rclone executable path.", ex);
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

                    throw new TimeoutException($"rclone manifest retrieval timed out after {timeoutSeconds} seconds.");
                }

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"rclone cat failed with exit code {process.ExitCode}: {TrimForLog(error.ToString())}");
                }

                var json = output.ToString();
                if (string.IsNullOrWhiteSpace(json))
                {
                    throw new InvalidOperationException("rclone returned an empty manifest.");
                }

                return json;
            }
        }

        internal static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return RcloneCommandBuilder.QuoteArgument(value);
        }

        internal static string TrimForLog(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "No error output.";
            }

            var trimmed = value.Trim();
            return trimmed.Length <= 500 ? trimmed : trimmed.Substring(0, 500);
        }
    }

}
