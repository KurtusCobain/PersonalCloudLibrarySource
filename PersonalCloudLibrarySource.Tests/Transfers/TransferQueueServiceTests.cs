using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class TransferQueueServiceTests
    {
        [Test]
        public void Workers_EnforceConfiguredConcurrencyAndObserveAllJobs()
        {
            var manager = new CloudTransferManager(2);
            var executor = new BlockingExecutor(manager);
            using (var queue = new TransferQueueService(manager, executor))
            {
                var first = queue.EnqueueLocal(Guid.NewGuid(), "First", "source-1", "dest-1", "LocalFolder", false);
                var second = queue.EnqueueLocal(Guid.NewGuid(), "Second", "source-2", "dest-2", "LocalFolder", false);
                var third = queue.EnqueueLocal(Guid.NewGuid(), "Third", "source-3", "dest-3", "LocalFolder", false);

                Assert.That(executor.TwoStarted.Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(executor.StartedCount, Is.EqualTo(2));
                Assert.That(third.State, Is.EqualTo(CloudTransferState.Queued));

                executor.Release.Set();
                Assert.That(queue.GetCompletion(first.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(queue.GetCompletion(second.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(queue.GetCompletion(third.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(executor.MaximumConcurrent, Is.EqualTo(2));
                Assert.That(manager.Jobs.All(job => job.State == CloudTransferState.Completed), Is.True);
            }
        }

        [Test]
        public void Cancel_QueuedJob_CompletesCancelledWithoutExecuting()
        {
            var manager = new CloudTransferManager(1);
            var executor = new BlockingExecutor(manager);
            using (var queue = new TransferQueueService(manager, executor))
            {
                var active = queue.EnqueueLocal(Guid.NewGuid(), "Active", "source-1", "dest-1", "LocalFolder", false);
                Assert.That(executor.OneStarted.Wait(TimeSpan.FromSeconds(3)), Is.True);
                var queued = queue.EnqueueLocal(Guid.NewGuid(), "Queued", "source-2", "dest-2", "LocalFolder", false);

                Assert.That(queue.Cancel(queued.Id), Is.True);
                executor.Release.Set();

                Assert.That(queue.GetCompletion(active.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(queue.GetCompletion(queued.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(queued.State, Is.EqualTo(CloudTransferState.Cancelled));
                Assert.That(executor.ExecutedJobIds, Does.Not.Contain(queued.Id));
            }
        }

        [Test]
        public void Cancel_QueuedJobSignalsWorkerAndCompletesWhileActiveJobRemainsBlocked()
        {
            var manager = new CloudTransferManager(1);
            var executor = new BlockingExecutor(manager);
            using (var queue = new TransferQueueService(manager, executor))
            {
                var active = queue.EnqueueLocal(Guid.NewGuid(), "Active", "source-1", "dest-1", "LocalFolder", false);
                Assert.That(executor.OneStarted.Wait(TimeSpan.FromSeconds(3)), Is.True);
                var queued = queue.EnqueueLocal(Guid.NewGuid(), "Queued", "source-2", "dest-2", "LocalFolder", false);

                Assert.That(queue.Cancel(queued.Id), Is.True);
                Assert.That(queue.GetCompletion(queued.Id).Wait(TimeSpan.FromSeconds(3)), Is.True,
                    "queued cancellation must not wait for the active transfer to finish");
                Assert.That(queued.State, Is.EqualTo(CloudTransferState.Cancelled));
                Assert.That(executor.ExecutedJobIds, Does.Not.Contain(queued.Id));
                Assert.That(queue.GetCompletion(active.Id).IsCompleted, Is.False);

                executor.Release.Set();
                Assert.That(queue.GetCompletion(active.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
            }
        }

        [Test]
        public void CancelDuringVerification_CancelsOnceAndLeavesNoPartialOrFinalData()
        {
            var root = CreateVerifiedTestRoot();
            try
            {
                var source = Path.Combine(root, "source.bin");
                var destination = Path.Combine(root, "cache", "game.bin");
                File.WriteAllText(source, "verified content");
                var manager = new CloudTransferManager(1);
                var executor = new PhaseRaceExecutor(manager, CloudTransferState.Verifying);
                using (var queue = new TransferQueueService(manager, executor))
                {
                    var terminalCount = 0;
                    queue.JobTerminated += (sender, args) => Interlocked.Increment(ref terminalCount);
                    var job = queue.EnqueueLocal(Guid.NewGuid(), "Verify race", source, destination, "LocalFolder", false);

                    Assert.That(queue.GetCompletion(job.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                    Assert.That(executor.CancelAccepted, Is.True);
                    Assert.That(job.State, Is.EqualTo(CloudTransferState.Cancelled));
                    Assert.That(terminalCount, Is.EqualTo(1));
                    Assert.That(File.Exists(destination), Is.False);
                    Assert.That(File.Exists(TransferPartialPathPolicy.Create(destination, job.Id)), Is.False);
                }
            }
            finally
            {
                DeleteVerifiedTestRoot(root);
            }
        }

        [Test]
        public void CancelAfterFinalizationCommit_IsRejectedAndCompletesOnceWithoutCorruption()
        {
            var root = CreateVerifiedTestRoot();
            try
            {
                var source = Path.Combine(root, "source.bin");
                var destination = Path.Combine(root, "cache", "game.bin");
                File.WriteAllText(source, "committed content");
                var manager = new CloudTransferManager(1);
                var executor = new PhaseRaceExecutor(manager, CloudTransferState.Finalizing);
                using (var queue = new TransferQueueService(manager, executor))
                {
                    var terminalCount = 0;
                    queue.JobTerminated += (sender, args) => Interlocked.Increment(ref terminalCount);
                    var job = queue.EnqueueLocal(Guid.NewGuid(), "Commit race", source, destination, "LocalFolder", false);

                    var result = queue.GetCompletion(job.Id).Result;
                    Assert.That(executor.CancelAccepted, Is.False);
                    Assert.That(result.Succeeded, Is.True);
                    Assert.That(job.State, Is.EqualTo(CloudTransferState.Completed));
                    Assert.That(terminalCount, Is.EqualTo(1));
                    Assert.That(File.ReadAllText(destination), Is.EqualTo("committed content"));
                    Assert.That(File.Exists(TransferPartialPathPolicy.Create(destination, job.Id)), Is.False);
                }
            }
            finally
            {
                DeleteVerifiedTestRoot(root);
            }
        }

        [Test]
        public void CancelBetweenVerificationCheckAndCommit_IsAcceptedAndLeavesNoData()
        {
            var root = CreateVerifiedTestRoot();
            try
            {
                var source = Path.Combine(root, "source.bin");
                var destination = Path.Combine(root, "cache", "game.bin");
                File.WriteAllText(source, "race content");
                var manager = new CloudTransferManager(1);
                var executor = new PhaseRaceExecutor(
                    manager,
                    CloudTransferState.Finalizing,
                    cancelBeforePhaseTransition: true);
                using (var queue = new TransferQueueService(manager, executor))
                {
                    var terminalCount = 0;
                    queue.JobTerminated += (sender, args) => Interlocked.Increment(ref terminalCount);
                    var job = queue.EnqueueLocal(Guid.NewGuid(), "Pre-commit race", source, destination, "LocalFolder", false);

                    var result = queue.GetCompletion(job.Id).Result;
                    Assert.That(executor.CancelAccepted, Is.True);
                    Assert.That(result.Cancelled, Is.True);
                    Assert.That(job.State, Is.EqualTo(CloudTransferState.Cancelled));
                    Assert.That(terminalCount, Is.EqualTo(1));
                    Assert.That(File.Exists(destination), Is.False);
                    Assert.That(File.Exists(TransferPartialPathPolicy.Create(destination, job.Id)), Is.False);
                }
            }
            finally
            {
                DeleteVerifiedTestRoot(root);
            }
        }

        [Test]
        public void WorkerException_FailsJobAndPublishesTerminalEventExactlyOnce()
        {
            var manager = new CloudTransferManager(1);
            var executor = new ThrowingExecutor();
            using (var queue = new TransferQueueService(manager, executor))
            {
                var terminalCount = 0;
                queue.JobTerminated += (sender, args) => Interlocked.Increment(ref terminalCount);

                var job = queue.EnqueueLocal(Guid.NewGuid(), "Broken", "source", "dest", "LocalFolder", false);
                Assert.That(queue.GetCompletion(job.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);

                Assert.That(job.State, Is.EqualTo(CloudTransferState.Failed));
                Assert.That(job.ErrorSummary, Does.Contain("worker exploded"));
                Assert.That(terminalCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void TerminalSubscriberException_DoesNotFaultWorkerOrStrandLaterJob()
        {
            var manager = new CloudTransferManager(1);
            var executor = new ImmediateSuccessExecutor(manager);
            using (var queue = new TransferQueueService(manager, executor))
            {
                queue.JobTerminated += (sender, args) => throw new InvalidOperationException("subscriber failed");
                var first = queue.EnqueueLocal(Guid.NewGuid(), "First", "source-1", "dest-1", "LocalFolder", false);
                var second = queue.EnqueueLocal(Guid.NewGuid(), "Second", "source-2", "dest-2", "LocalFolder", false);

                Assert.That(queue.GetCompletion(first.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(queue.GetCompletion(second.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(first.State, Is.EqualTo(CloudTransferState.Completed));
                Assert.That(second.State, Is.EqualTo(CloudTransferState.Completed));
            }
        }

        [Test]
        public void Retry_SuppressesDuplicateActiveAttemptAndIsQueueOwned()
        {
            var manager = new CloudTransferManager(1);
            var executor = new FailThenBlockExecutor(manager);
            using (var queue = new TransferQueueService(manager, executor))
            {
                var failed = queue.EnqueueLocal(Guid.NewGuid(), "Retry", "source", "dest", "LocalFolder", false);
                Assert.That(queue.GetCompletion(failed.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(failed.State, Is.EqualTo(CloudTransferState.Failed));

                var retry = queue.Retry(failed.Id);
                Assert.That(executor.RetryStarted.Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.Throws<InvalidOperationException>(() => queue.Retry(failed.Id));

                executor.Release.Set();
                Assert.That(queue.GetCompletion(retry.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(retry.State, Is.EqualTo(CloudTransferState.Completed));
                Assert.That(retry.PreviousAttemptId, Is.EqualTo(failed.Id));
            }
        }

        [Test]
        public void Shutdown_IsBoundedIdempotentCancelsActiveAndRejectsNewWork()
        {
            var manager = new CloudTransferManager(1);
            var executor = new CancellationAwareExecutor(manager);
            using (var queue = new TransferQueueService(manager, executor))
            {
                var job = queue.EnqueueLocal(Guid.NewGuid(), "Active", "source", "dest", "LocalFolder", false);
                Assert.That(executor.Started.Wait(TimeSpan.FromSeconds(3)), Is.True);

                Assert.That(queue.Shutdown(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(queue.Shutdown(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(job.State, Is.EqualTo(CloudTransferState.Cancelled));
                Assert.Throws<InvalidOperationException>(() =>
                    queue.EnqueueLocal(Guid.NewGuid(), "Late", "source", "dest", "LocalFolder", false));
            }
        }

        [Test]
        public void CancellationCleanupFailure_RemainsFailedInsteadOfCancelled()
        {
            var manager = new CloudTransferManager(1);
            var executor = new CancellationCleanupFailureExecutor(manager);
            using (var queue = new TransferQueueService(manager, executor))
            {
                var job = queue.EnqueueLocal(Guid.NewGuid(), "Locked", "source", "dest", "LocalFolder", false);
                Assert.That(executor.Started.Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(queue.Cancel(job.Id), Is.True);
                Assert.That(queue.GetCompletion(job.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);

                Assert.That(job.State, Is.EqualTo(CloudTransferState.Failed));
                Assert.That(job.ErrorSummary, Does.Contain("cleanup failed"));
            }
        }

        [Test]
        public void SetMaxConcurrentTransfers_IncreaseStartsAnotherWorkerAndDecreaseAppliesToNextClaim()
        {
            var manager = new CloudTransferManager(1);
            var executor = new SelectiveBlockingExecutor(manager);
            using (var queue = new TransferQueueService(manager, executor))
            {
                var first = queue.EnqueueLocal(Guid.NewGuid(), "First", "source-1", "dest-1", "LocalFolder", false);
                var second = queue.EnqueueLocal(Guid.NewGuid(), "Second", "source-2", "dest-2", "LocalFolder", false);
                var third = queue.EnqueueLocal(Guid.NewGuid(), "Third", "source-3", "dest-3", "LocalFolder", false);
                Assert.That(executor.WaitForStartedCount(1), Is.True);

                queue.SetMaxConcurrentTransfers(2);
                Assert.That(executor.WaitForStartedCount(2), Is.True);
                queue.SetMaxConcurrentTransfers(1);

                executor.Release(first.Id);
                Assert.That(queue.GetCompletion(first.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(executor.StartedCount, Is.EqualTo(2), "decrease must prevent a third claim while one worker remains active");

                executor.Release(second.Id);
                Assert.That(queue.GetCompletion(second.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(executor.WaitForStartedCount(3), Is.True);
                executor.Release(third.Id);
                Assert.That(queue.GetCompletion(third.Id).Wait(TimeSpan.FromSeconds(3)), Is.True);
            }
        }

        private sealed class BlockingExecutor : ICloudTransferExecutor
        {
            private readonly CloudTransferManager manager;
            private int active;
            private int maximum;

            public BlockingExecutor(CloudTransferManager manager)
            {
                this.manager = manager;
            }

            public ManualResetEventSlim OneStarted { get; } = new ManualResetEventSlim();
            public ManualResetEventSlim TwoStarted { get; } = new ManualResetEventSlim();
            public ManualResetEventSlim Release { get; } = new ManualResetEventSlim();
            public ConcurrentBag<Guid> ExecutedJobIds { get; } = new ConcurrentBag<Guid>();
            private int startedCount;
            public int StartedCount => startedCount;
            public int MaximumConcurrent => maximum;

            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
            {
                ExecutedJobIds.Add(jobId);
                var current = Interlocked.Increment(ref active);
                Interlocked.Increment(ref startedCount);
                UpdateMaximum(current);
                OneStarted.Set();
                if (Volatile.Read(ref startedCount) >= 2)
                {
                    TwoStarted.Set();
                }

                manager.Transition(jobId, CloudTransferState.Transferring);
                Release.Wait();
                Interlocked.Decrement(ref active);
                return CloudTransferExecutionResult.Success(1, 1);
            }

            public CloudTransferExecutionResult ExecuteRclone(Guid jobId, PersonalCloudLibrarySourceSettings settings)
            {
                return ExecuteLocal(jobId, false);
            }

            private void UpdateMaximum(int value)
            {
                int observed;
                do
                {
                    observed = maximum;
                    if (observed >= value)
                    {
                        return;
                    }
                }
                while (Interlocked.CompareExchange(ref maximum, value, observed) != observed);
            }
        }

        private sealed class ThrowingExecutor : ICloudTransferExecutor
        {
            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
            {
                throw new InvalidOperationException("worker exploded");
            }

            public CloudTransferExecutionResult ExecuteRclone(Guid jobId, PersonalCloudLibrarySourceSettings settings)
            {
                return ExecuteLocal(jobId, false);
            }
        }

        private sealed class ImmediateSuccessExecutor : ICloudTransferExecutor
        {
            private readonly CloudTransferManager manager;

            public ImmediateSuccessExecutor(CloudTransferManager manager)
            {
                this.manager = manager;
            }

            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
            {
                manager.Transition(jobId, CloudTransferState.Transferring);
                return CloudTransferExecutionResult.Success(1, 1);
            }

            public CloudTransferExecutionResult ExecuteRclone(Guid jobId, PersonalCloudLibrarySourceSettings settings)
            {
                return ExecuteLocal(jobId, false);
            }
        }

        private sealed class FailThenBlockExecutor : ICloudTransferExecutor
        {
            private readonly CloudTransferManager manager;
            private int calls;

            public FailThenBlockExecutor(CloudTransferManager manager)
            {
                this.manager = manager;
            }

            public ManualResetEventSlim RetryStarted { get; } = new ManualResetEventSlim();
            public ManualResetEventSlim Release { get; } = new ManualResetEventSlim();

            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
            {
                manager.Transition(jobId, CloudTransferState.Transferring);
                if (Interlocked.Increment(ref calls) == 1)
                {
                    return CloudTransferExecutionResult.Failure("first attempt failed");
                }

                RetryStarted.Set();
                Release.Wait();
                return CloudTransferExecutionResult.Success(1, 1);
            }

            public CloudTransferExecutionResult ExecuteRclone(Guid jobId, PersonalCloudLibrarySourceSettings settings)
            {
                return ExecuteLocal(jobId, false);
            }
        }

        private sealed class CancellationAwareExecutor : ICloudTransferExecutor
        {
            private readonly CloudTransferManager manager;

            public CancellationAwareExecutor(CloudTransferManager manager)
            {
                this.manager = manager;
            }

            public ManualResetEventSlim Started { get; } = new ManualResetEventSlim();

            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
            {
                var job = manager.GetJob(jobId);
                manager.Transition(jobId, CloudTransferState.Transferring);
                Started.Set();
                job.CancellationToken.WaitHandle.WaitOne();
                return CloudTransferExecutionResult.CancelledResult();
            }

            public CloudTransferExecutionResult ExecuteRclone(Guid jobId, PersonalCloudLibrarySourceSettings settings)
            {
                return ExecuteLocal(jobId, false);
            }
        }

        private sealed class SelectiveBlockingExecutor : ICloudTransferExecutor
        {
            private readonly CloudTransferManager manager;
            private readonly ConcurrentDictionary<Guid, ManualResetEventSlim> releases =
                new ConcurrentDictionary<Guid, ManualResetEventSlim>();
            private readonly ConcurrentDictionary<int, ManualResetEventSlim> startedSignals =
                new ConcurrentDictionary<int, ManualResetEventSlim>();
            private int startedCount;

            public SelectiveBlockingExecutor(CloudTransferManager manager)
            {
                this.manager = manager;
            }

            public int StartedCount => Volatile.Read(ref startedCount);

            public bool WaitForStartedCount(int count)
            {
                var started = startedSignals.GetOrAdd(count, _ => new ManualResetEventSlim());
                if (StartedCount >= count)
                {
                    return true;
                }

                return started.Wait(TimeSpan.FromSeconds(3));
            }

            public void Release(Guid jobId)
            {
                releases.GetOrAdd(jobId, _ => new ManualResetEventSlim()).Set();
            }

            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
            {
                manager.Transition(jobId, CloudTransferState.Transferring);
                var count = Interlocked.Increment(ref startedCount);
                ManualResetEventSlim started;
                if (startedSignals.TryGetValue(count, out started))
                {
                    started.Set();
                }
                releases.GetOrAdd(jobId, _ => new ManualResetEventSlim()).Wait();
                return CloudTransferExecutionResult.Success(1, 1);
            }

            public CloudTransferExecutionResult ExecuteRclone(Guid jobId, PersonalCloudLibrarySourceSettings settings)
            {
                return ExecuteLocal(jobId, false);
            }
        }

        private sealed class CancellationCleanupFailureExecutor : ICloudTransferExecutor
        {
            private readonly CloudTransferManager manager;

            public CancellationCleanupFailureExecutor(CloudTransferManager manager)
            {
                this.manager = manager;
            }

            public ManualResetEventSlim Started { get; } = new ManualResetEventSlim();

            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
            {
                var job = manager.GetJob(jobId);
                manager.Transition(jobId, CloudTransferState.Transferring);
                Started.Set();
                job.CancellationToken.WaitHandle.WaitOne();
                return CloudTransferExecutionResult.Failure("cleanup failed");
            }

            public CloudTransferExecutionResult ExecuteRclone(Guid jobId, PersonalCloudLibrarySourceSettings settings)
            {
                return ExecuteLocal(jobId, false);
            }
        }

        private sealed class PhaseRaceExecutor : ICloudTransferExecutor
        {
            private readonly CloudTransferManager manager;
            private readonly CloudTransferState cancelAt;
            private readonly bool cancelBeforePhaseTransition;

            public PhaseRaceExecutor(
                CloudTransferManager manager,
                CloudTransferState cancelAt,
                bool cancelBeforePhaseTransition = false)
            {
                this.manager = manager;
                this.cancelAt = cancelAt;
                this.cancelBeforePhaseTransition = cancelBeforePhaseTransition;
            }

            public bool CancelAccepted { get; private set; }

            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
            {
                var job = manager.GetJob(jobId);
                manager.Transition(jobId, CloudTransferState.Transferring);
                return new LocalTransferAdapter(4096).CopyFile(
                    job.Source,
                    job.Destination,
                    job.Id,
                    job.CancellationToken,
                    null,
                    phase =>
                    {
                        if (phase == cancelAt && cancelBeforePhaseTransition)
                        {
                            CancelAccepted = manager.Cancel(jobId);
                        }

                        manager.Transition(jobId, phase);
                        if (phase == cancelAt && !cancelBeforePhaseTransition)
                        {
                            CancelAccepted = manager.Cancel(jobId);
                        }
                    });
            }

            public CloudTransferExecutionResult ExecuteRclone(Guid jobId, PersonalCloudLibrarySourceSettings settings)
            {
                return ExecuteLocal(jobId, false);
            }
        }

        private static string CreateVerifiedTestRoot()
        {
            var parent = Path.Combine(Path.GetTempPath(), "PCLS-TransferQueueRaceTests");
            var root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Assert.That(Path.GetFileName(root).Length, Is.EqualTo(32));
            Assert.That(Path.GetFullPath(root).StartsWith(Path.GetFullPath(parent) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase), Is.True);
            return root;
        }

        private static void DeleteVerifiedTestRoot(string root)
        {
            var parent = Path.Combine(Path.GetTempPath(), "PCLS-TransferQueueRaceTests");
            var fullRoot = Path.GetFullPath(root);
            if (!fullRoot.StartsWith(Path.GetFullPath(parent) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) || Path.GetFileName(fullRoot).Length != 32)
            {
                throw new InvalidOperationException("Refusing to delete an unverified transfer test root.");
            }

            if (Directory.Exists(fullRoot))
            {
                Directory.Delete(fullRoot, true);
            }
        }
    }
}
