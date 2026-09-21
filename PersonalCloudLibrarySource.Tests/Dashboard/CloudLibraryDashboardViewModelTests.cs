using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Dashboard
{
    [TestFixture]
    public class CloudLibraryDashboardViewModelTests
    {
        [Test]
        public void ManagerChanges_AreCoalescedAndLatestQueueSnapshotIsPublishedOnDispatcher()
        {
            var manager = new CloudTransferManager(1);
            var dispatcher = new QueuedDispatcher();
            var viewModel = CreateViewModel(manager, new DashboardActivityService(), dispatcher, new RecordingActions());
            dispatcher.ExecuteAll();
            dispatcher.ResetSubmissionCount();

            var job = manager.Enqueue(Guid.NewGuid(), "Example Game", "source", "destination", "LocalFolder");
            manager.Transition(job.Id, CloudTransferState.Transferring);
            manager.UpdateProgress(job.Id, 25, 100);
            manager.UpdateProgress(job.Id, 75, 100);

            Assert.That(dispatcher.SubmissionCount, Is.EqualTo(1));
            Assert.That(viewModel.TransferQueueItems, Is.Empty);

            dispatcher.ExecuteAll();

            Assert.That(viewModel.TransferQueueItems, Has.Count.EqualTo(1));
            Assert.That(viewModel.TransferQueueItems[0].ProgressText, Does.Contain("75%"));
            viewModel.Dispose();
        }

        [Test]
        public void ActivityChange_IsPostedImmediatelyAndUsesNewestFirstCappedHistory()
        {
            var activity = new DashboardActivityService();
            var dispatcher = new QueuedDispatcher();
            var viewModel = CreateViewModel(new CloudTransferManager(1), activity, dispatcher, new RecordingActions());
            dispatcher.ExecuteAll();
            dispatcher.ResetSubmissionCount();

            for (var index = 0; index < 55; index++)
            {
                activity.Add(DashboardActivityKind.Library, "Event " + index);
            }

            Assert.That(dispatcher.SubmissionCount, Is.EqualTo(55));
            dispatcher.ExecuteAll();
            Assert.That(viewModel.RecentActivity, Has.Count.EqualTo(50));
            Assert.That(viewModel.RecentActivity.First().Message, Is.EqualTo("Event 54"));
            viewModel.Dispose();
        }

        [Test]
        public void QueueItemCommands_UseTransferQueueActions()
        {
            var manager = new CloudTransferManager(1);
            var dispatcher = new QueuedDispatcher();
            var actions = new RecordingActions();
            var viewModel = CreateViewModel(manager, new DashboardActivityService(), dispatcher, actions);
            var active = manager.Enqueue(Guid.NewGuid(), "Active", "source", "destination", "LocalFolder");
            var failed = manager.Enqueue(Guid.NewGuid(), "Failed", "source", "destination", "LocalFolder");
            manager.Transition(active.Id, CloudTransferState.Failed, "failed");
            manager.Transition(failed.Id, CloudTransferState.Failed, "failed");
            var cancellable = manager.Enqueue(Guid.NewGuid(), "Cancellable", "source", "destination", "LocalFolder");
            dispatcher.ExecuteAll();

            var activeItem = viewModel.TransferQueueItems.Single(item => item.Job.Id == failed.Id);
            var retryItem = viewModel.TransferQueueItems.Single(item => item.Job.Id == active.Id);
            var cancelItem = viewModel.TransferQueueItems.Single(item => item.Job.Id == cancellable.Id);
            cancelItem.CancelCommand.Execute(null);
            activeItem.RetryCommand.Execute(null);
            retryItem.RetryCommand.Execute(null);

            Assert.That(actions.Cancelled, Is.EqualTo(new[] { cancellable.Id }));
            Assert.That(actions.Retried, Is.EquivalentTo(new[] { failed.Id, active.Id }));
            viewModel.Dispose();
        }

        [Test]
        public void Dispose_StopsUpdatesAndInvalidatesAlreadyPostedCallbacks_ReopenGetsOneUpdate()
        {
            var manager = new CloudTransferManager(1);
            var activity = new DashboardActivityService();
            var dispatcher = new QueuedDispatcher(ignoreCancellation: true);
            var first = CreateViewModel(manager, activity, dispatcher, new RecordingActions());
            dispatcher.ExecuteAll();
            var firstChanges = 0;
            first.PropertyChanged += (sender, args) => firstChanges++;

            manager.Enqueue(Guid.NewGuid(), "Before dispose", "source", "destination", "LocalFolder");
            first.Dispose();
            dispatcher.ExecuteAll();
            activity.Add(DashboardActivityKind.Library, "After dispose");

            Assert.That(firstChanges, Is.EqualTo(0));
            Assert.That(dispatcher.PendingCount, Is.EqualTo(0));

            var second = CreateViewModel(manager, activity, dispatcher, new RecordingActions());
            dispatcher.ExecuteAll();
            var secondQueueChanges = 0;
            second.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(CloudLibraryDashboardViewModel.TransferQueueItems))
                {
                    secondQueueChanges++;
                }
            };

            manager.SetMaxConcurrentTransfers(2);
            dispatcher.ExecuteAll();

            Assert.That(firstChanges, Is.EqualTo(0));
            Assert.That(secondQueueChanges, Is.EqualTo(1));
            second.Dispose();
        }

        [Test]
        public void DispatcherDropsManagerRefresh_ReleasesPendingClaimSoNextChangeCanPost()
        {
            var manager = new CloudTransferManager(1);
            var dispatcher = new DroppingDispatcher();
            var viewModel = CreateViewModel(manager, new DashboardActivityService(), dispatcher, new RecordingActions());
            dispatcher.ResetSubmissionCount();

            var job = manager.Enqueue(Guid.NewGuid(), "Example", "source", "destination", "LocalFolder");
            manager.Transition(job.Id, CloudTransferState.Transferring);

            Assert.That(dispatcher.SubmissionCount, Is.EqualTo(2));
            viewModel.Dispose();
        }

        private static CloudLibraryDashboardViewModel CreateViewModel(
            CloudTransferManager manager,
            DashboardActivityService activity,
            IStartupUiDispatcher dispatcher,
            IDashboardTransferActions actions)
        {
            return new CloudLibraryDashboardViewModel(
                new DashboardStateStore(),
                EmptyNavigation(),
                manager,
                activity,
                actions,
                dispatcher);
        }

        private static PluginNavigationService EmptyNavigation()
        {
            Action none = () => { };
            return new PluginNavigationService(none, none, none, none, none, none, none, none, none);
        }

        private sealed class RecordingActions : IDashboardTransferActions
        {
            public List<Guid> Cancelled { get; } = new List<Guid>();
            public List<Guid> Retried { get; } = new List<Guid>();
            public void Cancel(Guid jobId) => Cancelled.Add(jobId);
            public void Retry(Guid jobId) => Retried.Add(jobId);
        }

        private sealed class QueuedDispatcher : IStartupUiDispatcher
        {
            private readonly Queue<Tuple<Action, CancellationToken>> callbacks =
                new Queue<Tuple<Action, CancellationToken>>();
            private readonly bool ignoreCancellation;

            public QueuedDispatcher(bool ignoreCancellation = false)
            {
                this.ignoreCancellation = ignoreCancellation;
            }

            public int PendingCount => callbacks.Count;
            public int SubmissionCount { get; private set; }

            public void Post(Action action, CancellationToken cancellationToken)
            {
                SubmissionCount++;
                callbacks.Enqueue(Tuple.Create(action, cancellationToken));
            }

            public void ExecuteAll()
            {
                while (callbacks.Count > 0)
                {
                    var callback = callbacks.Dequeue();
                    if (ignoreCancellation || !callback.Item2.IsCancellationRequested)
                    {
                        callback.Item1();
                    }
                }
            }

            public void ResetSubmissionCount() => SubmissionCount = 0;
        }

        private sealed class DroppingDispatcher : IAcknowledgingStartupUiDispatcher
        {
            public int SubmissionCount { get; private set; }

            public void Post(Action action, CancellationToken cancellationToken)
            {
                TryPost(action, cancellationToken);
            }

            public bool TryPost(Action action, CancellationToken cancellationToken)
            {
                SubmissionCount++;
                return false;
            }

            public void ResetSubmissionCount() => SubmissionCount = 0;
        }
    }
}
