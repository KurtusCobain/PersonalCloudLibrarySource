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
