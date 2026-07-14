using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudTransferManager
    {
        private readonly object syncRoot = new object();
        private readonly List<CloudTransferJob> jobs = new List<CloudTransferJob>();
        private int maxConcurrentTransfers;

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

        public int ActiveCount
        {
            get
            {
                lock (syncRoot)
                {
                    return jobs.Count(job => job.IsActive);
                }
            }
        }

        public int QueuedCount
        {
            get
            {
                lock (syncRoot)
                {
                    return jobs.Count(job => job.State == CloudTransferState.Queued);
                }
            }
        }

        public int FailedCount
        {
            get
            {
                lock (syncRoot)
                {
                    return jobs.Count(job => job.State == CloudTransferState.Failed);
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
                jobs.Add(job);
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
                return jobs
                    .Select((job, index) => new { Job = job, Index = index })
                    .Where(value => value.Job.GameId == gameId && !value.Job.IsTerminal)
                    .OrderByDescending(value => value.Job.CreatedAt)
                    .ThenByDescending(value => value.Index)
                    .Select(value => value.Job)
                    .FirstOrDefault();
            }
        }

        public CloudTransferJob GetLatestRetryableJobForGame(Guid gameId)
        {
            lock (syncRoot)
            {
                return jobs
                    .Select((job, index) => new { Job = job, Index = index })
                    .Where(value =>
                        value.Job.GameId == gameId &&
                        (value.Job.State == CloudTransferState.Failed || value.Job.State == CloudTransferState.Cancelled))
                    .OrderByDescending(value => value.Job.CompletedAt ?? value.Job.CreatedAt)
                    .ThenByDescending(value => value.Job.CreatedAt)
                    .ThenByDescending(value => value.Index)
                    .Select(value => value.Job)
                    .FirstOrDefault();
            }
        }

        public void SetMaxConcurrentTransfers(int value)
        {
            lock (syncRoot)
            {
                maxConcurrentTransfers = NormalizeConcurrency(value);
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

                job.SetState(nextState, errorSummary);
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
                if (failedJob.State != CloudTransferState.Failed &&
                    failedJob.State != CloudTransferState.Cancelled)
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
                jobs.Add(retry);
                StartQueuedJobsLocked();
            }

            OnChanged();
            return retry;
        }

        public CloudTransferAggregateProgress GetAggregateProgress()
        {
            lock (syncRoot)
            {
                var activeJobs = jobs.Where(job => job.IsActive).ToList();
                if (activeJobs.Count == 0)
                {
                    return new CloudTransferAggregateProgress(0, 0, 0, false);
                }

                var isIndeterminate = activeJobs.Any(job => !job.TotalBytes.HasValue || job.TotalBytes.Value <= 0);
                var bytesTransferred = activeJobs.Sum(job => Math.Max(0, job.BytesTransferred));
                var totalBytes = isIndeterminate
                    ? 0
                    : activeJobs.Sum(job => Math.Max(0, job.TotalBytes.GetValueOrDefault()));

                return new CloudTransferAggregateProgress(
                    activeJobs.Count,
                    bytesTransferred,
                    totalBytes,
                    isIndeterminate);
            }
        }

        private void StartQueuedJobsLocked()
        {
            var availableSlots = Math.Max(0, maxConcurrentTransfers - jobs.Count(job => job.IsActive));
            foreach (var queuedJob in jobs
                .Where(job => job.State == CloudTransferState.Queued)
                .OrderBy(job => job.CreatedAt)
                .Take(availableSlots))
            {
                queuedJob.SetState(CloudTransferState.Preparing, null);
            }
        }

        private CloudTransferJob FindJobLocked(Guid jobId)
        {
            var job = jobs.FirstOrDefault(value => value.Id == jobId);
            if (job == null)
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
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
