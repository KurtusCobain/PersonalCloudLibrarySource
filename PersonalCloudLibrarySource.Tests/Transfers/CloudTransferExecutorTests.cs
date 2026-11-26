using NUnit.Framework;
using System;
using System.IO;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class CloudTransferExecutorTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-TransferExecutorTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, true);
            }
        }

        [Test]
        public void ExecuteLocalFile_Success_TransitionsJobToCompleted()
        {
            var source = Path.Combine(testRoot, "source.bin");
            var destination = Path.Combine(testRoot, "cache", "game.bin");
            File.WriteAllText(source, "content");
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", source, destination, "LocalFolder");
            var executor = new CloudTransferExecutor(manager, new LocalTransferAdapter());

            var result = executor.ExecuteLocal(job.Id, false);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(job.State, Is.EqualTo(CloudTransferState.Completed));
            Assert.That(File.Exists(destination), Is.True);
            Assert.That(job.BytesTransferred, Is.EqualTo(new FileInfo(destination).Length));
        }

        [Test]
        public void ExecuteLocalFile_Failure_TransitionsJobToFailedAndStartsNextJob()
        {
            var missingSource = Path.Combine(testRoot, "missing.bin");
            var destination = Path.Combine(testRoot, "cache", "missing.bin");
            var manager = new CloudTransferManager(1);
            var failedJob = manager.Enqueue(Guid.NewGuid(), "Missing", missingSource, destination, "LocalFolder");
            var nextJob = manager.Enqueue(Guid.NewGuid(), "Next", "source-2", "dest-2", "LocalFolder");
            var executor = new CloudTransferExecutor(manager, new LocalTransferAdapter());

            var result = executor.ExecuteLocal(failedJob.Id, false);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(failedJob.State, Is.EqualTo(CloudTransferState.Failed));
            Assert.That(failedJob.ErrorSummary, Does.Contain("does not exist"));
            Assert.That(nextJob.State, Is.EqualTo(CloudTransferState.Preparing));
        }

        [Test]
        public void ExecuteLocalFile_CancelledJob_DoesNotStartCopy()
        {
            var source = Path.Combine(testRoot, "source.bin");
            var destination = Path.Combine(testRoot, "cache", "game.bin");
            File.WriteAllText(source, "content");
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", source, destination, "LocalFolder");
            manager.Cancel(job.Id);
            var executor = new CloudTransferExecutor(manager, new LocalTransferAdapter());

            var result = executor.ExecuteLocal(job.Id, false);

            Assert.That(result.Cancelled, Is.True);
            Assert.That(job.State, Is.EqualTo(CloudTransferState.Cancelled));
            Assert.That(File.Exists(destination), Is.False);
        }

        [Test]
        public void ExecuteRcloneFile_Success_TransitionsJobToCompleted()
        {
            var destination = Path.Combine(testRoot, "cache", "cloud-game.bin");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath));
                File.WriteAllText(request.DestinationPath, "cloud content");
                return RcloneProcessResult.Success("ok");
            });
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(
                Guid.NewGuid(),
                "Cloud Game",
                "library/cloud-game.bin",
                destination,
                PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType);
            var executor = new CloudTransferExecutor(
                manager,
                new LocalTransferAdapter(),
                new RcloneTransferAdapter(runner));
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                RcloneExecutablePath = "rclone",
                RcloneRemoteName = "games",
                RcloneTimeoutSeconds = 30
            };

            var result = executor.ExecuteRclone(job.Id, settings);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(job.State, Is.EqualTo(CloudTransferState.Completed));
            Assert.That(File.Exists(destination), Is.True);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("cloud content"));
            Assert.That(runner.RunCount, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SuccessfulPublicationWithMissingOrChangedDestinationRetainsRecoveryReservation(bool exists)
        {
            var destination = Path.Combine(testRoot, "game.bin");
            if (exists) File.WriteAllText(destination, "unexpected");
            var manager = new CloudTransferManager(1);
            var journalPath = Path.Combine(testRoot, "journal");
            var journal = new TransferRecoveryJournal(journalPath);
            var executor = new CloudTransferExecutor(manager, new LocalTransferAdapter());
            var completion = typeof(CloudTransferExecutor).GetMethod("CompleteTransfer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(completion, Is.Not.Null);
            using (var queue = new TransferQueueService(manager,
                new PublishedResultExecutor(manager, executor, completion), journal))
            {
                var job = queue.EnqueueLocal(Guid.NewGuid(), "Post-publication verification",
                    Path.Combine(testRoot, "source"), destination, "LocalFolder", false);
                var observed = queue.GetCompletion(job.Id);
                Assert.That(observed.Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(job.State, Is.EqualTo(CloudTransferState.Failed));
                Assert.That(observed.Result.Succeeded, Is.False);
                Assert.That(observed.Result.RequiresManualRecovery, Is.True);
                Assert.That(job.CanRetry, Is.False);
                Assert.That(journal.Inspect().Records.Count, Is.EqualTo(1));
                Assert.That(File.Exists(destination), Is.EqualTo(exists));
                if (exists) Assert.That(File.ReadAllText(destination), Is.EqualTo("unexpected"));
                Assert.Throws<TransferRetryRefusedException>(() => queue.EnqueueLocal(
                    Guid.NewGuid(), "Conflicting", "source", destination, "LocalFolder", false));
            }
        }

        // Deterministically exercise the real post-publication executor path
        // without relying on a racing thread to replace a genuine transfer.
        private sealed class PublishedResultExecutor : ICloudTransferExecutor
        {
            private readonly CloudTransferManager manager;
            private readonly CloudTransferExecutor executor;
            private readonly System.Reflection.MethodInfo completion;

            public PublishedResultExecutor(CloudTransferManager manager, CloudTransferExecutor executor,
                System.Reflection.MethodInfo completion)
            {
                this.manager = manager;
                this.executor = executor;
                this.completion = completion;
            }

            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool directory)
            {
                manager.Transition(jobId, CloudTransferState.Transferring);
                manager.Transition(jobId, CloudTransferState.Finalizing);
                return (CloudTransferExecutionResult)completion.Invoke(executor, new object[] {
                    manager.GetJob(jobId), CloudTransferExecutionResult.Success(1, 1), directory });
            }

            public CloudTransferExecutionResult ExecuteRclone(Guid jobId, PersonalCloudLibrarySourceSettings settings)
                => ExecuteLocal(jobId, false);
        }

        private sealed class FakeRcloneProcessRunner : IRcloneProcessRunner
        {
            private readonly Func<RcloneTransferRequest, RcloneProcessResult> handler;

            public FakeRcloneProcessRunner(Func<RcloneTransferRequest, RcloneProcessResult> handler)
            {
                this.handler = handler;
            }

            public int RunCount { get; private set; }

            public RcloneProcessResult Run(
                RcloneTransferRequest request,
                CancellationToken cancellationToken,
                Action<long, long?> progress)
            {
                RunCount++;
                progress?.Invoke(512, 1024);
                return handler(request);
            }
        }
    }
}
