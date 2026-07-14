using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public sealed class RcloneProcessRunner : IRcloneProcessRunner
    {
        private readonly IRcloneProcessFactory processFactory;

        public RcloneProcessRunner()
            : this(new RcloneProcessFactory())
        {
        }

        public RcloneProcessRunner(IRcloneProcessFactory processFactory)
        {
            this.processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
        }

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
                using (var process = processFactory.Create())
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

                    RcloneActivityTimeout activityTimeout = null;

                    DataReceivedEventHandler outputHandler = (sender, args) =>
                    {
                        HandleLine(
                            args.Data,
                            output,
                            syncRoot,
                            progress,
                            () => activityTimeout?.RecordActivity(DateTime.UtcNow));
                    };
                    DataReceivedEventHandler errorHandler = (sender, args) =>
                    {
                        HandleLine(
                            args.Data,
                            error,
                            syncRoot,
                            progress,
                            () => activityTimeout?.RecordActivity(DateTime.UtcNow));
                    };
                    process.OutputDataReceived += outputHandler;
                    process.ErrorDataReceived += errorHandler;

                    if (!process.Start())
                    {
                        return RcloneProcessResult.Failure("rclone did not start.");
                    }

                    var connectTimeoutSeconds = NormalizeTimeout(
                        request.ConnectTimeoutSeconds,
                        request.TimeoutSeconds);
                    var inactivityTimeoutSeconds = NormalizeTimeout(
                        request.InactivityTimeoutSeconds,
                        request.TimeoutSeconds);
                    activityTimeout = new RcloneActivityTimeout(
                        DateTime.UtcNow,
                        TimeSpan.FromSeconds(connectTimeoutSeconds),
                        TimeSpan.FromSeconds(inactivityTimeoutSeconds));
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    while (!process.WaitForExit(200))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return TryStop(process)
                                ? RcloneProcessResult.Cancelled("rclone transfer cancelled.")
                                : RcloneProcessResult.Failure("rclone cancellation could not stop the child process.");
                        }

                        var expiredKind = activityTimeout.GetExpiredKind(DateTime.UtcNow);
                        if (expiredKind != RcloneTimeoutKind.None)
                        {
                            if (!TryStop(process))
                            {
                                return RcloneProcessResult.Failure(
                                    "rclone timed out and the child process could not be stopped.",
                                    GetText(error, syncRoot),
                                    null,
                                    -1,
                                    true);
                            }

                            return RcloneProcessResult.Failure(
                                expiredKind == RcloneTimeoutKind.Connect
                                    ? "rclone did not report activity within " + connectTimeoutSeconds + " seconds."
                                    : "rclone transfer was inactive for " + inactivityTimeoutSeconds + " seconds.",
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
            Action<long, long?> progress,
            Action activity)
        {
            if (line == null)
            {
                return;
            }

            lock (syncRoot)
            {
                target.AppendLine(line);
            }

            activity?.Invoke();

            long transferred;
            long total;
            if (RcloneProgressParser.TryParse(line, out transferred, out total))
            {
                progress?.Invoke(transferred, total);
            }
        }

        private static int NormalizeTimeout(int value, int fallback)
        {
            if (value >= 5 && value <= 86400)
            {
                return value;
            }

            return fallback >= 5 && fallback <= 86400 ? fallback : 30;
        }

        private static string GetText(StringBuilder builder, object syncRoot)
        {
            lock (syncRoot)
            {
                return builder.ToString();
            }
        }

        private static bool TryStop(IRcloneProcessHandle process)
        {
            if (process == null)
            {
                return true;
            }

            var killSucceeded = true;
            try
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        killSucceeded = false;
                    }
                }

                return process.WaitForExit(5000) && killSucceeded;
            }
            catch
            {
                return false;
            }
        }
    }
}
