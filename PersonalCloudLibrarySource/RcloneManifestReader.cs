using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

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
                var oversizedOutput = 0;

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
                    if (args.Data == null) return;
                    lock (output)
                    {
                        if (!TryAppendLineWithinLimit(output, args.Data, ManifestParserValidator.MaxManifestCharacters))
                            Interlocked.Exchange(ref oversizedOutput, 1);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data == null) return;
                    lock (error)
                    {
                        TryAppendLineWithinLimit(error, args.Data, 4096);
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

                var timer = Stopwatch.StartNew();
                while (!process.WaitForExit(200))
                {
                    if (Volatile.Read(ref oversizedOutput) != 0)
                    {
                        StopProcess(process);
                        throw new InvalidOperationException("Rclone manifest exceeds the 32 million character limit.");
                    }

                    if (timer.Elapsed.TotalSeconds > timeoutSeconds)
                    {
                        StopProcess(process);
                        throw new TimeoutException($"rclone manifest retrieval timed out after {timeoutSeconds} seconds.");
                    }
                }

                process.WaitForExit();
                if (Volatile.Read(ref oversizedOutput) != 0)
                    throw new InvalidOperationException("Rclone manifest exceeds the 32 million character limit.");

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

        internal static bool TryAppendLineWithinLimit(StringBuilder buffer, string line, int limit)
        {
            if (line == null) return true;
            if (limit < 0 || line.Length > limit - buffer.Length - Environment.NewLine.Length)
                return false;
            buffer.AppendLine(line);
            return true;
        }

        private static void StopProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            catch
            {
                // Preserve the original size or timeout failure.
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
