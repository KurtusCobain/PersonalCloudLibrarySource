using NUnit.Framework;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class MultiSourceControllerFactoryTests
    {
        private string root;
        private string cache;
        private string manifest;
        private string source;
        private Guid pluginId;
        private const string LegacyId = "070fa1c5-c93f-4d8b-a460-b51dc718df9f";

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-SourceActionFactory",
                Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            source = Path.Combine(root, "source", "game.exe");
            manifest = Path.Combine(root, "catalog.json");
            Directory.CreateDirectory(cache);
            Directory.CreateDirectory(Path.GetDirectoryName(source));
            File.WriteAllText(source, "disposable game bytes");
            // Use LocalFolder source path, so manifest needs no rooted paths.
            File.WriteAllText(manifest, "{\"version\":3,\"items\":[{" +
                "\"id\":\"game\",\"title\":\"Test Game\"," +
                "\"sourcePath\":\"source/game.exe\",\"sourceType\":\"file\"," +
                "\"cachePath\":\"test-game/game.exe\"}]}");
            pluginId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private PclsSourceProfileDefinition Profile()
            => new PclsSourceProfileDefinition
            {
                ProfileId = LegacyId,
                DisplayName = "Safe local source",
                IsLegacySingleSource = true,
                ProviderType = "LocalFile",
                LocalManifestPath = manifest
            };

        private PersonalCloudLibrarySourceSettingsV3 Shared()
            => new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true, AllowDownloads = true,
                LocalCacheFolder = cache,
                AllowUninstallOutsideCacheFolder = false,
                UninstallBehavior =
                    PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior
            };

        private MultiSourceRuntimePlan Prepare()
            => new MultiSourceRuntimeCoordinator().Prepare(
                new[] { Profile() }, Shared(), null);

        private Game Game(string gameId = "game")
            => new Game("Test Game")
            {
                Id = Guid.NewGuid(), GameId = gameId,
                PluginId = pluginId, IsInstalled = false
            };

        [Test]
        public void InstalledGameIsRoutedOnlyToMatchingPluginAndSource()
        {
            var plan = Prepare();
            Assert.That(plan.CanPublish, Is.True, plan.RefusalReason);
            var factory = new MultiSourceControllerFactory();
            var manager = new CloudTransferManager(1);
            var receipts = new CacheOwnershipReceiptStore(
                Path.Combine(root, "ownership-receipts"));
            using (var queue = new TransferQueueService(manager,
                new CloudTransferExecutor(manager, new LocalTransferAdapter())))
            {
                InstallController action;
                var correct = Game();
                Assert.That(factory.TryPrepareInstall(null, correct,
                    pluginId, plan, queue, receipts, out action), Is.True);
                Assert.That(action, Is.TypeOf<RcloneInstallController>());
                action.Dispose();

                var wrongPlugin = Game();
                wrongPlugin.PluginId = Guid.NewGuid();
                Assert.That(factory.TryPrepareInstall(null, wrongPlugin,
                    pluginId, plan, queue, receipts, out action), Is.False);
                Assert.That(action, Is.Null);

                Assert.That(factory.TryPrepareInstall(null, Game("not-in-this-library"),
                    pluginId, plan, queue, receipts, out action), Is.False);
                Assert.That(action, Is.Null);

                Assert.That(factory.TryPrepareInstall(null, Game(),
                    pluginId, plan, queue, null, out action), Is.False);
                Assert.That(action, Is.Null);
            }
        }

        [Test]
        public void UninstallIsOfferedOnlyForCacheFileInsideOriginatingSourceScope()
        {
            var plan = Prepare();
            var game = Game();
            var factory = new MultiSourceControllerFactory();
            UninstallController action;

            Assert.That(factory.TryPrepareUninstall(null, game,
                pluginId, plan, out action), Is.False);
            Assert.That(action, Is.Null);

            var destination = Path.Combine(cache, "test-game", "game.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.WriteAllText(destination, "cached bytes");
            Assert.That(factory.TryPrepareUninstall(null, game,
                pluginId, plan, out action), Is.True);
            Assert.That(action, Is.TypeOf<PersonalCloudLibraryUninstallController>());
            action.Dispose();

            Assert.That(File.Exists(destination), Is.True,
                "Merely building an uninstall action must never delete content.");
        }

        [Test]
        public void NewProfileInstallUsesNamespacedReceiptIdThroughFactory()
        {
            var added = Profile();
            added.IsLegacySingleSource = false;
            var plan = new MultiSourceRuntimeCoordinator().Prepare(
                new[] { added }, Shared(), null);
            Assert.That(plan.CanPublish, Is.True, plan.RefusalReason);
            var identity = plan.Games.Single().GameId;
            Assert.That(identity, Does.StartWith("pcls1-"));
            var game = Game(identity);
            var manager = new CloudTransferManager(1);
            var receipts = new CacheOwnershipReceiptStore(
                Path.Combine(root, "ownership-receipts"));

            using (var queue = new TransferQueueService(manager,
                new CloudTransferExecutor(manager, new LocalTransferAdapter())))
            {
                InstallController action;
                Assert.That(new MultiSourceControllerFactory().TryPrepareInstall(
                    null, game, pluginId, plan, queue, receipts, out action), Is.True);
                try
                {
                    action.Install(new InstallActionArgs());
                    CacheOwnershipReceipt record = null, oldRecord;
                    Assert.That(SpinWait.SpinUntil(() =>
                        receipts.TryRead(cache, identity, out record), 10000), Is.True);
                    Assert.That(record.ItemId, Is.EqualTo(identity));
                    Assert.That(receipts.TryRead(cache, "game", out oldRecord), Is.False);
                    Assert.That(File.ReadAllText(
                        Path.Combine(cache, "test-game", "game.exe")),
                        Is.EqualTo("disposable game bytes"));
                }
                finally
                {
                    action.Dispose();
                }
            }
        }
    }
}
