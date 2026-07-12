using System;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource
    {
        private CloudTransferManager transferManager;
        private CloudTransferExecutor transferExecutor;

        internal CloudTransferManager GetTransferManager()
        {
            if (transferManager == null)
            {
                var concurrency = settings?.Settings?.TransferConcurrency ?? 1;
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
                transferManager.SetMaxConcurrentTransfers(settings?.Settings?.TransferConcurrency ?? 1);
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
            if (transferManager != null)
            {
                transferManager.Changed -= TransferManager_Changed;
            }
        }
    }
}
