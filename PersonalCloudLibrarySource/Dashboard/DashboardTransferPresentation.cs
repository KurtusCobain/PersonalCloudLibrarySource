using System;

namespace PersonalCloudLibrarySource
{
    public interface ITransferTerminalSource
    {
        event EventHandler<CloudTransferJobEventArgs> JobTerminated;
    }

    public interface IDashboardTransferActions
    {
        void Cancel(Guid jobId);
        void Retry(Guid jobId);
    }

    public sealed class DashboardTransferActions : IDashboardTransferActions
    {
        private const string DuplicateActiveTransferMessage =
            "A transfer attempt is already active for this game.";
        private readonly TransferQueueService queue;
        private readonly Func<PersonalCloudLibrarySourceSettings> settingsSnapshotFactory;

        public DashboardTransferActions(
            TransferQueueService queue,
            Func<PersonalCloudLibrarySourceSettings> settingsSnapshotFactory)
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
            this.settingsSnapshotFactory = settingsSnapshotFactory ??
                throw new ArgumentNullException(nameof(settingsSnapshotFactory));
        }

        public void Cancel(Guid jobId)
        {
            queue.Cancel(jobId);
        }

        public void Retry(Guid jobId)
        {
            try
            {
                queue.Retry(jobId, settingsSnapshotFactory());
            }
            catch (InvalidOperationException ex) when (
                string.Equals(ex.Message, DuplicateActiveTransferMessage, StringComparison.Ordinal))
            {
                // Another open dashboard view already claimed this retry.
            }
        }
    }

    public sealed class DashboardTransferActivityBridge : IDisposable
    {
        private readonly ITransferTerminalSource terminalSource;
        private readonly DashboardActivityService activityService;
        private readonly TransferActivityTracker tracker = new TransferActivityTracker();
        private readonly object syncRoot = new object();
        private bool disposed;

        public DashboardTransferActivityBridge(
            ITransferTerminalSource terminalSource,
            DashboardActivityService activityService)
        {
            this.terminalSource = terminalSource ?? throw new ArgumentNullException(nameof(terminalSource));
            this.activityService = activityService ?? throw new ArgumentNullException(nameof(activityService));
            this.terminalSource.JobTerminated += TerminalSource_JobTerminated;
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                terminalSource.JobTerminated -= TerminalSource_JobTerminated;
            }
        }

        private void TerminalSource_JobTerminated(object sender, CloudTransferJobEventArgs e)
        {
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                foreach (var record in tracker.CollectNew(new[] { e.Job }))
                {
                    activityService.Add(record.Kind, record.Message, record.TimestampUtc, record.GameId);
                }
            }
        }
    }
}
