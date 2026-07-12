using NUnit.Framework;
using System;
using System.IO;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class RcloneTransferAdapterTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-RcloneTransferTests", Guid.NewGuid().ToString("N"));
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
        public void CopyFile_Success_MovesVerifiedPartialFileIntoFinalDestination()
        {
            var destination = Path.Combine(testRoot, "cache", "game.zip");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath));
                File.WriteAllText(request.DestinationPath, "cloud content");
                return RcloneProcessResult.Success("ok");
            });
            var adapter = new RcloneTransferAdapter(runner);
            var settings = CreateSettings();

            var result = adapter.Copy(
                settings,
                "library/game.zip",
                destination,
                false,
                CancellationToken.None,
                null);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(destination), Is.True);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("cloud content"));
            Assert.That(File.Exists(destination + ".pcls-partial"), Is.False);
            Assert.That(runner.LastRequest.DestinationPath, Is.EqualTo(destination + ".pcls-partial"));
        }

        [Test]
        public void CopyDirectory_Success_MovesVerifiedPartialDirectoryIntoFinalDestination()
        {
            var destination = Path.Combine(testRoot, "cache", "Disc Game");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(Path.Combine(request.DestinationPath, "nested"));
                File.WriteAllText(Path.Combine(request.DestinationPath, "nested", "game.cue"), "cue");
                return RcloneProcessResult.Success("ok");
            });
            var adapter = new RcloneTransferAdapter(runner);

            var result = adapter.Copy(
                CreateSettings(),
                "library/Disc Game",
                destination,
                true,
                CancellationToken.None,
                null);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(Path.Combine(destination, "nested", "game.cue")), Is.True);
            Assert.That(Directory.Exists(destination + ".pcls-partial"), Is.False);
        }

        [Test]
        public void Copy_Cancelled_RemovesPartialDataAndLeavesFinalDestinationMissing()
        {
            var destination = Path.Combine(testRoot, "cache", "game.zip");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath));
                File.WriteAllText(request.DestinationPath, "partial");
                return RcloneProcessResult.Cancelled("cancelled");
            });
            var adapter = new RcloneTransferAdapter(runner);

            var result = adapter.Copy(
                CreateSettings(),
                "library/game.zip",
                destination,
                false,
                CancellationToken.None,
                null);

            Assert.That(result.Cancelled, Is.True);
            Assert.That(File.Exists(destination), Is.False);
            Assert.That(File.Exists(destination + ".pcls-partial"), Is.False);
        }

        [Test]
        public void Copy_ExistingDestination_FailsBeforeStartingRclone()
        {
            var destination = Path.Combine(testRoot, "game.zip");
            File.WriteAllText(destination, "existing");
            var runner = new FakeRcloneProcessRunner(request => RcloneProcessResult.Success("unexpected"));
            var adapter = new RcloneTransferAdapter(runner);

            var result = adapter.Copy(
                CreateSettings(),
                "library/game.zip",
                destination,
                false,
                CancellationToken.None,
                null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("already exists"));
            Assert.That(runner.RunCount, Is.EqualTo(0));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("existing"));
        }

        private static PersonalCloudLibrarySourceSettingsV3 CreateSettings()
        {
            return new PersonalCloudLibrarySourceSettingsV3
            {
                RcloneExecutablePath = "rclone",
                RcloneRemoteName = "games",
                RcloneTimeoutSeconds = 30
            };
        }

        private sealed class FakeRcloneProcessRunner : IRcloneProcessRunner
        {
            private readonly Func<RcloneTransferRequest, RcloneProcessResult> handler;

            public FakeRcloneProcessRunner(Func<RcloneTransferRequest, RcloneProcessResult> handler)
            {
                this.handler = handler;
            }

            public int RunCount { get; private set; }
            public RcloneTransferRequest LastRequest { get; private set; }

            public RcloneProcessResult Run(
                RcloneTransferRequest request,
                CancellationToken cancellationToken,
                Action<long, long?> progress)
            {
                RunCount++;
                LastRequest = request;
                progress?.Invoke(512, 1024);
                return handler(request);
            }
        }
    }
}
