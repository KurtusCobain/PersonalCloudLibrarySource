using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public sealed class RcloneProcessRunner : IRcloneProcessRunner
    {
        public RcloneProcessResult Run(
            RcloneTransferRequest request,
            CancellationToken cancellationToken,
            Action<long, long?> progress)
        {
            if (request == null)
            {
                return RcloneProcessResult.Failure("rclone request is missing.");
            }

            var output = new StringBuilder();
            var error = new StringBuilder();
            var syncRoot = new object();

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = string.IsNullOrWhiteSpace(request.ExecutablePath)
                            ? "rclone"
                            : request.ExecutablePath,
                        Arguments = RcloneCommandBuilder.BuildArguments(request),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    DataReceivedEventHandler outputHandler = (sender, args) =>
                    {
                        HandleLine(args.Data, output, syncRoot, progress);
                    };
                    DataReceivedEventHandler errorHandler = (sender, args) =>
                    {
                        HandleLine(args.Data, error, syncRoot, progress);
                    };
                    process.OutputDataReceived += outputHandler;
                    process.ErrorDataReceived += errorHandler;

                    if (!process.Start())
                    {
                        return RcloneProcessResult.Failure("rclone did not start.");
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    var timeoutSeconds = request.TimeoutSeconds >= 5 && request.TimeoutSeconds <= 86400
                        ? request.TimeoutSeconds
                        : 30;
                    var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

                    while (!process.WaitForExit(200))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            TryKill(process);
                            process.WaitForExit();
                            return RcloneProcessResult.Cancelled("rclone transfer cancelled.");
                        }

                        if (DateTime.UtcNow >= deadline)
                        {
                            TryKill(process);
                            process.WaitForExit();
                            return RcloneProcessResult.Failure(
                                "rclone transfer timed out after " + timeoutSeconds + " seconds.",
                                GetText(error, syncRoot),
                                null,
                                -1,
                                true);
                        }
                    }

                    process.WaitForExit();
                    process.OutputDataReceived -= outputHandler;
                    process.ErrorDataReceived -= errorHandler;

                    var outputText = GetText(output, syncRoot);
                    var errorText = GetText(error, syncRoot);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return RcloneProcessResult.Cancelled("rclone transfer cancelled.");
                    }

                    if (process.ExitCode == 0)
                    {
                        return RcloneProcessResult.Success(outputText);
                    }

                    return RcloneProcessResult.Failure(
                        "rclone stopped with exit code " + process.ExitCode + ".",
                        errorText,
                        null,
                        process.ExitCode);
                }
            }
            catch (Exception ex)
            {
                return RcloneProcessResult.Failure("Unable to run rclone: " + ex.Message, null, ex);
            }
        }

        private static void HandleLine(
            string line,
            StringBuilder target,
            object syncRoot,
            Action<long, long?> progress)
        {
            if (line == null)
            {
                return;
            }

            lock (syncRoot)
            {
                target.AppendLine(line);
            }

            long transferred;
            long total;
            if (RcloneProgressParser.TryParse(line, out transferred, out total))
            {
                progress?.Invoke(transferred, total);
            }
        }

        private static string GetText(StringBuilder builder, object syncRoot)
        {
            lock (syncRoot)
            {
                return builder.ToString();
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
            }
        }
    }
}
