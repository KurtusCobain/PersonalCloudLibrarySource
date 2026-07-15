using NUnit.Framework;
using System;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class CloudTransferQueueItemViewModelTests
    {
        [Test]
        public void ActiveJob_ShowsProgressAndInvokesCancel()
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Example Game", "source", "destination", "LocalFolder");
            manager.Transition(job.Id, CloudTransferState.Transferring);
            manager.UpdateProgress(job.Id, 50, 100);
            var cancelCalls = 0;
            var retryCalls = 0;

            var viewModel = new CloudTransferQueueItemViewModel(
                job,
                () => cancelCalls++,
                () => retryCalls++);

            Assert.That(viewModel.DisplayName, Is.EqualTo("Example Game"));
            Assert.That(viewModel.StateText, Is.EqualTo("Transferring"));
            Assert.That(viewModel.ProgressText, Does.Contain("50%"));
            Assert.That(viewModel.CanCancel, Is.True);
            Assert.That(viewModel.CanRetry, Is.False);

            viewModel.CancelCommand.Execute(null);

            Assert.That(cancelCalls, Is.EqualTo(1));
            Assert.That(retryCalls, Is.EqualTo(0));
        }

        [Test]
        public void FailedJob_ShowsErrorAndInvokesRetry()
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Failed Game", "source", "destination", "LocalFolder");
            manager.Transition(job.Id, CloudTransferState.Failed, "Source unavailable");
            var retryCalls = 0;

            var viewModel = new CloudTransferQueueItemViewModel(
                job,
                () => { },
                () => retryCalls++);

            Assert.That(viewModel.StateText, Is.EqualTo("Failed"));
            Assert.That(viewModel.ProgressText, Is.EqualTo("Source unavailable"));
            Assert.That(viewModel.CanCancel, Is.False);
            Assert.That(viewModel.CanRetry, Is.True);

            viewModel.RetryCommand.Execute(null);

            Assert.That(retryCalls, Is.EqualTo(1));
        }

        [Test]
        public void CancellationRequestedOrFinalizingJob_DisablesCancelCommand()
        {
            var manager = new CloudTransferManager(2);
            var cancelling = manager.Enqueue(Guid.NewGuid(), "Cancelling", "source", "destination", "LocalFolder");
            var finalizing = manager.Enqueue(Guid.NewGuid(), "Finalizing", "source", "destination", "LocalFolder");
            manager.Cancel(cancelling.Id);
            manager.Transition(finalizing.Id, CloudTransferState.Transferring);
            manager.Transition(finalizing.Id, CloudTransferState.Finalizing);

            var cancellingItem = new CloudTransferQueueItemViewModel(cancelling, () => { }, () => { });
            var finalizingItem = new CloudTransferQueueItemViewModel(finalizing, () => { }, () => { });

            Assert.That(cancellingItem.CanCancel, Is.False);
            Assert.That(cancellingItem.CancelCommand.CanExecute(null), Is.False);
            Assert.That(finalizingItem.CanCancel, Is.False);
            Assert.That(finalizingItem.CancelCommand.CanExecute(null), Is.False);
        }

        [Test]
        public void RetryCommand_ExecutedTwiceBeforeRefresh_ClaimsRetryOnlyOnceAndDoesNotThrow()
        {
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Failed", "source", "destination", "LocalFolder");
            manager.Transition(job.Id, CloudTransferState.Failed, "failed");
            var retryCalls = 0;
            var item = new CloudTransferQueueItemViewModel(job, () => { }, () => retryCalls++);

            Assert.DoesNotThrow(() => item.RetryCommand.Execute(null));
            Assert.DoesNotThrow(() => item.RetryCommand.Execute(null));

            Assert.That(retryCalls, Is.EqualTo(1));
            Assert.That(item.CanRetry, Is.False);
            Assert.That(item.RetryCommand.CanExecute(null), Is.False);
        }
    }
}
