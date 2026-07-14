using NUnit.Framework;
using System;

namespace PersonalCloudLibrarySource.Tests.Dashboard
{
    [TestFixture]
    public class DashboardTransferActivityBridgeTests
    {
        [TestCase(CloudTransferState.Completed, DashboardActivityKind.TransferCompleted)]
        [TestCase(CloudTransferState.Failed, DashboardActivityKind.TransferFailed)]
        [TestCase(CloudTransferState.Cancelled, DashboardActivityKind.TransferCancelled)]
        public void TerminalEvent_AddsExactlyOneActivityRecord(
            CloudTransferState terminalState,
            DashboardActivityKind expectedKind)
        {
            var source = new TerminalSource();
            var activity = new DashboardActivityService();
            using (var bridge = new DashboardTransferActivityBridge(source, activity))
            {
                var job = CreateTerminalJob(terminalState);

                source.Publish(job);
                source.Publish(job);

                Assert.That(activity.Records, Has.Count.EqualTo(1));
                Assert.That(activity.Records[0].Kind, Is.EqualTo(expectedKind));
                Assert.That(activity.Records[0].GameId, Is.EqualTo(job.GameId));
            }
        }

        [Test]
        public void Dispose_UnsubscribesFromTerminalEvents()
        {
            var source = new TerminalSource();
            var activity = new DashboardActivityService();
            var bridge = new DashboardTransferActivityBridge(source, activity);

            bridge.Dispose();
            source.Publish(CreateTerminalJob(CloudTransferState.Completed));

            Assert.That(activity.Records, Is.Empty);
        }

        [Test]
        public void TerminalEventsArrivingOutOfOrder_ArePresentedNewestTimestampFirst()
        {
            var source = new TerminalSource();
            var activity = new DashboardActivityService();
            using (var bridge = new DashboardTransferActivityBridge(source, activity))
            {
                var older = CreateTerminalJob(CloudTransferState.Failed);
                var newer = CreateTerminalJob(CloudTransferState.Completed);
                SetCompletedAt(older, new DateTime(2026, 7, 13, 1, 0, 0, DateTimeKind.Utc));
                SetCompletedAt(newer, new DateTime(2026, 7, 13, 2, 0, 0, DateTimeKind.Utc));

                source.Publish(newer);
                source.Publish(older);

                Assert.That(activity.Records[0].GameId, Is.EqualTo(newer.GameId));
                Assert.That(activity.Records[1].GameId, Is.EqualTo(older.GameId));
            }
        }

        private static void SetCompletedAt(CloudTransferJob job, DateTime value)
        {
            var property = typeof(CloudTransferJob).GetProperty(nameof(CloudTransferJob.CompletedAt));
            Assert.That(property, Is.Not.Null);
            property.SetValue(job, (DateTime?)value, null);
        }

        private static CloudTransferJob CreateTerminalJob(CloudTransferState state)
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Example Game", "source", "destination", "LocalFolder");
            if (state == CloudTransferState.Completed)
            {
                manager.Transition(job.Id, CloudTransferState.Transferring);
                manager.Transition(job.Id, state);
            }
            else
            {
                manager.Transition(job.Id, state, state == CloudTransferState.Failed ? "Source unavailable" : null);
            }

            return job;
        }

        private sealed class TerminalSource : ITransferTerminalSource
        {
            public event EventHandler<CloudTransferJobEventArgs> JobTerminated;

            public void Publish(CloudTransferJob job)
            {
                JobTerminated?.Invoke(this, new CloudTransferJobEventArgs(job));
            }
        }
    }
}
