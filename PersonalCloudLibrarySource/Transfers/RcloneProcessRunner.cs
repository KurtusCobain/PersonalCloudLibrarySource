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
            : this(new WindowsJobProcessFactory())
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
                    var launchAttempted = false;

                    var receiving = 1;
                    Action<string> outputLine = line =>
                    {
                        if (Volatile.Read(ref receiving) == 0) return;
                        if (line == null) { Interlocked.Exchange(ref outputEnded, 1); return; }
                        try
                        {
                            HandleLine(line, output, syncRoot, progress,
                                activityTimeout, epoch + clock.Elapsed);
                        }
                        catch { Interlocked.Exchange(ref callbackFailed, 1); }
                    };
                    Action<string> errorLine = line =>
                    {
                        if (Volatile.Read(ref receiving) == 0) return;
                        if (line == null) { Interlocked.Exchange(ref errorEnded, 1); return; }
                        try
                        {
                            HandleLine(line, error, syncRoot, progress,
                                activityTimeout, epoch + clock.Elapsed);
                        }
                        catch { Interlocked.Exchange(ref callbackFailed, 1); }
                    };
                    DataReceivedEventHandler outputHandler = (sender, args) => outputLine(args.Data);
                    DataReceivedEventHandler errorHandler = (sender, args) => errorLine(args.Data);
                    var contained = process as WindowsJobProcessHandle;
                    if (contained == null)
                    {
                        process.OutputDataReceived += outputHandler;
                        process.ErrorDataReceived += errorHandler;
                    }

                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return RcloneProcessResult.Cancelled("rclone transfer cancelled before launch.");
                        // Process.Start can fail after native creation (for example
                        // during redirected-stream initialization). An exception
                        // is not proof that no child exists.
                        launchAttempted = true;
                        if (!(contained == null ? process.Start() : contained.Start(cancellationToken)))
                            return RcloneProcessResult.Failure("rclone did not start.");
                        var bounded = process as RcloneProcessHandle;
                        if (contained != null)
                        {
                            Action readFailed = () => Interlocked.Exchange(ref callbackFailed, 1);
                            contained.BeginBoundedOutputRead(outputLine, readFailed);
                            contained.BeginBoundedErrorRead(errorLine, readFailed);
                        }
                        else if (bounded != null)
                        {
                            Action readFailed = () => Interlocked.Exchange(ref callbackFailed, 1);
                            bounded.BeginBoundedOutputRead(outputLine, readFailed);
                            bounded.BeginBoundedErrorRead(errorLine, readFailed);
                        }
                        else
                        {
                            // Retain the existing injected-process contract.
                            process.BeginOutputReadLine();
                            process.BeginErrorReadLine();
                        }

                        Stopwatch descendantGrace = null;
                        while (!process.WaitForExit(200))
                        {
                            if (contained != null && contained.RootExited)
                            {
                                if (descendantGrace == null) descendantGrace = Stopwatch.StartNew();
                                if (descendantGrace.Elapsed >= TimeSpan.FromSeconds(5))
                                    return RcloneProcessResult.Failure("rclone descendants did not finish after root exit.",
                                        processExitConfirmed: TryStop(process));
                            }
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
                            if (Volatile.Read(ref callbackFailed) != 0 ||
                                drain.Elapsed >= TimeSpan.FromSeconds(5) || cancellationToken.IsCancellationRequested)
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
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return TryStop(process)
                            ? RcloneProcessResult.Cancelled("rclone transfer cancelled.")
                            : RcloneProcessResult.Failure("rclone cancellation could not confirm process exit.",
                                processExitConfirmed: false);
                    }
                    catch (Exception ex)
                    {
                        return RcloneProcessResult.Failure(
                            DescribeFailure("Unable to run rclone; review the executable and transfer settings.", ex),
                            processExitConfirmed: !launchAttempted || TryStop(process));
                    }
                    finally
                    {
                        Interlocked.Exchange(ref receiving, 0);
                        if (contained == null)
                        {
                            process.OutputDataReceived -= outputHandler;
                            process.ErrorDataReceived -= errorHandler;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return RcloneProcessResult.Failure(
                    DescribeFailure("Unable to initialize rclone process handling.", ex),
                    processExitConfirmed: false);
            }
        }

        private static string DescribeFailure(string message, Exception error)
        {
            // Exception text, command lines and configuration may disclose
            // paths or remote credentials. Report only diagnostic codes.
            return message + " (" + error.GetType().Name + ", HRESULT 0x" +
                error.HResult.ToString("X8") + ").";
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
