using System;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource
    {
        private CloudTransferManager transferManager;
        private CloudTransferExecutor transferExecutor;
        private TransferQueueService transferQueue;

        internal CloudTransferManager GetTransferManager()
        {
            if (transferManager == null)
            {
                var concurrency = settings?.GetRuntimeSettingsSnapshot()?.TransferConcurrency ?? 1;
                transferManager = new CloudTransferManager(concurrency);
                transferManager.Changed += TransferManager_Changed;
            }

            return transferManager;
        }

        internal CloudTransferExecutor GetTransferExecutor()
        {
            if (transferExecutor == null)
            {
                transferExecutor = new CloudTransferExecutor(
                    GetTransferManager(),
                    new LocalTransferAdapter());
            }

            return transferExecutor;
        }

        internal TransferQueueService GetTransferQueue()
        {
            if (transferQueue == null)
            {
                transferQueue = new TransferQueueService(GetTransferManager(), GetTransferExecutor());
            }

            return transferQueue;
        }

        private int GetActiveTransferCount()
        {
            return GetTransferManager().ActiveCount;
        }

        private int GetFailedTransferCount()
        {
            return GetTransferManager().FailedCount;
        }

        private void SynchronizeTransferManagerSettings()
        {
            if (transferManager != null)
            {
                var concurrency = settings?.GetRuntimeSettingsSnapshot()?.TransferConcurrency ?? 1;
                if (transferQueue != null)
                {
                    transferQueue.SetMaxConcurrentTransfers(concurrency);
                }
                else
                {
                    transferManager.SetMaxConcurrentTransfers(concurrency);
                }
            }
        }

        private void UpdateSidebarTransferProgress()
        {
            if (dashboardSidebarItem == null)
            {
                return;
            }

            var progress = GetTransferManager().GetAggregateProgress();
            if (progress.ActiveJobCount == 0 || progress.IsIndeterminate || progress.TotalBytes <= 0)
            {
                dashboardSidebarItem.ProgressValue = 0;
                dashboardSidebarItem.ProgressMaximum = 100;
                return;
            }

            dashboardSidebarItem.ProgressValue = progress.BytesTransferred;
            dashboardSidebarItem.ProgressMaximum = progress.TotalBytes;
        }

        private void TransferManager_Changed(object sender, EventArgs e)
        {
            if (playniteApi?.MainView?.UIDispatcher == null)
            {
                return;
            }

            playniteApi.MainView.UIDispatcher.BeginInvoke(new Action(() =>
            {
                RefreshDashboardState();
                UpdateNavigationItemState();
            }));
        }

        private void DisposeTransferManager()
        {
            if (transferQueue != null)
            {
                transferQueue.Shutdown(TimeSpan.FromSeconds(10));
                transferQueue.Dispose();
                transferQueue = null;
            }

            if (transferManager != null)
            {
                transferManager.Changed -= TransferManager_Changed;
            }
        }
    }
}
