using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudTransferManager
    {
        private readonly object syncRoot = new object();
        private readonly List<CloudTransferJob> jobs = new List<CloudTransferJob>();
        private readonly Dictionary<Guid, CloudTransferJob> jobsById = new Dictionary<Guid, CloudTransferJob>();
        private readonly Dictionary<Guid, List<CloudTransferJob>> jobsByGame = new Dictionary<Guid, List<CloudTransferJob>>();
        private readonly List<CloudTransferJob> activeJobs = new List<CloudTransferJob>();
        private readonly List<CloudTransferJob> queuedJobs = new List<CloudTransferJob>();
        private readonly Dictionary<Guid, CloudTransferJob> reservedJobs = new Dictionary<Guid, CloudTransferJob>();
        private int failedCount;
        private int maxConcurrentTransfers;
        private long lifecycleRevision;

        public long LifecycleRevision
        {
            get { lock (syncRoot) return lifecycleRevision; }
        }

        public CloudTransferManager(int maxConcurrentTransfers)
        {
            this.maxConcurrentTransfers = NormalizeConcurrency(maxConcurrentTransfers);
        }

        public event EventHandler Changed;

        public int MaxConcurrentTransfers
        {
            get
            {
                lock (syncRoot)
                {
                    return maxConcurrentTransfers;
                }
            }
        }

        public IReadOnlyList<CloudTransferJob> Jobs
        {
            get
            {
                lock (syncRoot)
                {
                    return jobs.ToList().AsReadOnly();
                }
            }
        }

        // Includes active, queued, and terminal attempts requiring recovery.
        // The queue separately retains terminal publication in flight.
        public IReadOnlyList<CloudTransferJob> GetReservationSnapshot()
        {
            lock (syncRoot) return reservedJobs.Values.ToArray();
        }

        public int ActiveCount
        {
            get
            {
                lock (syncRoot)
                {
                    return activeJobs.Count;
                }
            }
        }

        public int QueuedCount
        {
            get
            {
                lock (syncRoot)
                {
                    return queuedJobs.Count;
                }
            }
        }

        public int FailedCount
        {
            get
            {
                lock (syncRoot)
                {
                    return failedCount;
                }
            }
        }

        public CloudTransferJob Enqueue(
            Guid gameId,
            string displayName,
            string source,
            string destination,
            string providerType,
            bool isDirectory = false)
        {
            CloudTransferJob job;
            lock (syncRoot)
            {
                job = new CloudTransferJob(
                    gameId,
                    displayName,
                    source,
                    destination,
                    providerType,
                    isDirectory);
                RegisterJobLocked(job);
                lifecycleRevision++;
                StartQueuedJobsLocked();
            }

            OnChanged();
            return job;
        }

        public CloudTransferJob GetJob(Guid jobId)
        {
            lock (syncRoot)
            {
                return FindJobLocked(jobId);
            }
        }

        public CloudTransferJob GetActiveJobForGame(Guid gameId)
        {
            lock (syncRoot)
            {
                List<CloudTransferJob> attempts;
                if (!jobsByGame.TryGetValue(gameId, out attempts)) return null;
                CloudTransferJob newest = null;
                foreach (var job in attempts)
                    if (!job.IsTerminal && (newest == null || job.CreatedAt >= newest.CreatedAt))
                        newest = job;
                return newest;
            }
        }

        public CloudTransferJob GetLatestRetryableJobForGame(Guid gameId)
        {
            lock (syncRoot)
            {
                List<CloudTransferJob> attempts;
                if (!jobsByGame.TryGetValue(gameId, out attempts)) return null;
                CloudTransferJob newest = null;
                foreach (var job in attempts)
                {
                    if (!job.CanRetry) continue;
                    if (newest == null ||
                        (job.CompletedAt ?? job.CreatedAt) > (newest.CompletedAt ?? newest.CreatedAt) ||
                        ((job.CompletedAt ?? job.CreatedAt) == (newest.CompletedAt ?? newest.CreatedAt) &&
                            job.CreatedAt >= newest.CreatedAt))
                        newest = job;
                }
                return newest;
            }
        }

        internal void MarkRequiresManualRecovery(Guid jobId)
        {
            lock (syncRoot)
            {
                var job = FindJobLocked(jobId);
                if (job.RequiresManualRecovery) return;
                job.RequiresManualRecovery = true;
                reservedJobs[job.Id] = job;
                lifecycleRevision++;
            }
            OnChanged();
        }

        public void SetMaxConcurrentTransfers(int value)
        {
            lock (syncRoot)
            {
                maxConcurrentTransfers = NormalizeConcurrency(value);
                lifecycleRevision++;
                StartQueuedJobsLocked();
            }

            OnChanged();
        }

        public void Transition(Guid jobId, CloudTransferState nextState, string errorSummary = null)
        {
            lock (syncRoot)
            {
                var job = FindJobLocked(jobId);
                if (!IsTransitionAllowed(job.State, nextState))
                {
                    throw new InvalidOperationException(
                        "Transfer job cannot move from " + job.State + " to " + nextState + ".");
                }

                if (nextState == CloudTransferState.Finalizing &&
                    job.CancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(job.CancellationToken);
                }

                if (nextState == CloudTransferState.Cancelled)
                {
                    job.RequestCancellation();
                }

                SetStateLocked(job, nextState, errorSummary);
                lifecycleRevision++;
                if (job.IsTerminal)
                {
                    StartQueuedJobsLocked();
                }
            }

            OnChanged();
        }

        public void UpdateProgress(Guid jobId, long bytesTransferred, long? totalBytes)
        {
            lock (syncRoot)
            {
                var job = FindJobLocked(jobId);
                if (!job.IsActive)
                {
                    throw new InvalidOperationException("Progress can only be updated for an active transfer job.");
                }

                job.SetProgress(bytesTransferred, totalBytes);
            }

            OnChanged();
        }

        public bool Cancel(Guid jobId)
        {
            lock (syncRoot)
            {
                var job = FindJobLocked(jobId);
                if (job.IsTerminal)
                {
                    return false;
                }

                if (job.State == CloudTransferState.Finalizing ||
                    job.CancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                job.RequestCancellation();
            }

            OnChanged();
            return true;
        }

        public CloudTransferJob Retry(Guid failedJobId)
        {
            CloudTransferJob retry;
            lock (syncRoot)
            {
                var failedJob = FindJobLocked(failedJobId);
                if (!failedJob.CanRetry)
                {
                    throw new InvalidOperationException("Only failed or cancelled transfer jobs can be retried.");
                }

                retry = new CloudTransferJob(
                    failedJob.GameId,
                    failedJob.DisplayName,
                    failedJob.Source,
                    failedJob.Destination,
                    failedJob.ProviderType,
                    failedJob.IsDirectory,
                    failedJob.Id);
                RegisterJobLocked(retry);
                lifecycleRevision++;
                StartQueuedJobsLocked();
            }

            OnChanged();
            return retry;
        }

        public CloudTransferAggregateProgress GetAggregateProgress()
        {
            lock (syncRoot)
            {
                if (activeJobs.Count == 0)
                {
                    return new CloudTransferAggregateProgress(0, 0, 0, false);
                }

                var isIndeterminate = activeJobs.Any(job => !job.TotalBytes.HasValue || job.TotalBytes.Value <= 0);
                long bytesTransferred = 0;
                long totalBytes = 0;
                foreach (var job in activeJobs)
                {
                    var bytes = Math.Max(0, job.BytesTransferred);
                    var total = Math.Max(0, job.TotalBytes.GetValueOrDefault());
                    if (bytes > long.MaxValue - bytesTransferred || total > long.MaxValue - totalBytes)
                    {
                        // The aggregate cannot represent the provider totals.
                        // Show indeterminate rather than overflow or a fake percentage.
                        isIndeterminate = true;
                        bytesTransferred = 0;
                        break;
                    }
                    bytesTransferred += bytes;
                    totalBytes += total;
                }
                if (isIndeterminate) totalBytes = 0;

                return new CloudTransferAggregateProgress(
                    activeJobs.Count,
                    bytesTransferred,
                    totalBytes,
                    isIndeterminate);
            }
        }

        private void StartQueuedJobsLocked()
        {
            var availableSlots = Math.Max(0, maxConcurrentTransfers - activeJobs.Count);
            if (availableSlots == 0 || queuedJobs.Count == 0) return;
            foreach (var queuedJob in queuedJobs
                .OrderBy(job => job.CreatedAt)
                .Take(availableSlots).ToArray())
            {
                SetStateLocked(queuedJob, CloudTransferState.Preparing, null);
            }
        }

        private void RegisterJobLocked(CloudTransferJob job)
        {
            jobsById.Add(job.Id, job);
            List<CloudTransferJob> attempts;
            if (!jobsByGame.TryGetValue(job.GameId, out attempts))
            {
                attempts = new List<CloudTransferJob>();
                jobsByGame.Add(job.GameId, attempts);
            }
            attempts.Add(job);
            jobs.Add(job);
            reservedJobs.Add(job.Id, job);
            queuedJobs.Add(job);
        }

        // Live indexes are maintained under the same lock as the full history.
        // Terminal attempts remain in the identity and per-game history indexes.
        private void SetStateLocked(CloudTransferJob job, CloudTransferState state, string error)
        {
            var wasActive = job.IsActive;
            var wasQueued = job.State == CloudTransferState.Queued;
            job.SetState(state, error);
            if (wasQueued && state != CloudTransferState.Queued) queuedJobs.Remove(job);
            if (wasActive && !job.IsActive) activeJobs.Remove(job);
            else if (!wasActive && job.IsActive) activeJobs.Add(job);
            if (state == CloudTransferState.Failed) failedCount++;
            if (job.IsTerminal && !job.RequiresManualRecovery) reservedJobs.Remove(job.Id);
        }

        private CloudTransferJob FindJobLocked(Guid jobId)
        {
            CloudTransferJob job;
            if (!jobsById.TryGetValue(jobId, out job))
            {
                throw new KeyNotFoundException("Transfer job was not found: " + jobId);
            }

            return job;
        }

        private static bool IsTransitionAllowed(CloudTransferState current, CloudTransferState next)
        {
            if (current == next)
            {
                return false;
            }

            switch (current)
            {
                case CloudTransferState.Queued:
                    return next == CloudTransferState.Preparing ||
                           next == CloudTransferState.Cancelled;

                case CloudTransferState.Preparing:
                    return next == CloudTransferState.Connecting ||
                           next == CloudTransferState.CalculatingSize ||
                           next == CloudTransferState.Transferring ||
                           next == CloudTransferState.Failed ||
                           next == CloudTransferState.Cancelled;

                case CloudTransferState.Connecting:
                    return next == CloudTransferState.CalculatingSize ||
                           next == CloudTransferState.Transferring ||
                           next == CloudTransferState.Failed ||
                           next == CloudTransferState.Cancelled;

                case CloudTransferState.CalculatingSize:
                    return next == CloudTransferState.Transferring ||
                           next == CloudTransferState.Failed ||
                           next == CloudTransferState.Cancelled;

                case CloudTransferState.Transferring:
                    return next == CloudTransferState.Verifying ||
                           next == CloudTransferState.Finalizing ||
                           next == CloudTransferState.Completed ||
                           next == CloudTransferState.Failed ||
                           next == CloudTransferState.Cancelled;

                case CloudTransferState.Verifying:
                    return next == CloudTransferState.Finalizing ||
                           next == CloudTransferState.Completed ||
                           next == CloudTransferState.Failed ||
                           next == CloudTransferState.Cancelled;

                case CloudTransferState.Finalizing:
                    return next == CloudTransferState.Completed ||
                           next == CloudTransferState.Failed ||
                           next == CloudTransferState.Cancelled;

                default:
                    return false;
            }
        }

        private static int NormalizeConcurrency(int value)
        {
            return value >= 1 && value <= 4 ? value : 1;
        }

        private void OnChanged()
        {
            var handlers = Changed;
            if (handlers == null) return;
            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try { handler(this, EventArgs.Empty); }
                catch (Exception)
                {
                    // A presentation observer cannot interrupt an already
                    // committed state change or strand queue-owned work.
                }
            }
        }
    }
}
