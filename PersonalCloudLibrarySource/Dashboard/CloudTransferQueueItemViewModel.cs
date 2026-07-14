using System;
using System.Threading;
using System.Windows.Input;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudTransferQueueItemViewModel
    {
        private readonly Action retry;
        private int retryAvailable;

        public CloudTransferQueueItemViewModel(
            CloudTransferJob job,
            Action cancel,
            Action retry)
        {
            Job = job ?? throw new ArgumentNullException(nameof(job));
            DisplayName = string.IsNullOrWhiteSpace(job.DisplayName)
                ? PclsResources.Get("LOCPLSTransferUnnamed", "Unnamed transfer")
                : job.DisplayName;
            StateText = GetStateText(job.State);
            ProgressText = GetProgressText(job);
            CanCancel = !job.IsTerminal &&
                job.State != CloudTransferState.Finalizing &&
                !job.CancellationToken.IsCancellationRequested;
            retryAvailable = job.State == CloudTransferState.Failed || job.State == CloudTransferState.Cancelled
                ? 1
                : 0;
            this.retry = retry ?? (() => { });
            CancelCommand = new DelegateCommand(cancel ?? (() => { }), () => CanCancel);
            RetryCommand = new DelegateCommand(ExecuteRetry, () => CanRetry);
        }

        public CloudTransferJob Job { get; }
        public string DisplayName { get; }
        public string StateText { get; }
        public string ProgressText { get; }
        public bool CanCancel { get; }
        public bool CanRetry => Volatile.Read(ref retryAvailable) == 1;
        public ICommand CancelCommand { get; }
        public ICommand RetryCommand { get; }

        private void ExecuteRetry()
        {
            if (Interlocked.Exchange(ref retryAvailable, 0) != 1)
            {
                return;
            }

            ((DelegateCommand)RetryCommand).RaiseCanExecuteChanged();
            retry();
        }

        private static string GetStateText(CloudTransferState state)
        {
            switch (state)
            {
                case CloudTransferState.Queued:
                    return PclsResources.Get("LOCPLSTransferStateQueued", "Queued");
                case CloudTransferState.Preparing:
                    return PclsResources.Get("LOCPLSTransferStatePreparing", "Preparing");
                case CloudTransferState.Connecting:
                    return PclsResources.Get("LOCPLSTransferStateConnecting", "Connecting");
                case CloudTransferState.CalculatingSize:
                    return PclsResources.Get("LOCPLSTransferCalculatingSize", "Calculating size");
                case CloudTransferState.Transferring:
                    return PclsResources.Get("LOCPLSTransferStateTransferring", "Transferring");
                case CloudTransferState.Verifying:
                    return PclsResources.Get("LOCPLSTransferStateVerifying", "Verifying");
                case CloudTransferState.Finalizing:
                    return PclsResources.Get("LOCPLSTransferStateFinalizing", "Finalizing");
                case CloudTransferState.Completed:
                    return PclsResources.Get("LOCPLSTransferStateCompleted", "Completed");
                case CloudTransferState.Cancelled:
                    return PclsResources.Get("LOCPLSTransferStateCancelled", "Cancelled");
                case CloudTransferState.Failed:
                    return PclsResources.Get("LOCPLSTransferStateFailed", "Failed");
                default:
                    return PclsResources.Get("LOCPLSTransferStateUnknown", "Unknown");
            }
        }

        private static string GetProgressText(CloudTransferJob job)
        {
            if (job.State == CloudTransferState.Failed && !string.IsNullOrWhiteSpace(job.ErrorSummary))
            {
                return job.ErrorSummary;
            }

            if (job.TotalBytes.HasValue && job.TotalBytes.Value > 0)
            {
                var percentage = (double)job.BytesTransferred / job.TotalBytes.Value * 100;
                return FormatBytes(job.BytesTransferred) + " / " +
                       FormatBytes(job.TotalBytes.Value) +
                       " (" + percentage.ToString("0") + "%)";
            }

            if (job.BytesTransferred > 0)
            {
            return PclsResources.Format("LOCPLSTransferBytesTransferred", "{0} transferred", FormatBytes(job.BytesTransferred));
            }

            return GetStateText(job.State);
        }

        private static string FormatBytes(long bytes)
        {
            var safeBytes = Math.Max(0, bytes);
            if (safeBytes >= 1024L * 1024L * 1024L)
            {
                return (safeBytes / (1024d * 1024d * 1024d)).ToString("0.0") + " GB";
            }

            if (safeBytes >= 1024L * 1024L)
            {
                return (safeBytes / (1024d * 1024d)).ToString("0.0") + " MB";
            }

            if (safeBytes >= 1024L)
            {
                return (safeBytes / 1024d).ToString("0.0") + " KB";
            }

            return safeBytes + " B";
        }
    }
}
