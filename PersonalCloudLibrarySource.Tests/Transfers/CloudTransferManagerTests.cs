using NUnit.Framework;
using System;
using System.Linq;
using System.Diagnostics;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class CloudTransferManagerTests
    {
        [Test]
        public void ChangedObserverFailureDoesNotInterruptCommittedState()
        {
            var manager = new CloudTransferManager(1);
            var observed = 0;
            manager.Changed += (sender, args) => throw new InvalidOperationException("observer");
            manager.Changed += (sender, args) => observed++;
            var job = manager.Enqueue(Guid.NewGuid(), "Game", "source", "dest", "LocalFolder");
            manager.Transition(job.Id, CloudTransferState.Transferring);
            Assert.That(job.State, Is.EqualTo(CloudTransferState.Transferring));
            Assert.That(observed, Is.EqualTo(2));
        }

        [Test]
        public void Enqueue_StartsOnlyConfiguredNumberOfJobs()
        {
            var manager = new CloudTransferManager(2);

            var first = manager.Enqueue(Guid.NewGuid(), "First", "source-1", "dest-1", "LocalFolder");
            var second = manager.Enqueue(Guid.NewGuid(), "Second", "source-2", "dest-2", "LocalFolder");
            var third = manager.Enqueue(Guid.NewGuid(), "Third", "source-3", "dest-3", "LocalFolder");

            Assert.That(first.State, Is.EqualTo(CloudTransferState.Preparing));
            Assert.That(second.State, Is.EqualTo(CloudTransferState.Preparing));
            Assert.That(third.State, Is.EqualTo(CloudTransferState.Queued));
            Assert.That(manager.ActiveCount, Is.EqualTo(2));
            Assert.That(manager.QueuedCount, Is.EqualTo(1));
        }

        [Test]
        public void Complete_ActiveJob_StartsNextQueuedJob()
        {
            var manager = new CloudTransferManager(1);
            var first = manager.Enqueue(Guid.NewGuid(), "First", "source-1", "dest-1", "LocalFolder");
            var second = manager.Enqueue(Guid.NewGuid(), "Second", "source-2", "dest-2", "LocalFolder");

            manager.Transition(first.Id, CloudTransferState.Transferring);
            manager.Transition(first.Id, CloudTransferState.Completed);

            Assert.That(first.State, Is.EqualTo(CloudTransferState.Completed));
            Assert.That(second.State, Is.EqualTo(CloudTransferState.Preparing));
            Assert.That(manager.ActiveCount, Is.EqualTo(1));
            Assert.That(manager.QueuedCount, Is.EqualTo(0));
        }

        [Test]
        public void Transition_TerminalJob_RejectsFurtherChanges()
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", "source", "dest", "LocalFolder");
            manager.Transition(job.Id, CloudTransferState.Cancelled);

            Assert.Throws<InvalidOperationException>(() =>
                manager.Transition(job.Id, CloudTransferState.Transferring));
        }

        [Test]
        public void TransitionToFinalizing_WhenCancellationWonRace_RejectsCommitAtomically()
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", "source", "dest", "LocalFolder");
            manager.Transition(job.Id, CloudTransferState.Transferring);
            manager.Transition(job.Id, CloudTransferState.Verifying);
            Assert.That(manager.Cancel(job.Id), Is.True);

            Assert.Throws<OperationCanceledException>(() =>
                manager.Transition(job.Id, CloudTransferState.Finalizing));
            Assert.That(job.State, Is.EqualTo(CloudTransferState.Verifying));
        }

        [Test]
        public void LifecycleRevision_ChangesForTransitionsButNotByteProgress()
        {
            var manager = new CloudTransferManager(1);
            var initial = manager.LifecycleRevision;
            var job = manager.Enqueue(Guid.NewGuid(), "Game", "source", "dest", "LocalFolder");
            Assert.That(manager.LifecycleRevision, Is.GreaterThan(initial));
            manager.Transition(job.Id, CloudTransferState.Transferring);
            var transferring = manager.LifecycleRevision;
            manager.UpdateProgress(job.Id, 1, 100);
            manager.UpdateProgress(job.Id, 2, 100);
            Assert.That(manager.LifecycleRevision, Is.EqualTo(transferring));
            manager.Transition(job.Id, CloudTransferState.Completed);
            Assert.That(manager.LifecycleRevision, Is.GreaterThan(transferring));
        }

        [Test]
        public void UpdateProgress_AggregatesKnownByteTotals()
        {
            var manager = new CloudTransferManager(2);
            var first = manager.Enqueue(Guid.NewGuid(), "First", "source-1", "dest-1", "LocalFolder");
            var second = manager.Enqueue(Guid.NewGuid(), "Second", "source-2", "dest-2", "LocalFolder");

            manager.Transition(first.Id, CloudTransferState.Transferring);
            manager.Transition(second.Id, CloudTransferState.Transferring);
            manager.UpdateProgress(first.Id, 50, 100);
            manager.UpdateProgress(second.Id, 100, 300);

            var progress = manager.GetAggregateProgress();

            Assert.That(progress.IsIndeterminate, Is.False);
            Assert.That(progress.BytesTransferred, Is.EqualTo(150));
            Assert.That(progress.TotalBytes, Is.EqualTo(400));
            Assert.That(progress.Percentage, Is.EqualTo(37.5).Within(0.001));
        }

        [Test]
        public void AggregateProgress_ProviderTotalsOverflowBecomeIndeterminate()
        {
            var manager = new CloudTransferManager(2);
            var first = manager.Enqueue(Guid.NewGuid(), "First", "source-1", "dest-1", "LocalFolder");
            var second = manager.Enqueue(Guid.NewGuid(), "Second", "source-2", "dest-2", "LocalFolder");
            manager.Transition(first.Id, CloudTransferState.Transferring);
            manager.Transition(second.Id, CloudTransferState.Transferring);
            manager.UpdateProgress(first.Id, long.MaxValue, long.MaxValue);
            manager.UpdateProgress(second.Id, 1, 1);
            var aggregate = manager.GetAggregateProgress();
            Assert.That(aggregate.IsIndeterminate, Is.True);
            Assert.That(aggregate.TotalBytes, Is.Zero);
        }

        [Test]
        public void IndexedLookup_PreservesHistoryAndRetryIdentity()
        {
            var manager = new CloudTransferManager(1);
            var game = Guid.NewGuid();
            var first = manager.Enqueue(game, "First", "source", "dest", "LocalFolder");
            manager.Transition(first.Id, CloudTransferState.Failed);
            var retry = manager.Retry(first.Id);
            for (var index = 0; index < 100; index++)
            {
                var other = manager.Enqueue(Guid.NewGuid(), "Other", "source", "dest", "LocalFolder");
                manager.Transition(other.Id, CloudTransferState.Cancelled);
            }
            Assert.That(manager.GetJob(first.Id), Is.SameAs(first));
            Assert.That(manager.GetActiveJobForGame(game), Is.SameAs(retry));
            Assert.That(manager.GetLatestRetryableJobForGame(game), Is.SameAs(first));
            Assert.That(manager.Jobs.Count, Is.EqualTo(102));
        }

        [Test]
        public void LiveIndexesMatchHistoryAcrossMixedTransitions()
        {
            var manager = new CloudTransferManager(2);
            var first = manager.Enqueue(Guid.NewGuid(), "First", "s", "d", "LocalFolder");
            var second = manager.Enqueue(Guid.NewGuid(), "Second", "s", "d", "LocalFolder");
            var third = manager.Enqueue(Guid.NewGuid(), "Third", "s", "d", "LocalFolder");
            var fourth = manager.Enqueue(Guid.NewGuid(), "Fourth", "s", "d", "LocalFolder");
            manager.Transition(third.Id, CloudTransferState.Cancelled);
            manager.Transition(first.Id, CloudTransferState.Failed);
            manager.SetMaxConcurrentTransfers(1);
            manager.Transition(second.Id, CloudTransferState.Transferring);
            manager.Transition(second.Id, CloudTransferState.Completed);
            manager.Transition(fourth.Id, CloudTransferState.Transferring);
            manager.UpdateProgress(fourth.Id, 5, 10);
            var history = manager.Jobs;
            Assert.That(manager.ActiveCount, Is.EqualTo(history.Count(job => job.IsActive)));
            Assert.That(manager.QueuedCount, Is.EqualTo(history.Count(job => job.State == CloudTransferState.Queued)));
            Assert.That(manager.FailedCount, Is.EqualTo(history.Count(job => job.State == CloudTransferState.Failed)));
            Assert.That(manager.GetAggregateProgress().Percentage, Is.EqualTo(50));
            Assert.That(history.Count, Is.EqualTo(4));
        }

        [Test]
        public void ReservationIndexBenchmark_5000HistoricalJobs()
        {
            const int historySize = 5000;
            const int iterations = 2000;
            var manager = new CloudTransferManager(4);
            for (var i = 0; i < historySize; i++)
            {
                var old = manager.Enqueue(Guid.NewGuid(), "History", "s", "d", "LocalFolder");
                manager.Transition(old.Id, CloudTransferState.Cancelled);
            }
            for (var i = 0; i < 6; i++)
                manager.Enqueue(Guid.NewGuid(), "Live", "s", "d", "LocalFolder");
            var history = manager.Jobs;
            Func<int> scan = () => history.Count(job => !job.IsTerminal || job.RequiresManualRecovery);
            Assert.That(manager.GetReservationSnapshot().Count, Is.EqualTo(6));
            var referenceTimer = Stopwatch.StartNew();
            long reference = 0;
            for (var i = 0; i < iterations; i++) reference += scan();
            referenceTimer.Stop();
            var indexedTimer = Stopwatch.StartNew();
            long indexed = 0;
            for (var i = 0; i < iterations; i++) indexed += manager.GetReservationSnapshot().Count;
            indexedTimer.Stop();
            Assert.That(indexed, Is.EqualTo(reference));
            Assert.That(manager.Jobs.Count, Is.EqualTo(historySize + 6));
            Console.WriteLine("PCLS_BENCHMARK reservations history={0} iterations={1} scan_ms={2:F3} indexed_ms={3:F3}",
                historySize, iterations, referenceTimer.Elapsed.TotalMilliseconds, indexedTimer.Elapsed.TotalMilliseconds);
        }

        [Test]
        public void LiveProgressBenchmark_5000HistoricalJobs()
        {
            const int historySize = 5000;
            const int iterations = 2000;
            var manager = new CloudTransferManager(4);
            for (var i = 0; i < historySize; i++)
            {
                var job = manager.Enqueue(Guid.NewGuid(), "History", "s", "d", "LocalFolder");
                manager.Transition(job.Id, CloudTransferState.Cancelled);
            }
            for (var i = 0; i < 4; i++)
            {
                var job = manager.Enqueue(Guid.NewGuid(), "Active", "s", "d", "LocalFolder");
                manager.Transition(job.Id, CloudTransferState.Transferring);
                manager.UpdateProgress(job.Id, 50, 100);
            }

            var snapshot = manager.Jobs;
            // Same input and byte totals, a conservative reference scan of the
            // history without the old manager's lock or temporary list allocation.
            Func<long> scan = () =>
            {
                long bytes = 0;
                foreach (var job in snapshot)
                    if (job.IsActive) bytes += job.BytesTransferred;
                return bytes;
            };
            Assert.That(scan(), Is.EqualTo(200));
            Assert.That(manager.GetAggregateProgress().BytesTransferred, Is.EqualTo(200));
            var scanTimer = Stopwatch.StartNew();
            long baseline = 0;
            for (var i = 0; i < iterations; i++) baseline += scan();
            scanTimer.Stop();
            var indexedTimer = Stopwatch.StartNew();
            long indexed = 0;
            for (var i = 0; i < iterations; i++)
                indexed += manager.GetAggregateProgress().BytesTransferred;
            indexedTimer.Stop();
            Assert.That(indexed, Is.EqualTo(baseline));
            Assert.That(manager.Jobs.Count, Is.EqualTo(historySize + 4));
            Console.WriteLine("PCLS_BENCHMARK live_progress history={0} iterations={1} scan_ms={2:F3} indexed_ms={3:F3}",
                historySize, iterations, scanTimer.Elapsed.TotalMilliseconds, indexedTimer.Elapsed.TotalMilliseconds);
            // Timings are evidence, not a brittle wall-clock pass/fail threshold.
        }

        [Test]
        public void AggregateProgress_UnknownActiveTotal_IsIndeterminate()
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", "source", "dest", "RcloneRemote");
            manager.Transition(job.Id, CloudTransferState.Transferring);
            manager.UpdateProgress(job.Id, 1024, null);

            var progress = manager.GetAggregateProgress();

            Assert.That(progress.IsIndeterminate, Is.True);
            Assert.That(progress.ActiveJobCount, Is.EqualTo(1));
        }

        [Test]
        public void Retry_CreatesNewQueuedAttemptLinkedToFailedJobAndPreservesDirectoryKind()
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", "source", "dest", "LocalFolder", true);
            manager.Transition(job.Id, CloudTransferState.Failed, "Network unavailable");

            var retry = manager.Retry(job.Id);

            Assert.That(retry.PreviousAttemptId, Is.EqualTo(job.Id));
            Assert.That(retry.GameId, Is.EqualTo(job.GameId));
            Assert.That(retry.IsDirectory, Is.True);
            Assert.That(retry.State, Is.EqualTo(CloudTransferState.Preparing));
            Assert.That(manager.Jobs.Count(value => value.GameId == job.GameId), Is.EqualTo(2));
        }

        [Test]
        public void GetActiveJobForGame_ReturnsNewestNonTerminalAttempt()
        {
            var manager = new CloudTransferManager(2);
            var gameId = Guid.NewGuid();
            var first = manager.Enqueue(gameId, "First", "source-1", "dest-1", "LocalFolder");
            var second = manager.Enqueue(gameId, "Second", "source-2", "dest-2", "LocalFolder");
            manager.Transition(first.Id, CloudTransferState.Cancelled);

            var active = manager.GetActiveJobForGame(gameId);

            Assert.That(active, Is.Not.Null);
            Assert.That(active.Id, Is.EqualTo(second.Id));
        }

        [Test]
        public void GetLatestRetryableJobForGame_ReturnsNewestFailedOrCancelledAttempt()
        {
            var manager = new CloudTransferManager(1);
            var gameId = Guid.NewGuid();
            var failed = manager.Enqueue(gameId, "Failed", "source-1", "dest-1", "LocalFolder");
            manager.Transition(failed.Id, CloudTransferState.Failed, "failed");
            var cancelled = manager.Enqueue(gameId, "Cancelled", "source-2", "dest-2", "LocalFolder");
            manager.Cancel(cancelled.Id);
            Assert.That(cancelled.CancellationToken.IsCancellationRequested, Is.True);
            Assert.That(cancelled.State, Is.Not.EqualTo(CloudTransferState.Cancelled),
                "the observed worker owns terminal transition after cleanup");
            manager.Transition(cancelled.Id, CloudTransferState.Cancelled);

            var retryable = manager.GetLatestRetryableJobForGame(gameId);

            Assert.That(retryable, Is.Not.Null);
            Assert.That(retryable.Id, Is.EqualTo(cancelled.Id));
        }

        [Test]
        public void RetryLookupUsesRegistrationOrderInsteadOfCompletionTime()
        {
            var manager = new CloudTransferManager(2);
            var gameId = Guid.NewGuid();
            var older = manager.Enqueue(gameId, "Older", "source", "dest", "LocalFolder");
            var newer = manager.Enqueue(gameId, "Newer", "source", "dest", "LocalFolder");
            manager.Transition(newer.Id, CloudTransferState.Failed);
            Assert.Throws<InvalidOperationException>(() => manager.Retry(newer.Id));
            manager.Transition(older.Id, CloudTransferState.Transferring);
            manager.Transition(older.Id, CloudTransferState.Completed);
            Assert.That(manager.GetLatestRetryableJobForGame(gameId), Is.SameAs(newer));
            Assert.That(manager.Retry(newer.Id).PreviousAttemptId, Is.EqualTo(newer.Id));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LatestTerminalAttemptSupersedesHistoricalFailure(bool quarantine)
        {
            var manager = new CloudTransferManager(1);
            var gameId = Guid.NewGuid();
            var failed = manager.Enqueue(gameId, "First", "source", "dest", "LocalFolder");
            manager.Transition(failed.Id, CloudTransferState.Failed);
            var next = manager.Retry(failed.Id);
            manager.Transition(next.Id, CloudTransferState.Transferring);
            manager.Transition(next.Id, quarantine ? CloudTransferState.Failed : CloudTransferState.Completed);
            if (quarantine)
            {
                var mark = typeof(CloudTransferManager).GetMethod("MarkRequiresManualRecovery",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(mark, Is.Not.Null);
                mark.Invoke(manager, new object[] { next.Id });
            }

            Assert.That(manager.GetLatestRetryableJobForGame(gameId), Is.Null);
            Assert.Throws<InvalidOperationException>(() => manager.Retry(failed.Id));
            Assert.That(manager.GetJob(failed.Id), Is.SameAs(failed));
            Assert.That(manager.Jobs.Count, Is.EqualTo(2));
        }

        [TestCase(0)]
        [TestCase(5)]
        public void Constructor_InvalidConcurrency_UsesOne(int invalidConcurrency)
        {
            var manager = new CloudTransferManager(invalidConcurrency);

            Assert.That(manager.MaxConcurrentTransfers, Is.EqualTo(1));
        }
    }
}
