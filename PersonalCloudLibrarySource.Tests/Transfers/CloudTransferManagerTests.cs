using NUnit.Framework;
using System;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class CloudTransferManagerTests
    {
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

        [TestCase(0)]
        [TestCase(5)]
        public void Constructor_InvalidConcurrency_UsesOne(int invalidConcurrency)
        {
            var manager = new CloudTransferManager(invalidConcurrency);

            Assert.That(manager.MaxConcurrentTransfers, Is.EqualTo(1));
        }
    }
}
