using System;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource
    {
        private CloudTransferManager transferManager;
        private CloudTransferExecutor transferExecutor;
        private TransferQueueService transferQueue;
        private TransferRecoveryJournal transferRecoveryJournal;
        private readonly object recoveryJournalSync = new object();
        private DashboardTransferActivityBridge transferActivityBridge;
        private readonly DashboardRefreshPostGate dashboardTransferRefreshGate = new DashboardRefreshPostGate();
        private bool transferPresentationDisposed;
        private bool recoveryReviewInProgress;
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
            lock (recoveryJournalSync)
            {
                if (transferRecoveryJournal == null)
                    transferRecoveryJournal = new TransferRecoveryJournal(
                        System.IO.Path.Combine(GetPluginDataDirectory(), "transfer-recovery"));
                return transferRecoveryJournal;
            }
        }

        private bool IsRecoveryClearForCatalogPublication()
        {
            // Include queued jobs and current-session quarantine even if a
            // marker is missing. Recheck after the namespace observation.
            // This is an advisory publication fence, not a transaction with
            // Playnite or a nonparticipating process.
            var manager = transferManager;
            if (manager != null && manager.GetReservationSnapshot().Count != 0)
                return false;
            if (!GetTransferRecoveryJournal().IsClearForCatalogPublication())
                return false;
            manager = transferManager;
            return manager == null || manager.GetReservationSnapshot().Count == 0;
        }

        private async void ReviewTransferRecovery()
        {
            if (recoveryReviewInProgress || transferPresentationDisposed) return;
            recoveryReviewInProgress = true;
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
                if (snapshot.UnverifiedOwnedJobIds.Count > 0)
                {
                    var sample = string.Join(", ", System.Linq.Enumerable.Take(snapshot.UnverifiedOwnedJobIds, 20));
                    message.AppendLine(PclsResources.Format("LOCPLSTransferRecoveryUnverifiedOwned",
                        "Current-session reservations not reconciled: {0}. Job IDs (first 20): {1}. Do not retry or edit the journal.",
                        snapshot.UnverifiedOwnedJobIds.Count, sample));
                }
                message.AppendLine(PclsResources.Get("LOCPLSTransferRecoveryAdvisory",
                    "Reservations may belong to active or interrupted transfers. This review does not authorize retry, deletion, or release. Preserve all staging and journal files."));
                message.AppendLine(PclsResources.Format("LOCPLSTransferRecoveryJournalLocation",
                    "Journal location: {0}", System.IO.Path.Combine(GetPluginDataDirectory(), "transfer-recovery")));
                foreach (var record in System.Linq.Enumerable.Take(snapshot.Records, 20))
                {
                    message.AppendLine();
                    message.AppendLine(PclsResources.Format("LOCPLSTransferRecoveryRecord",
                        "Job: {0}{1}Destination: {2} ({4}){1}Staging: {3} ({5})",
                        record.JobId, Environment.NewLine, record.Destination, record.StagingPath,
                        DescribeRecoveryPathState(record.DestinationState), DescribeRecoveryPathState(record.StagingState)));
                    message.AppendLine(PclsResources.Format("LOCPLSTransferRecoveryOrigin",
                        "Marker origin: {0}", DescribeRecoveryOrigin(record.Origin)));
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
            finally { recoveryReviewInProgress = false; }
        }

        private static string DescribeRecoveryOrigin(TransferRecoveryOrigin origin)
        {
            switch (origin)
            {
                case TransferRecoveryOrigin.CurrentJournal:
                    return PclsResources.Get("LOCPLSTransferRecoveryOriginCurrent", "recorded by this session; may still be active");
                case TransferRecoveryOrigin.Replaced:
                    return PclsResources.Get("LOCPLSTransferRecoveryOriginReplaced", "identity or binding changed; preserve evidence");
                default:
                    return PclsResources.Get("LOCPLSTransferRecoveryOriginUnknown", "unknown session; activity not established");
            }
        }

        private static string DescribeRecoveryPathState(TransferRecoveryPathState state)
        {
            switch (state)
            {
                case TransferRecoveryPathState.Missing:
                    return PclsResources.Get("LOCPLSTransferRecoveryPathMissing", "not found during review");
                case TransferRecoveryPathState.File:
                    return PclsResources.Get("LOCPLSTransferRecoveryPathFile", "file observed");
                case TransferRecoveryPathState.Directory:
                    return PclsResources.Get("LOCPLSTransferRecoveryPathDirectory", "directory observed");
                default:
                    return PclsResources.Get("LOCPLSTransferRecoveryPathUnverified", "unverified");
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
