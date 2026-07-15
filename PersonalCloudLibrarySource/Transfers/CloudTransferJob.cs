using System;
using System.Collections.Generic;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public enum CloudTransferState
    {
        Queued = 0,
        Preparing = 1,
        Connecting = 2,
        CalculatingSize = 3,
        Transferring = 4,
        Verifying = 5,
        Finalizing = 6,
        Completed = 7,
        Cancelled = 8,
        Failed = 9
    }

    public sealed class CloudTransferJob : ObservableObject
    {
        private readonly CancellationTokenSource cancellationSource = new CancellationTokenSource();
        private CloudTransferState state;
        private long bytesTransferred;
        private long? totalBytes;
        private string errorSummary = string.Empty;
        private DateTime? startedAt;
        private DateTime? completedAt;

        internal CloudTransferJob(
            Guid gameId,
            string displayName,
            string source,
            string destination,
            string providerType,
            bool isDirectory,
            Guid? previousAttemptId = null)
        {
            Id = Guid.NewGuid();
            GameId = gameId;
            DisplayName = displayName ?? string.Empty;
            Source = source ?? string.Empty;
            Destination = destination ?? string.Empty;
            ProviderType = providerType ?? string.Empty;
            IsDirectory = isDirectory;
            PreviousAttemptId = previousAttemptId;
            State = CloudTransferState.Queued;
            CreatedAt = DateTime.UtcNow;
        }

        public Guid Id { get; }
        public Guid GameId { get; }
        public string DisplayName { get; }
        public string Source { get; }
        public string Destination { get; }
        public string ProviderType { get; }
        public bool IsDirectory { get; }
        public Guid? PreviousAttemptId { get; }
        public DateTime CreatedAt { get; }
        public CancellationToken CancellationToken => cancellationSource.Token;

        public CloudTransferState State
        {
            get => state;
            private set => SetValue(ref state, value);
        }

        public long BytesTransferred
        {
            get => bytesTransferred;
            private set => SetValue(ref bytesTransferred, value);
        }

        public long? TotalBytes
        {
            get => totalBytes;
            private set => SetValue(ref totalBytes, value);
        }

        public string ErrorSummary
        {
            get => errorSummary;
            private set => SetValue(ref errorSummary, value ?? string.Empty);
        }

        public DateTime? StartedAt
        {
            get => startedAt;
            private set => SetValue(ref startedAt, value);
        }

        public DateTime? CompletedAt
        {
            get => completedAt;
            private set => SetValue(ref completedAt, value);
        }

        public bool IsTerminal =>
            State == CloudTransferState.Completed ||
            State == CloudTransferState.Cancelled ||
            State == CloudTransferState.Failed;

        public bool IsActive =>
            State == CloudTransferState.Preparing ||
            State == CloudTransferState.Connecting ||
            State == CloudTransferState.CalculatingSize ||
            State == CloudTransferState.Transferring ||
            State == CloudTransferState.Verifying ||
            State == CloudTransferState.Finalizing;

        internal void SetState(CloudTransferState value, string errorSummary)
        {
            State = value;
            ErrorSummary = value == CloudTransferState.Failed ? errorSummary : string.Empty;

            if (value == CloudTransferState.Preparing && !StartedAt.HasValue)
            {
                StartedAt = DateTime.UtcNow;
            }

            if (value == CloudTransferState.Cancelled)
            {
                RequestCancellation();
            }

            if (IsTerminal)
            {
                CompletedAt = DateTime.UtcNow;
            }
        }

        internal void SetProgress(long transferred, long? total)
        {
            var safeTransferred = Math.Max(0, transferred);
            var safeTotal = total.HasValue ? Math.Max(0, total.Value) : (long?)null;
            if (safeTotal.HasValue && safeTransferred > safeTotal.Value)
            {
                safeTransferred = safeTotal.Value;
            }

            BytesTransferred = safeTransferred;
            TotalBytes = safeTotal;
        }

        internal void RequestCancellation()
        {
            if (!cancellationSource.IsCancellationRequested)
            {
                cancellationSource.Cancel();
            }
        }
    }

    public sealed class CloudTransferAggregateProgress
    {
        public CloudTransferAggregateProgress(
            int activeJobCount,
            long bytesTransferred,
            long totalBytes,
            bool isIndeterminate)
        {
            ActiveJobCount = Math.Max(0, activeJobCount);
            BytesTransferred = Math.Max(0, bytesTransferred);
            TotalBytes = Math.Max(0, totalBytes);
            IsIndeterminate = isIndeterminate;
        }

        public int ActiveJobCount { get; }
        public long BytesTransferred { get; }
        public long TotalBytes { get; }
        public bool IsIndeterminate { get; }
        public double Percentage => TotalBytes <= 0 ? 0 : (double)BytesTransferred / TotalBytes * 100;
    }
}
