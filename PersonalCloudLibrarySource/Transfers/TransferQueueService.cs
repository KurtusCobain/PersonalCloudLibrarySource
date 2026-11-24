using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudTransferJobEventArgs : EventArgs
    {
        public CloudTransferJobEventArgs(CloudTransferJob job)
        {
            Job = job ?? throw new ArgumentNullException(nameof(job));
        }

        public CloudTransferJob Job { get; }
    }

    public sealed class TransferRetryRefusedException : InvalidOperationException
    {
        public TransferRetryRefusedException(string message) : base(message) { }
    }

    public sealed class TransferQueueService : ITransferTerminalSource, IDisposable
    {
        private sealed class QueueRequest
        {
            public CloudTransferJob Job { get; set; }
            public bool IsRclone { get; set; }
            public PersonalCloudLibrarySourceSettings Settings { get; set; }
            public Func<bool> ExecutionAuthorized { get; set; }
            public bool TerminalPublished { get; set; }
            public bool CompletionClaimed { get; set; }
            public TaskCompletionSource<CloudTransferExecutionResult> Completion { get; set; }
        }

        private sealed class HostedRetryIntent
        {
            public Guid PreviousJobId { get; set; }
            public long CreatedTimestamp { get; set; }
        }

        private readonly Dictionary<Guid, HostedRetryIntent> hostedRetryIntents =
            new Dictionary<Guid, HostedRetryIntent>();
        private readonly object syncRoot = new object();
        private readonly CloudTransferManager manager;
        private readonly ICloudTransferExecutor executor;
        private readonly TransferRecoveryJournal recoveryJournal;
        private readonly List<QueueRequest> pending = new List<QueueRequest>();
        private readonly Dictionary<Guid, QueueRequest> requests = new Dictionary<Guid, QueueRequest>();
        private readonly Dictionary<Guid, CloudTransferJob> awaitingCompletion =
            new Dictionary<Guid, CloudTransferJob>();
        private const int WorkerCount = 5;
        private const int RetainedHistoryLimit = 512;
        private const int RetentionBatchSize = 32;
        private int completionsSincePrune;
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0, WorkerCount);
        private readonly Task[] workers;
        private bool stopping;
        private bool disposed;

        public TransferQueueService(CloudTransferManager manager, ICloudTransferExecutor executor,
            TransferRecoveryJournal recoveryJournal = null)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.recoveryJournal = recoveryJournal;
            workers = Enumerable.Range(0, WorkerCount)
                .Select(_ => Task.Run(WorkerLoopAsync))
                .ToArray();
        }

        public event EventHandler<CloudTransferJobEventArgs> JobTerminated;

        public CloudTransferJob EnqueueLocal(
            Guid gameId,
            string displayName,
            string source,
            string destination,
            string providerType,
            bool isDirectory, PersonalCloudLibrarySourceSettings settings = null,
            Func<bool> executionAuthorized = null)
        {
            return Enqueue(gameId, displayName, source, destination, providerType, isDirectory, false, settings, executionAuthorized);
        }

        public CloudTransferJob EnqueueRclone(
            Guid gameId,
            string displayName,
            string source,
            string destination,
            string providerType,
            bool isDirectory,
            PersonalCloudLibrarySourceSettings settings,
            Func<bool> executionAuthorized = null)
        {
            return Enqueue(gameId, displayName, source, destination, providerType, isDirectory, true, settings, executionAuthorized);
        }

        public Task<CloudTransferExecutionResult> GetCompletion(Guid jobId)
        {
            lock (syncRoot)
            {
                QueueRequest request;
                if (!requests.TryGetValue(jobId, out request))
                {
                    throw new KeyNotFoundException("Transfer request was not found: " + jobId);
                }

                // Once returned, the caller owns this task independently of
                // the bounded history lookup; an unclaimed result is protected.
                request.CompletionClaimed = true;
                return request.Completion.Task;
            }
        }

        public bool Cancel(Guid jobId)
        {
            lock (syncRoot)
            {
                if (stopping || disposed || !requests.ContainsKey(jobId)) return false;
                var accepted = manager.Cancel(jobId);
                if (accepted) WakeWorkersLocked();
                return accepted;
            }
        }

        public void SetMaxConcurrentTransfers(int value)
        {
            lock (syncRoot)
            {
                if (stopping || disposed) return;
                manager.SetMaxConcurrentTransfers(value);
                WakeWorkersLocked();
            }
        }

        // The Playnite host must create a fresh install controller so completion
        // and ownership receipts flow through the normal installation observer.
        public Guid PrepareHostedRetry(Guid failedJobId, PersonalCloudLibrarySourceSettings current)
        {
            lock (syncRoot)
            {
                ThrowIfStoppingLocked();
                QueueRequest previous;
                if (!requests.TryGetValue(failedJobId, out previous) || !previous.Job.CanRetry ||
                    previous.ExecutionAuthorized == null || previous.Settings == null)
                    throw new TransferRetryRefusedException("The selected attempt cannot be retried. Refresh the library.");
                if (!ReferenceEquals(manager.GetLatestRetryableJobForGame(previous.Job.GameId), previous.Job))
                    throw new TransferRetryRefusedException("Select the latest retryable attempt.");
                if (manager.GetActiveJobForGame(previous.Job.GameId) != null)
                    throw new TransferRetryRefusedException("A transfer attempt is already active for this game.");
                RefuseBlockedDestination(previous.Job.GameId, previous.Job.Destination);
                if (!MatchesRetryConfiguration(previous.Settings, current))
                    throw new TransferRetryRefusedException("Transfer source or cache settings changed. Refresh the library and start a new download.");
                hostedRetryIntents[previous.Job.GameId] = new HostedRetryIntent
                {
                    PreviousJobId = failedJobId,
                    CreatedTimestamp = Stopwatch.GetTimestamp()
                };
                return previous.Job.GameId;
            }
        }

        public void CancelHostedRetry(Guid gameId, Guid previousJobId)
        {
            lock (syncRoot)
            {
                HostedRetryIntent intent;
                if (hostedRetryIntents.TryGetValue(gameId, out intent) && intent.PreviousJobId == previousJobId)
                    hostedRetryIntents.Remove(gameId);
            }
        }

        public CloudTransferJob Retry(
            Guid failedJobId,
            PersonalCloudLibrarySourceSettings settingsSnapshot = null)
        {
            QueueRequest previous;
            lock (syncRoot)
            {
                ThrowIfStoppingLocked();
                if (!requests.TryGetValue(failedJobId, out previous))
                {
                    throw new TransferRetryRefusedException("Transfer history is no longer retained. Refresh the library.");
                }

                if (manager.GetActiveJobForGame(previous.Job.GameId) != null)
                {
                    throw new InvalidOperationException("A transfer attempt is already active for this game.");
                }

                RefuseBlockedDestination(previous.Job.GameId, previous.Job.Destination);
                if (previous.Job.CanRetry &&
                    !ReferenceEquals(manager.GetLatestRetryableJobForGame(previous.Job.GameId), previous.Job))
                    throw new TransferRetryRefusedException("Select the latest retryable attempt.");
                if (previous.Settings != null &&
                    !MatchesRetryConfiguration(previous.Settings, settingsSnapshot))
                    throw new TransferRetryRefusedException(
                        "Transfer source or cache settings changed. Refresh the library and start a new download.");
                var retry = manager.Retry(failedJobId);
                AddRequestLocked(retry, previous.IsRclone, settingsSnapshot ?? previous.Settings,
                    previous.ExecutionAuthorized);
                WakeWorkersLocked();
                return retry;
            }
        }

        public bool Shutdown(TimeSpan timeout)
        {
            lock (syncRoot)
            {
                if (!stopping)
                {
                    stopping = true;
                    foreach (var job in manager.GetReservationSnapshot().Where(job => !job.IsTerminal))
                    {
                        manager.Cancel(job.Id);
                    }

                    WakeWorkersLocked();
                }
            }

            try
            {
                return Task.WaitAll(workers, timeout);
            }
            catch (AggregateException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed) return;
                disposed = true;
            }

            if (Shutdown(TimeSpan.FromSeconds(5)))
            {
                lock (syncRoot) signal.Dispose();
            }
            else
            {
                // A timed-out worker still owns the semaphore. Dispose only
                // when every worker has exited, even if a worker faults.
                Task.WhenAll(workers).ContinueWith(completed =>
                {
                    var ignored = completed.Exception;
                    lock (syncRoot) signal.Dispose();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }

        private CloudTransferJob Enqueue(
            Guid gameId,
            string displayName,
            string source,
            string destination,
            string providerType,
            bool isDirectory,
            bool isRclone,
            PersonalCloudLibrarySourceSettings settings, Func<bool> executionAuthorized)
        {
            lock (syncRoot)
            {
                ThrowIfStoppingLocked();
                if (manager.GetActiveJobForGame(gameId) != null)
                {
                    throw new InvalidOperationException("A transfer attempt is already active for this game.");
                }

                RefuseBlockedDestination(gameId, destination);
                CloudTransferJob job;
                HostedRetryIntent intent;
                if (hostedRetryIntents.TryGetValue(gameId, out intent))
                {
                    hostedRetryIntents.Remove(gameId);
                    QueueRequest previous;
                    if ((Stopwatch.GetTimestamp() - intent.CreatedTimestamp) / (double)Stopwatch.Frequency > 120 ||
                        !requests.TryGetValue(intent.PreviousJobId, out previous) ||
                        !previous.Job.CanRetry ||
                        !ReferenceEquals(manager.GetLatestRetryableJobForGame(gameId), previous.Job) ||
                        executionAuthorized == null ||
                        previous.Settings == null || !MatchesRetryConfiguration(previous.Settings, settings) ||
                        previous.IsRclone != isRclone || previous.Job.IsDirectory != isDirectory ||
                        !Same(previous.Job.Source, source) || !Same(previous.Job.Destination, destination) ||
                        !Same(previous.Job.ProviderType, providerType))
                        throw new TransferRetryRefusedException(
                            "The retry route changed or expired. Refresh the library and start a new download.");
                    job = manager.Retry(previous.Job.Id);
                }
                else
                {
                    job = manager.Enqueue(gameId, displayName, source, destination, providerType, isDirectory);
                }
                AddRequestLocked(job, isRclone, settings, executionAuthorized);
                WakeWorkersLocked();
                return job;
            }
        }

        private void AddRequestLocked(
            CloudTransferJob job,
            bool isRclone,
            PersonalCloudLibrarySourceSettings settings, Func<bool> executionAuthorized)
        {
            var request = new QueueRequest
            {
                Job = job,
                IsRclone = isRclone,
                ExecutionAuthorized = executionAuthorized,
                Settings = settings == null ? null : SettingsMigrationService.CloneForEditing(settings),
                Completion = new TaskCompletionSource<CloudTransferExecutionResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously)
            };
            requests.Add(job.Id, request);
            awaitingCompletion.Add(job.Id, job);
            pending.Add(request);

            // The new request already owns a cloned settings snapshot and its
            // execution guard. Older retry contexts are no longer actionable:
            // the active attempt blocks retry until its terminal outcome, which
            // then supersedes the previous attempt. Preserve history and tasks.
            var previousJob = manager.GetLatestRetryableJobForGame(job.GameId);
            QueueRequest previousRequest;
            if (previousJob != null && previousJob.Id != job.Id &&
                requests.TryGetValue(previousJob.Id, out previousRequest))
            {
                previousRequest.Settings = null;
                previousRequest.ExecutionAuthorized = null;
            }
        }

        private async Task WorkerLoopAsync()
        {
            while (true)
            {
                await signal.WaitAsync().ConfigureAwait(false);
                QueueRequest request;
                lock (syncRoot)
                {
                    var readyIndex = pending.FindIndex(value =>
                        value.Job.State == CloudTransferState.Preparing ||
                        value.Job.CancellationToken.IsCancellationRequested);
                    if (readyIndex < 0)
                    {
                        if (stopping && pending.Count == 0)
                        {
                            return;
                        }

                        continue;
                    }

                    request = pending[readyIndex];
                    pending.RemoveAt(readyIndex);
                }

                ExecuteObserved(request);

                lock (syncRoot)
                {
                    WakeWorkersLocked();
                    if (stopping && pending.Count == 0) return;
                }
            }
        }

        // All producers hold syncRoot; consumers can only reduce the count.
        // Cap outstanding wakeups at the number of workers, not the number
        // of historical jobs or settings/progress notifications.
        private void WakeWorkersLocked()
        {
            var available = WorkerCount - signal.CurrentCount;
            if (available > 0) signal.Release(available);
        }

        private void ExecuteObserved(QueueRequest request)
        {
            CloudTransferExecutionResult result;
            var reservationWritten = false;
            try
            {
                if (request.Job.CancellationToken.IsCancellationRequested)
                {
                    result = CloudTransferExecutionResult.CancelledResult();
                }
                else if (!IsExecutionAuthorized(request))
                {
                    result = CloudTransferExecutionResult.Failure(
                        "Transfer authorization expired. Refresh the library and start a new download.");
                }
                else if (request.Job.CancellationToken.IsCancellationRequested)
                {
                    result = CloudTransferExecutionResult.CancelledResult();
                }
                else
                {
                    if (recoveryJournal != null)
                    {
                        recoveryJournal.Begin(request.Job);
                        reservationWritten = true;
                    }
                    result = request.IsRclone
                        ? executor.ExecuteRclone(request.Job.Id, request.Settings)
                        : executor.ExecuteLocal(request.Job.Id, request.Job.IsDirectory);
                    if (reservationWritten && !result.RequiresManualRecovery)
                    {
                        try { recoveryJournal.Complete(request.Job); }
                        catch (Exception)
                        {
                            // The adapter may already have published a successful
                            // install. Preserve that outcome, but quarantine future
                            // attempts until the retained reservation is reviewed.
                            result.RequiresManualRecovery = true;
                            manager.MarkRequiresManualRecovery(request.Job.Id);
                        }
                    }
                }

                if (result.RequiresManualRecovery) manager.MarkRequiresManualRecovery(request.Job.Id);
                EnsureTerminal(request.Job, result);
            }
            catch (Exception ex)
            {
                if (reservationWritten) manager.MarkRequiresManualRecovery(request.Job.Id);
                result = CloudTransferExecutionResult.Failure(ex.Message, ex,
                    requiresManualRecovery: reservationWritten);
                if (!request.Job.IsTerminal)
                {
                    manager.Transition(request.Job.Id, CloudTransferState.Failed, ex.Message);
                }
            }

            // Cancellation can win the race after an executor returns its result,
            // but before the queue commits the terminal state. Always report the
            // committed terminal outcome to installation observers.
            if (request.Job.State == CloudTransferState.Cancelled && !result.Cancelled)
                result = CloudTransferExecutionResult.CancelledResult();
            else if (request.Job.State == CloudTransferState.Failed && (result.Succeeded || result.Cancelled))
                result = CloudTransferExecutionResult.Failure(
                    string.IsNullOrWhiteSpace(request.Job.ErrorSummary)
                        ? "Transfer failed." : request.Job.ErrorSummary,
                    requiresManualRecovery: request.Job.RequiresManualRecovery);

            result.RequiresManualRecovery |= request.Job.RequiresManualRecovery;

            // Completed/quarantined attempts cannot be retried. Retain
            // their history and result, but release the configuration object.
            lock (syncRoot)
            {
                if (request.Job.State == CloudTransferState.Completed || request.Job.RequiresManualRecovery)
                {
                    request.Settings = null;
                    request.ExecutionAuthorized = null;
                }
            }
            PublishTerminalOnce(request);
            lock (syncRoot)
            {
                request.Completion.TrySetResult(result);
                awaitingCompletion.Remove(request.Job.Id);
                if (requests.Count > RetainedHistoryLimit &&
                    ++completionsSincePrune >= RetentionBatchSize)
                {
                    completionsSincePrune = 0;
                    TrimCompletedHistoryLocked(RetainedHistoryLimit);
                }
            }
        }

        // A soft cap: live, unclaimed, latest retryable and quarantined
        // attempts may exceed it. Never discard recovery or completion authority
        // to meet a numerical target. Prune in batches, not on every progress tick.
        private void TrimCompletedHistoryLocked(int keepCount)
        {
            var eligible = new HashSet<Guid>(requests.Values
                .Where(request => request.CompletionClaimed && request.Completion.Task.IsCompleted &&
                    request.TerminalPublished && !awaitingCompletion.ContainsKey(request.Job.Id))
                .Select(request => request.Job.Id));
            foreach (var id in manager.PruneTerminalHistory(eligible, keepCount))
                requests.Remove(id);
        }

        private static bool IsExecutionAuthorized(QueueRequest request)
        {
            try { return request.ExecutionAuthorized == null || request.ExecutionAuthorized(); }
            catch (Exception) { return false; }
        }

        private void EnsureTerminal(CloudTransferJob job, CloudTransferExecutionResult result)
        {
            if (job.IsTerminal)
            {
                return;
            }

            if (result.Cancelled)
            {
                manager.Transition(job.Id, CloudTransferState.Cancelled);
            }
            else if (!result.Succeeded)
            {
                manager.Transition(job.Id, CloudTransferState.Failed, result.Message);
            }
            else if (job.CancellationToken.IsCancellationRequested)
            {
                manager.Transition(job.Id, CloudTransferState.Cancelled);
            }
            else
            {
                manager.Transition(job.Id, CloudTransferState.Completed);
            }
        }

        private void PublishTerminalOnce(QueueRequest request)
        {
            var job = request.Job;
            EventHandler<CloudTransferJobEventArgs> handler = null;
            lock (syncRoot)
            {
                if (job.IsTerminal && !request.TerminalPublished)
                {
                    request.TerminalPublished = true;
                    handler = JobTerminated;
                }
            }

            if (handler == null)
            {
                return;
            }

            var args = new CloudTransferJobEventArgs(job);
            foreach (EventHandler<CloudTransferJobEventArgs> subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber(this, args);
                }
                catch
                {
                    // Event consumers cannot fault a queue-owned worker or strand completion.
                }
            }
        }

        private void RefuseBlockedDestination(Guid gameId, string destination)
        {
            // Do not scan completed history for each enqueue. Keep publication
            // reservations until the completion task resolves, even if the
            // manager already committed a terminal state.
            foreach (var job in manager.GetReservationSnapshot().Concat(awaitingCompletion.Values).Distinct())
            {
                var overlaps = job.GameId == gameId ||
                    PathBoundary.IsContained(job.Destination, destination) ||
                    PathBoundary.IsContained(destination, job.Destination);
                if (!overlaps) continue;
                if (job.RequiresManualRecovery)
                    throw new TransferRetryRefusedException(
                        "A prior transfer requires manual recovery. Stop the process and inspect preserved staging before any new attempt.");
                throw new TransferRetryRefusedException(
                    "Another transfer already reserves an overlapping cache destination.");
            }
        }

        // Old paths cannot be combined with new provider credentials or roots.
        // Scoped multi-source attempts need a fresh source-specific action,
        // not the globally selected settings from the dashboard.
        private static bool MatchesRetryConfiguration(PersonalCloudLibrarySourceSettings original,
            PersonalCloudLibrarySourceSettings current)
        {
            if (current == null || !current.Enabled || !current.AllowDownloads ||
                (current is PersonalCloudLibrarySourceSettingsV3 v3 && v3.EnableMultiSourceProfiles))
                return false;
            return Same(original.SourceProviderType, current.SourceProviderType) &&
                Same(original.LocalManifestPath, current.LocalManifestPath) &&
                Same(original.LocalLibraryRoot, current.LocalLibraryRoot) &&
                Same(original.ManifestRelativePath, current.ManifestRelativePath) &&
                Same(original.LocalCacheFolder, current.LocalCacheFolder) &&
                Same(original.RcloneExecutablePath, current.RcloneExecutablePath) &&
                Same(original.RcloneRemoteName, current.RcloneRemoteName) &&
                Same(original.RcloneManifestPath, current.RcloneManifestPath) &&
                Same(original.RcloneContentRoot, current.RcloneContentRoot);
        }

        private static bool Same(string a, string b) =>
            string.Equals(a, b, StringComparison.Ordinal);

        private void ThrowIfStoppingLocked()
        {
            if (stopping || disposed)
            {
                throw new InvalidOperationException("The transfer queue is shutting down and cannot accept new work.");
            }
        }
    }
}
