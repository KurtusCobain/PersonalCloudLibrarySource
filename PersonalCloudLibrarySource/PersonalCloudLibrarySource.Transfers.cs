using System;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource
    {
        private CloudTransferManager transferManager;
        private CloudTransferExecutor transferExecutor;
        private TransferQueueService transferQueue;
        private TransferRecoveryJournal transferRecoveryJournal;
        private DashboardTransferActivityBridge transferActivityBridge;
        private readonly DashboardRefreshPostGate dashboardTransferRefreshGate = new DashboardRefreshPostGate();
        private bool transferPresentationDisposed;
        private long lastDashboardTransferRevision = -1;

        internal CloudTransferManager GetTransferManager()
        {
            if (transferManager == null)
            {
                var concurrency = settings?.GetRuntimeSettingsSnapshot()?.TransferConcurrency ?? 1;
                transferManager = new CloudTransferManager(concurrency);
                transferPresentationDisposed = false;
                lastDashboardTransferRevision = -1;
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
                transferQueue = new TransferQueueService(GetTransferManager(), GetTransferExecutor(),
                    GetTransferRecoveryJournal());
                transferActivityBridge = new DashboardTransferActivityBridge(
                    transferQueue,
                    dashboardActivityService);
            }

            return transferQueue;
        }

        private TransferRecoveryJournal GetTransferRecoveryJournal()
        {
            if (transferRecoveryJournal == null)
                transferRecoveryJournal = new TransferRecoveryJournal(
                    System.IO.Path.Combine(GetPluginDataDirectory(), "transfer-recovery"));
            return transferRecoveryJournal;
        }

        private async void ReviewTransferRecovery()
        {
            var title = PclsResources.Get("LOCPLSTransferRecoveryReview", "Review Transfer Recovery");
            try
            {
                var journal = GetTransferRecoveryJournal();
                var snapshot = await System.Threading.Tasks.Task.Run(() => journal.Inspect());
                if (transferPresentationDisposed) return;
                var message = new System.Text.StringBuilder();
                message.AppendLine(PclsResources.Format("LOCPLSTransferRecoverySummary",
                    "Pending reservations: {0}; unreadable records: {1}.",
                    snapshot.Records.Count, snapshot.UnreadableCount));
                if (!snapshot.IsComplete)
                    message.AppendLine(PclsResources.Get("LOCPLSTransferRecoveryIncomplete",
                        "The journal could not be fully verified. Do not assume recovery is clear."));
                message.AppendLine(PclsResources.Get("LOCPLSTransferRecoveryAdvisory",
                    "Reservations may belong to active or interrupted transfers. This review does not authorize retry, deletion, or release. Preserve all staging and journal files."));
                foreach (var record in System.Linq.Enumerable.Take(snapshot.Records, 20))
                {
                    message.AppendLine();
                    message.AppendLine(PclsResources.Format("LOCPLSTransferRecoveryRecord",
                        "Job: {0}{1}Destination: {2}{1}Staging: {3}",
                        record.JobId, Environment.NewLine, record.Destination, record.StagingPath));
                }
                if (snapshot.Records.Count > 20)
                    message.AppendLine(PclsResources.Get("LOCPLSTransferRecoveryTruncated",
                        "Additional reservations are not displayed."));
                playniteApi.Dialogs.ShowMessage(message.ToString(), title);
            }
            catch (Exception)
            {
                if (!transferPresentationDisposed)
                    playniteApi.Dialogs.ShowMessage(PclsResources.Get("LOCPLSTransferRecoveryIncomplete",
                        "The journal could not be fully verified. Do not assume recovery is clear."), title);
            }
        }

        private void RetryHostedTransfer(Guid previousJobId)
        {
            var queue = GetTransferQueue();
            var gameId = queue.PrepareHostedRetry(previousJobId, settings.GetRuntimeSettingsSnapshot());
            try
            {
                var game = System.Linq.Enumerable.FirstOrDefault(playniteApi.Database.Games,
                    candidate => candidate.Id == gameId && candidate.PluginId == Id);
                if (game == null)
                    throw new TransferRetryRefusedException("The game is no longer owned by this library.");
                playniteApi.InstallGame(gameId);
            }
            catch (Exception)
            {
                queue.CancelHostedRetry(gameId, previousJobId);
                throw new TransferRetryRefusedException("Playnite could not start a fresh installation action for this retry.");
            }
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

            dashboardTransferRefreshGate.Request(
                action => playniteApi.MainView.UIDispatcher.BeginInvoke(action),
                () =>
                {
                    if (transferPresentationDisposed)
                    {
                        return;
                    }

                    var revision = GetTransferManager().LifecycleRevision;
                    if (revision != lastDashboardTransferRevision)
                    {
                        RefreshDashboardState();
                        UpdateNavigationItemState();
                        lastDashboardTransferRevision = revision;
                    }
                    else
                    {
                        // Byte-only progress does not change library membership
                        // or require rechecking every cached game on the UI thread.
                        UpdateSidebarTransferProgress();
                    }
                });
        }

        private void DisposeTransferManager()
        {
            transferPresentationDisposed = true;
            transferActivityBridge?.Dispose();
            transferActivityBridge = null;

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
