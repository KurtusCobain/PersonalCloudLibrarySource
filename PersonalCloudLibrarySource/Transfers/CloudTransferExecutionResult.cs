using System;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudTransferExecutionResult
    {
        public bool Succeeded { get; set; }
        public bool Cancelled { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception Exception { get; set; }
        public long BytesTransferred { get; set; }
        public long? TotalBytes { get; set; }

        public static CloudTransferExecutionResult Success(long bytesTransferred, long? totalBytes)
        {
            return new CloudTransferExecutionResult
            {
                Succeeded = true,
                BytesTransferred = Math.Max(0, bytesTransferred),
                TotalBytes = totalBytes
            };
        }

        public static CloudTransferExecutionResult CancelledResult(string message = null)
        {
            return new CloudTransferExecutionResult
            {
                Cancelled = true,
                Message = message ?? "Transfer cancelled."
            };
        }

        public static CloudTransferExecutionResult Failure(string message, Exception exception = null)
        {
            return new CloudTransferExecutionResult
            {
                Message = message ?? "Transfer failed.",
                Exception = exception
            };
        }
    }
}
