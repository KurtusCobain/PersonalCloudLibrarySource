using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

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

            string remoteManifestPath;
            if (!RcloneCommandBuilder.TryBuildRemoteOperand(remoteName, manifestPath, false, out remoteManifestPath))
                throw new InvalidOperationException("The configured rclone manifest source is invalid.");
            var json = ReadContainedCommand(executablePath, "cat " + QuoteArgument(remoteManifestPath),
                settings.RcloneTimeoutSeconds, ManifestParserValidator.MaxManifestCharacters);
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("rclone returned an empty manifest.");
            return json;
        }

        internal static string ListRemotes(PersonalCloudLibrarySourceSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var executable = string.IsNullOrWhiteSpace(settings.RcloneExecutablePath)
                ? "rclone" : settings.RcloneExecutablePath.Trim();
            return ReadContainedCommand(executable, "listremotes", settings.RcloneTimeoutSeconds, 64 * 1024);
        }

        private static string ReadContainedCommand(string executablePath, string arguments,
            int requestedTimeout, int outputLimit)
        {
            var timeoutSeconds = requestedTimeout < 5 || requestedTimeout > 86400 ? 30 : requestedTimeout;
            using (var process = new WindowsJobProcessHandle())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments + " --ask-password=false",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                try
                {
                    if (!process.Start())
                        throw new InvalidOperationException("rclone did not start.");
                }
                catch (Exception ex)
                {
                    // Launch failure is not proof that no native child exists.
                    StopProcess(process);
                    throw new InvalidOperationException("Unable to start rclone. Check the rclone executable path.", ex);
                }

                string json;
                try
                {
                    var timer = Stopwatch.StartNew();
                    // The native pipes are synchronous handles. Start both
                    // readers independently so one full pipe cannot starve the other.
                    var output = Task.Run(() => ReadBoundedAsync(process.StandardOutput,
                        outputLimit));
                    var stderr = Task.Run(() => DrainAsync(process.StandardError));
                    ObserveFailure(output);
                    ObserveFailure(stderr);

                    // Bound both process exit and pipe completion. Inherited
                    // pipe handles must not keep this caller blocked forever.
                    var exited = false;
                    Stopwatch descendants = null;
                    while (!exited || !output.IsCompleted || !stderr.IsCompleted)
                    {
                        if (!exited && process.RootExited)
                        {
                            if (descendants == null) descendants = Stopwatch.StartNew();
                            if (descendants.Elapsed >= TimeSpan.FromSeconds(5))
                                throw new InvalidOperationException("Rclone command descendants did not finish after root exit.");
                        }
                        if (output.IsFaulted)
                            throw new InvalidOperationException("Rclone command output could not be read within its character limit.");
                        if (stderr.IsFaulted)
                            throw new InvalidOperationException("Rclone diagnostic stream could not be drained.");
                        if (timer.Elapsed.TotalSeconds >= timeoutSeconds)
                            throw new TimeoutException($"rclone command timed out after {timeoutSeconds} seconds.");
                        if (!exited) exited = process.WaitForExit(100);
                        else Thread.Sleep(20);
                    }
                    json = output.GetAwaiter().GetResult();
                    stderr.GetAwaiter().GetResult();
                    if (process.ExitCode != 0)
                        throw new InvalidOperationException(
                            $"rclone command failed with exit code {process.ExitCode}. Check the executable and remote configuration.");
                }
                catch
                {
                    StopProcess(process);
                    throw;
                }

                return json;
            }
        }

        internal static async Task<string> ReadBoundedAsync(TextReader reader, int limit)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));
            var buffer = new char[8192];
            var output = new StringBuilder();
            while (true)
            {
                // Read at most the remaining budget plus one character.
                var count = (int)Math.Min(buffer.Length, (long)limit - output.Length + 1);
                var read = await reader.ReadAsync(buffer, 0, count).ConfigureAwait(false);
                if (read == 0) return output.ToString();
                if (read > limit - output.Length)
                    throw new InvalidDataException("Rclone manifest exceeds its character limit.");
                output.Append(buffer, 0, read);
            }
        }

        private static async Task DrainAsync(TextReader reader)
        {
            var buffer = new char[4096];
            while (await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false) > 0) { }
        }

        private static void ObserveFailure(Task task)
        {
            // A timed-out caller may dispose its stream while a read remains
            // outstanding. Observe any late exception without retaining output.
            task.ContinueWith(failed => { var ignored = failed.Exception; },
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted |
                TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private static void StopProcess(IRcloneProcessHandle process)
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
