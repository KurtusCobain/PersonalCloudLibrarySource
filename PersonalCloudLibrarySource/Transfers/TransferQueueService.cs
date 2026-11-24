using System;
using System.Collections.Generic;
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
            public TaskCompletionSource<CloudTransferExecutionResult> Completion { get; set; }
        }

        private readonly object syncRoot = new object();
        private readonly CloudTransferManager manager;
        private readonly ICloudTransferExecutor executor;
        private readonly TransferRecoveryJournal recoveryJournal;
        private readonly List<QueueRequest> pending = new List<QueueRequest>();
        private readonly Dictionary<Guid, QueueRequest> requests = new Dictionary<Guid, QueueRequest>();
        private readonly HashSet<Guid> publishedTerminalJobs = new HashSet<Guid>();
        private readonly SemaphoreSlim signal = new SemaphoreSlim(0);
        private readonly Task[] workers;
        private bool stopping;
        private bool disposed;

        public TransferQueueService(CloudTransferManager manager, ICloudTransferExecutor executor,
            TransferRecoveryJournal recoveryJournal = null)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.recoveryJournal = recoveryJournal;
            workers = Enumerable.Range(0, 5)
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
            bool isDirectory, PersonalCloudLibrarySourceSettings settings = null)
        {
            return Enqueue(gameId, displayName, source, destination, providerType, isDirectory, false, settings);
        }

        public CloudTransferJob EnqueueRclone(
            Guid gameId,
            string displayName,
            string source,
            string destination,
            string providerType,
            bool isDirectory,
            PersonalCloudLibrarySourceSettings settings)
        {
            return Enqueue(gameId, displayName, source, destination, providerType, isDirectory, true, settings);
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

                return request.Completion.Task;
            }
        }

        public bool Cancel(Guid jobId)
        {
            var accepted = manager.Cancel(jobId);
            if (accepted)
            {
                signal.Release();
            }

            return accepted;
        }

        public void SetMaxConcurrentTransfers(int value)
        {
            manager.SetMaxConcurrentTransfers(value);
            signal.Release(workers.Length);
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
                    throw new KeyNotFoundException("Transfer request was not found: " + failedJobId);
                }

                if (manager.GetActiveJobForGame(previous.Job.GameId) != null)
                {
                    throw new InvalidOperationException("A transfer attempt is already active for this game.");
                }

                RefuseBlockedDestination(previous.Job.GameId, previous.Job.Destination);
                if (previous.Settings != null &&
                    !MatchesRetryConfiguration(previous.Settings, settingsSnapshot))
                    throw new TransferRetryRefusedException(
                        "Transfer source or cache settings changed. Refresh the library and start a new download.");
                var retry = manager.Retry(failedJobId);
                AddRequestLocked(retry, previous.IsRclone, settingsSnapshot ?? previous.Settings);
                signal.Release();
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
                    foreach (var job in manager.Jobs.Where(job => !job.IsTerminal))
                    {
                        manager.Cancel(job.Id);
                    }

                    signal.Release(workers.Length);
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
            if (disposed)
            {
                return;
            }

            disposed = true;
            if (Shutdown(TimeSpan.FromSeconds(5)))
            {
                signal.Dispose();
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
            PersonalCloudLibrarySourceSettings settings)
        {
            lock (syncRoot)
            {
                ThrowIfStoppingLocked();
                if (manager.GetActiveJobForGame(gameId) != null)
                {
                    throw new InvalidOperationException("A transfer attempt is already active for this game.");
                }

                RefuseBlockedDestination(gameId, destination);
                var job = manager.Enqueue(gameId, displayName, source, destination, providerType, isDirectory);
                AddRequestLocked(job, isRclone, settings);
                signal.Release();
                return job;
            }
        }

        private void AddRequestLocked(
            CloudTransferJob job,
            bool isRclone,
            PersonalCloudLibrarySourceSettings settings)
        {
            var request = new QueueRequest
            {
                Job = job,
                IsRclone = isRclone,
                Settings = settings == null ? null : SettingsMigrationService.CloneForEditing(settings),
                Completion = new TaskCompletionSource<CloudTransferExecutionResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously)
            };
            requests.Add(job.Id, request);
            pending.Add(request);
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
                signal.Release(workers.Length);

                lock (syncRoot)
                {
                    if (stopping && pending.Count == 0)
                    {
                        signal.Release(workers.Length);
                        return;
                    }
                }
            }
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
                            request.Job.RequiresManualRecovery = true;
                        }
                    }
                }

                if (result.RequiresManualRecovery) request.Job.RequiresManualRecovery = true;
                EnsureTerminal(request.Job, result);
            }
            catch (Exception ex)
            {
                if (reservationWritten) request.Job.RequiresManualRecovery = true;
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

            // Completed/quarantined attempts cannot be retried. Retain
            // their history and result, but release the configuration object.
            lock (syncRoot)
            {
                if (request.Job.State == CloudTransferState.Completed || request.Job.RequiresManualRecovery)
                    request.Settings = null;
            }
            PublishTerminalOnce(request.Job);
            request.Completion.TrySetResult(result);
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

        private void PublishTerminalOnce(CloudTransferJob job)
        {
            EventHandler<CloudTransferJobEventArgs> handler = null;
            lock (syncRoot)
            {
                if (job.IsTerminal && publishedTerminalJobs.Add(job.Id))
                {
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
            foreach (var job in manager.Jobs)
            {
                if (job.IsTerminal && !job.RequiresManualRecovery) continue;
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
