using NUnit.Framework;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class RcloneReceiptIntegrationTests
    {
        private string root;
        private string cache;
        private string destination;
        private CacheOwnershipReceiptStore receipts;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-RcloneReceipt", Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            destination = Path.Combine(cache, "remote-game");
            Directory.CreateDirectory(cache);
            receipts = new CacheOwnershipReceiptStore(Path.Combine(root, "receipts"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void RcloneSuccess_CreatesV2ReceiptAndDetectsPersonalFiles()
        {
            var runner = new FixtureRunner(request =>
            {
                Directory.CreateDirectory(Path.Combine(request.DestinationPath, "assets"));
                File.WriteAllText(Path.Combine(request.DestinationPath, "play.exe"), "remote game");
                File.WriteAllText(Path.Combine(request.DestinationPath, "assets", "data.bin"), "asset");
                return RcloneProcessResult.Success("fixture");
            });
            WithController(runner, (controller, manager, game) =>
            {
                controller.Install(new InstallActionArgs());
                Assert.That(SpinWait.SpinUntil(() => game.IsInstalled, 10000), Is.True);
                CacheOwnershipReceipt receipt;
                Assert.That(receipts.TryRead(cache, "remote-game", out receipt), Is.True);
                Assert.That(receipt.Version, Is.EqualTo(2));
                Assert.That(receipt.InventoryFileCount, Is.EqualTo(2));
                bool matches;
                Assert.That(receipts.TryCompareInventory(cache, "remote-game", destination, out matches), Is.True);
                Assert.That(matches, Is.True);
                File.WriteAllText(Path.Combine(destination, "personal.sav"), "user data");
                Assert.That(receipts.TryCompareInventory(cache, "remote-game", destination, out matches), Is.True);
                Assert.That(matches, Is.False);
                Assert.That(File.ReadAllText(Path.Combine(destination, "personal.sav")), Is.EqualTo("user data"));
                Assert.That(runner.RunCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void RcloneFailure_CleansPartialAndNeverIssuesReceipt()
        {
            var runner = new FixtureRunner(request =>
            {
                Directory.CreateDirectory(request.DestinationPath);
                File.WriteAllText(Path.Combine(request.DestinationPath, "incomplete.bin"), "partial");
                return RcloneProcessResult.Failure("synthetic remote outage");
            });
            WithController(runner, (controller, manager, game) =>
            {
                controller.Install(new InstallActionArgs());
                Assert.That(SpinWait.SpinUntil(() => manager.Jobs.Count > 0 &&
                    manager.Jobs[0].IsTerminal, 10000), Is.True);
                Assert.That(manager.Jobs[0].State, Is.EqualTo(CloudTransferState.Failed));
                Assert.That(Directory.Exists(destination), Is.False);
                Assert.That(Directory.Exists(runner.LastRequest.DestinationPath), Is.False);
                CacheOwnershipReceipt receipt;
                Assert.That(receipts.TryRead(cache, "remote-game", out receipt), Is.False);
                Assert.That(game.IsInstalled, Is.False);
            });
        }

        [Test]
        public void RcloneCancellation_CleansPartialAndNeverIssuesReceipt()
        {
            var runner = new FixtureRunner(request =>
            {
                Directory.CreateDirectory(request.DestinationPath);
                File.WriteAllText(Path.Combine(request.DestinationPath, "incomplete.bin"), "partial");
                return RcloneProcessResult.Cancelled("synthetic cancellation");
            });
            WithController(runner, (controller, manager, game) =>
            {
                controller.Install(new InstallActionArgs());
                Assert.That(SpinWait.SpinUntil(() => manager.Jobs.Count > 0 &&
                    manager.Jobs[0].IsTerminal, 10000), Is.True);
                Assert.That(manager.Jobs[0].State, Is.EqualTo(CloudTransferState.Cancelled));
                Assert.That(Directory.Exists(destination), Is.False);
                Assert.That(Directory.Exists(runner.LastRequest.DestinationPath), Is.False);
                CacheOwnershipReceipt receipt;
                Assert.That(receipts.TryRead(cache, "remote-game", out receipt), Is.False);
                Assert.That(game.IsInstalled, Is.False);
            });
        }

        [Test]
        public void RclonePreexistingDirectory_IsNeverOverwrittenOrAdopted()
        {
            Directory.CreateDirectory(destination);
            var personal = Path.Combine(destination, "personal.sav");
            File.WriteAllText(personal, "keep");
            var runner = new FixtureRunner(request => RcloneProcessResult.Success("unexpected"));
            WithController(runner, (controller, manager, game) =>
            {
                controller.Install(new InstallActionArgs());
                Assert.That(SpinWait.SpinUntil(() => manager.Jobs.Count > 0 &&
                    manager.Jobs[0].IsTerminal, 10000), Is.True);
                Assert.That(manager.Jobs[0].State, Is.EqualTo(CloudTransferState.Failed));
                Assert.That(runner.RunCount, Is.Zero);
                Assert.That(File.ReadAllText(personal), Is.EqualTo("keep"));
                CacheOwnershipReceipt receipt;
                Assert.That(receipts.TryRead(cache, "remote-game", out receipt), Is.False);
            });
        }

        private void WithController(FixtureRunner runner,
            Action<RcloneInstallController, CloudTransferManager, Game> action)
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true,
                LocalCacheFolder = cache,
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneRemoteName = "disposable-fixture",
                RcloneExecutablePath = "fake-rclone",
                RcloneTimeoutSeconds = 30
            };
            var item = new PersonalCloudLibraryItem
            {
                Id = "remote-game", Title = "Remote Fixture",
                SourceType = "directory", SourcePath = "remote/game",
                InstallDirectory = "remote-game", LaunchFile = "play.exe"
            };
            var game = new Game("Remote Fixture")
            {
                Id = Guid.NewGuid(), GameId = item.Id, IsInstalled = false
            };
            var manager = new CloudTransferManager(1);
            using (var queue = new TransferQueueService(manager,
                new CloudTransferExecutor(manager, new LocalTransferAdapter(),
                    new RcloneTransferAdapter(runner))))
            {
                var controller = new RcloneInstallController(null, game, item, settings,
                    queue, ownershipReceipts: receipts);
                try
                {
                    action(controller, manager, game);
                }
                finally
                {
                    controller.Dispose();
                }
            }
        }

        private sealed class FixtureRunner : IRcloneProcessRunner
        {
            private readonly Func<RcloneTransferRequest, RcloneProcessResult> execute;
            public FixtureRunner(Func<RcloneTransferRequest, RcloneProcessResult> execute)
            {
                this.execute = execute;
            }
            public int RunCount { get; private set; }
            public RcloneTransferRequest LastRequest { get; private set; }
            public RcloneProcessResult Run(RcloneTransferRequest request,
                CancellationToken token, Action<long, long?> progress)
            {
                RunCount++;
                LastRequest = request;
                return execute(request);
            }
        }
    }
}
