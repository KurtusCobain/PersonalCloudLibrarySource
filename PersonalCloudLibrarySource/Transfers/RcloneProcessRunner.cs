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

            if (cancellationToken.IsCancellationRequested)
                return RcloneProcessResult.Cancelled("rclone transfer cancelled before launch.");

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

                    var clock = Stopwatch.StartNew();
                    var epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var connectTimeoutSeconds = NormalizeTimeout(request.ConnectTimeoutSeconds, request.TimeoutSeconds);
                    var inactivityTimeoutSeconds = NormalizeTimeout(request.InactivityTimeoutSeconds, request.TimeoutSeconds);
                    var activityTimeout = new RcloneActivityTimeout(epoch,
                        TimeSpan.FromSeconds(connectTimeoutSeconds), TimeSpan.FromSeconds(inactivityTimeoutSeconds));
                    var callbackFailed = 0;
                    var outputEnded = 0;
                    var errorEnded = 0;
                    var started = false;

                    DataReceivedEventHandler outputHandler = (sender, args) =>
                    {
                        if (args.Data == null) { Interlocked.Exchange(ref outputEnded, 1); return; }
                        try
                        {
                            HandleLine(args.Data, output, syncRoot, progress,
                                activityTimeout, epoch + clock.Elapsed);
                        }
                        catch { Interlocked.Exchange(ref callbackFailed, 1); }
                    };
                    DataReceivedEventHandler errorHandler = (sender, args) =>
                    {
                        if (args.Data == null) { Interlocked.Exchange(ref errorEnded, 1); return; }
                        try
                        {
                            HandleLine(args.Data, error, syncRoot, progress,
                                activityTimeout, epoch + clock.Elapsed);
                        }
                        catch { Interlocked.Exchange(ref callbackFailed, 1); }
                    };
                    process.OutputDataReceived += outputHandler;
                    process.ErrorDataReceived += errorHandler;

                    try
                    {
                        if (!process.Start())
                            return RcloneProcessResult.Failure("rclone did not start.");
                        started = true;
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        while (!process.WaitForExit(200))
                        {
                            if (Volatile.Read(ref callbackFailed) != 0)
                                return RcloneProcessResult.Failure("rclone progress callback failed.",
                                    processExitConfirmed: TryStop(process));
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return TryStop(process)
                                    ? RcloneProcessResult.Cancelled("rclone transfer cancelled.")
                                    : RcloneProcessResult.Failure("rclone cancellation could not stop the child process.",
                                        processExitConfirmed: false);
                            }

                            var expiredKind = activityTimeout.GetExpiredKind(epoch + clock.Elapsed);
                            if (expiredKind != RcloneTimeoutKind.None)
                            {
                                if (!TryStop(process))
                                {
                                    return RcloneProcessResult.Failure(
                                        "rclone timed out and the child process could not be stopped.",
                                        GetText(error, syncRoot),
                                        null,
                                        -1,
                                        true, processExitConfirmed: false);
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

                        // Bound asynchronous pipe EOF; descendants can retain pipe handles.
                        var drain = Stopwatch.StartNew();
                        while (Volatile.Read(ref outputEnded) == 0 || Volatile.Read(ref errorEnded) == 0)
                        {
                            if (drain.Elapsed >= TimeSpan.FromSeconds(5) || cancellationToken.IsCancellationRequested)
                                return RcloneProcessResult.Failure("rclone output drain was not confirmed.",
                                    processExitConfirmed: false);
                            Thread.Sleep(20);
                        }
                        if (Volatile.Read(ref callbackFailed) != 0)
                            return RcloneProcessResult.Failure("rclone progress callback failed.");

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
                    catch (Exception)
                    {
                        return RcloneProcessResult.Failure("Unable to run rclone; review the executable and transfer settings.",
                            processExitConfirmed: !started || TryStop(process));
                    }
                    finally
                    {
                        process.OutputDataReceived -= outputHandler;
                        process.ErrorDataReceived -= errorHandler;
                    }
                }
            }
            catch (Exception)
            {
                return RcloneProcessResult.Failure("Unable to initialize rclone process handling.",
                    processExitConfirmed: false);
            }
        }

        private static void HandleLine(
            string line,
            StringBuilder target,
            object syncRoot,
            Action<long, long?> progress,
            RcloneActivityTimeout activity, DateTime timestamp)
        {
            if (line == null)
            {
                return;
            }

            lock (syncRoot)
            {
                AppendLineBounded(target, line, 16 * 1024);
            }

            long transferred;
            long total;
            var parsed = RcloneProgressParser.TryParse(line, out transferred, out total);
            if (!string.IsNullOrWhiteSpace(line))
                activity.RecordObservation(timestamp, parsed ? (long?)transferred : null,
                    parsed ? (long?)total : null);
            if (parsed) progress?.Invoke(transferred, total);
        }

        // Retain a bounded diagnostic tail; progress parsing still receives
        // each original line and does not depend on the retained text.
        internal static void AppendLineBounded(StringBuilder target, string line, int limit)
        {
            if (limit <= Environment.NewLine.Length)
            {
                target.Clear();
                return;
            }
            var keep = limit - Environment.NewLine.Length;
            if (line.Length > keep)
            {
                target.Clear();
                target.Append(line, line.Length - keep, keep);
                target.AppendLine();
                return;
            }
            var added = line.Length + Environment.NewLine.Length;
            var excess = target.Length + added - limit;
            if (excess > 0)
                target.Remove(0, Math.Min(target.Length, excess));
            target.AppendLine(line);
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
