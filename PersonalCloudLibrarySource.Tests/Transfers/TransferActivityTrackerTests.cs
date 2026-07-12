using NUnit.Framework;
using System;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class TransferActivityTrackerTests
    {
        [Test]
        public void CollectNew_ActiveJob_ProducesNoActivity()
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", "source", "destination", "LocalFolder");
            var tracker = new TransferActivityTracker();

            var records = tracker.CollectNew(manager.Jobs);

            Assert.That(records, Is.Empty);
        }

        [Test]
        public void CollectNew_CompletedJob_ProducesOneRecordOnlyOnce()
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", "source", "destination", "LocalFolder");
            manager.Transition(job.Id, CloudTransferState.Transferring);
            manager.Transition(job.Id, CloudTransferState.Completed);
            var tracker = new TransferActivityTracker();

            var first = tracker.CollectNew(manager.Jobs);
            var second = tracker.CollectNew(manager.Jobs);

            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(first[0].Kind, Is.EqualTo(DashboardActivityKind.TransferCompleted));
            Assert.That(first[0].Message, Is.EqualTo("Game is ready to play."));
            Assert.That(first[0].GameId, Is.EqualTo(job.GameId));
            Assert.That(second, Is.Empty);
        }

        [Test]
        public void CollectNew_FailedJob_IncludesFriendlyErrorSummary()
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", "source", "destination", "RcloneRemote");
            manager.Transition(job.Id, CloudTransferState.Failed, "Cloud source did not respond.");
            var tracker = new TransferActivityTracker();

            var record = tracker.CollectNew(manager.Jobs)[0];

            Assert.That(record.Kind, Is.EqualTo(DashboardActivityKind.TransferFailed));
            Assert.That(record.Message, Is.EqualTo("Game failed: Cloud source did not respond."));
        }

        [Test]
        public void CollectNew_CancelledJob_ProducesCancellationRecord()
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", "source", "destination", "LocalFolder");
            manager.Cancel(job.Id);
            var tracker = new TransferActivityTracker();

            var record = tracker.CollectNew(manager.Jobs)[0];

            Assert.That(record.Kind, Is.EqualTo(DashboardActivityKind.TransferCancelled));
            Assert.That(record.Message, Is.EqualTo("Game transfer was cancelled."));
        }
    }
}
