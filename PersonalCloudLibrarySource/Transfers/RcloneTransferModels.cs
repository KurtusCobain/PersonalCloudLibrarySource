using System;

namespace PersonalCloudLibrarySource
{
    public sealed class RcloneTransferRequest
    {
        public string ExecutablePath { get; set; } = "rclone";
        public string RemoteName { get; set; } = string.Empty;
        public string RemoteSourcePath { get; set; } = string.Empty;
        public string DestinationPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
    }

    public sealed class RcloneProcessResult
    {
        public bool Succeeded { get; set; }
        public bool WasCancelled { get; set; }
        public bool TimedOut { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public Exception Exception { get; set; }

        public static RcloneProcessResult Success(string output)
        {
            return new RcloneProcessResult
            {
                Succeeded = true,
                ExitCode = 0,
                Output = output ?? string.Empty,
                Message = "rclone transfer completed."
            };
        }

        public static RcloneProcessResult Cancelled(string message)
        {
            return new RcloneProcessResult
            {
                WasCancelled = true,
                Message = message ?? "rclone transfer cancelled."
            };
        }

        public static RcloneProcessResult Failure(
            string message,
            string error = null,
            Exception exception = null,
            int exitCode = -1,
            bool timedOut = false)
        {
            return new RcloneProcessResult
            {
                Message = message ?? "rclone transfer failed.",
                Error = error ?? string.Empty,
                Exception = exception,
                ExitCode = exitCode,
                TimedOut = timedOut
            };
        }
    }

    public interface IRcloneProcessRunner
    {
        RcloneProcessResult Run(
            RcloneTransferRequest request,
            System.Threading.CancellationToken cancellationToken,
            Action<long, long?> progress);
    }
}
