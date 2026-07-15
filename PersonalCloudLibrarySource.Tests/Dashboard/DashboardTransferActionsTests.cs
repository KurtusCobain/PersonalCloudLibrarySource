using NUnit.Framework;
using System;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Dashboard
{
    [TestFixture]
    public class DashboardTransferActionsTests
    {
        [Test]
        public void TwoRowsForSameFailedJob_SecondExpectedDuplicateRetryDoesNotEscapeUi()
        {
            var manager = new CloudTransferManager(1);
            var executor = new FailThenBlockExecutor();
            using (var queue = new TransferQueueService(manager, executor))
            {
                var failed = queue.EnqueueLocal(
                    Guid.NewGuid(), "Example", "source", "destination", "LocalFolder", false);
                Assert.That(queue.GetCompletion(failed.Id).Wait(TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(failed.State, Is.EqualTo(CloudTransferState.Failed));

                var actions = new DashboardTransferActions(
                    queue,
                    () => new PersonalCloudLibrarySourceSettingsV3());
                var firstRow = new CloudTransferQueueItemViewModel(
                    failed, () => { }, () => actions.Retry(failed.Id));
                var secondRow = new CloudTransferQueueItemViewModel(
                    failed, () => { }, () => actions.Retry(failed.Id));

                try
                {
                    firstRow.RetryCommand.Execute(null);
                    Assert.That(executor.RetryStarted.Wait(TimeSpan.FromSeconds(2)), Is.True);
                    Assert.DoesNotThrow(() => secondRow.RetryCommand.Execute(null));
                }
                finally
                {
                    executor.ReleaseRetry.Set();
                }
            }
        }

        [Test]
        public void RetryCompletedJob_UnrelatedInvalidStateStillPropagates()
        {
            var manager = new CloudTransferManager(1);
            using (var queue = new TransferQueueService(manager, new SuccessExecutor(manager)))
            {
                var completed = queue.EnqueueLocal(
                    Guid.NewGuid(), "Example", "source", "destination", "LocalFolder", false);
                Assert.That(queue.GetCompletion(completed.Id).Wait(TimeSpan.FromSeconds(2)), Is.True);
                var actions = new DashboardTransferActions(
                    queue,
                    () => new PersonalCloudLibrarySourceSettingsV3());

                Assert.Throws<InvalidOperationException>(() => actions.Retry(completed.Id));
            }
        }

        private sealed class FailThenBlockExecutor : ICloudTransferExecutor
        {
            private int calls;
            public ManualResetEventSlim RetryStarted { get; } = new ManualResetEventSlim();
            public ManualResetEventSlim ReleaseRetry { get; } = new ManualResetEventSlim();

            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    return CloudTransferExecutionResult.Failure("first attempt failed");
                }

                RetryStarted.Set();
                ReleaseRetry.Wait(TimeSpan.FromSeconds(5));
                return CloudTransferExecutionResult.Failure("retry stopped");
            }

            public CloudTransferExecutionResult ExecuteRclone(
                Guid jobId,
                PersonalCloudLibrarySourceSettings settings)
            {
                return ExecuteLocal(jobId, false);
            }
        }

        private sealed class SuccessExecutor : ICloudTransferExecutor
        {
            private readonly CloudTransferManager manager;

            public SuccessExecutor(CloudTransferManager manager)
            {
                this.manager = manager;
            }

            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
            {
                manager.Transition(jobId, CloudTransferState.Transferring);
                return CloudTransferExecutionResult.Success(0, 0);
            }

            public CloudTransferExecutionResult ExecuteRclone(
                Guid jobId,
                PersonalCloudLibrarySourceSettings settings)
            {
                return ExecuteLocal(jobId, false);
            }
        }
    }
}
