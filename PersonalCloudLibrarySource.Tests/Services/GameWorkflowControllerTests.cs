using NUnit.Framework;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class GameWorkflowControllerTests
    {
        private string root;
        private string cache;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ControllerBoundaryTests", Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void Uninstall_AuthorizedCachedFile_DeletesAndNotifiesWithoutApiDialogs()
        {
            var launch = CreateFile(Path.Combine(cache, "game", "play.exe"));
            var sink = new RecordingSink();
            var settings = FileOnlySettings();
            settings.TreatMissingFilesAsUninstalled = false;
            var controller = CreateUninstallController(launch, settings, sink);

            controller.Uninstall(new UninstallActionArgs());

            Assert.That(File.Exists(launch), Is.False);
            Assert.That(sink.Messages, Has.Count.EqualTo(1));
            Assert.That(sink.Messages[0].Text, Does.Contain("completed"));
        }

        [Test]
        public void Uninstall_UnsafeOutsideTarget_RefusesWithoutDeleteAndNotifies()
        {
            var outside = CreateFile(Path.Combine(root, "outside", "play.exe"));
            var sink = new RecordingSink();
            var deleted = false;
            var executor = new SafeCacheDeletionExecutor(
                new SafeCacheDeletionPolicy(),
                ignored => deleted = true,
                ignored => deleted = true);
            var controller = CreateUninstallController(outside, FileOnlySettings(), sink, executor);

            Assert.Throws<InvalidOperationException>(() => controller.Uninstall(new UninstallActionArgs()));

            Assert.That(File.Exists(outside), Is.True);
            Assert.That(deleted, Is.False);
            Assert.That(sink.Messages[0].Text, Does.Contain("refused"));
        }

        [Test]
        public void Uninstall_ExplicitOutsideCachePermission_NeverDeletesOriginalLocalSource()
        {
            var source = CreateFile(Path.Combine(root, "original", "play.exe"));
            var sink = new RecordingSink();
            var settings = FileOnlySettings();
            settings.AllowUninstallOutsideCacheFolder = true;
            settings.SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType;
            var deleted = false;
            var executor = new SafeCacheDeletionExecutor(
                new SafeCacheDeletionPolicy(),
                ignored => deleted = true,
                ignored => deleted = true);
            var controller = CreateUninstallController(source, settings, sink, executor, source);

            Assert.Throws<InvalidOperationException>(() => controller.Uninstall(new UninstallActionArgs()));

            Assert.That(File.Exists(source), Is.True);
            Assert.That(deleted, Is.False);
            Assert.That(sink.Messages[0].Text, Does.Contain("refused"));
        }

        [Test]
        public void Uninstall_ExplicitOutsideCachePermission_RefusesAncestorOfOriginalSource()
        {
            var source = CreateFile(Path.Combine(root, "original", "sub", "play.exe"));
            var targetFolder = Path.Combine(root, "original");
            var sink = new RecordingSink();
            var settings = FileOnlySettings();
            settings.AllowUninstallOutsideCacheFolder = true;
            var deleted = false;
            var executor = new SafeCacheDeletionExecutor(
                new SafeCacheDeletionPolicy(),
                ignored => deleted = true,
                ignored => deleted = true);
            var controller = CreateUninstallController(targetFolder, settings, sink, executor, source);
            settings.UninstallBehavior = PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior;

            Assert.Throws<InvalidOperationException>(() => controller.Uninstall(new UninstallActionArgs()));

            Assert.That(File.Exists(source), Is.True);
            Assert.That(deleted, Is.False);
        }

        [Test]
        public void Uninstall_OutsideCachePermission_UnknownLocalSourceFailsClosed()
        {
            var external = CreateFile(Path.Combine(root, "external", "play.exe"));
            var sink = new RecordingSink();
            var settings = FileOnlySettings();
            settings.AllowUninstallOutsideCacheFolder = true;
            var deleted = false;
            var executor = new SafeCacheDeletionExecutor(
                new SafeCacheDeletionPolicy(),
                ignored => deleted = true,
                ignored => deleted = true);
            var controller = CreateUninstallController(external, settings, sink, executor, string.Empty);

            Assert.Throws<InvalidOperationException>(() => controller.Uninstall(new UninstallActionArgs()));

            Assert.That(File.Exists(external), Is.True);
            Assert.That(deleted, Is.False);
            Assert.That(sink.Messages[0].Text, Does.Contain("refused"));
        }

        [Test]
        public void Uninstall_LegacyAskEachTime_RefusesUnownedFileParentFolder()
        {
            var launch = CreateFile(Path.Combine(cache, "game", "play.exe"));
            var settings = FileOnlySettings();
            settings.UninstallBehavior = PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior;
            var sink = new RecordingSink();
            var deleted = false;
            var executor = new SafeCacheDeletionExecutor(
                new SafeCacheDeletionPolicy(),
                ignored => deleted = true,
                ignored => deleted = true);
            var controller = CreateUninstallController(launch, settings, sink, executor);

            Assert.Throws<InvalidOperationException>(() => controller.Uninstall(new UninstallActionArgs()));

            Assert.That(deleted, Is.False);
            Assert.That(File.Exists(launch), Is.True);
            Assert.That(sink.Messages[0].Text, Does.Contain("refused"));
        }

        [Test]
        public void Uninstall_FileEntryFolderMode_ProtectsUnrelatedSibling()
        {
            var launch = CreateFile(Path.Combine(cache, "shared", "play.exe"));
            var sibling = CreateFile(Path.Combine(cache, "shared", "personal.txt"));
            var settings = FileOnlySettings();
            settings.UninstallBehavior = PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior;
            var controller = CreateUninstallController(launch, settings, new RecordingSink());

            Assert.Throws<InvalidOperationException>(() => controller.Uninstall(new UninstallActionArgs()));
            Assert.That(File.Exists(launch), Is.True);
            Assert.That(File.Exists(sibling), Is.True);
        }

        [Test]
        public void Uninstall_DirectoryItemWithoutVerifiedOwnership_LeavesAllFilesIntact()
        {
            var launch = CreateFile(Path.Combine(cache, "directory-game", "play.exe"));
            var personal = CreateFile(Path.Combine(cache, "directory-game", "personal.txt"));
            var settings = FileOnlySettings();
            settings.UninstallBehavior = PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior;
            var item = new PersonalCloudLibraryItem
            {
                Id = "directory-game", Title = "Directory Game",
                SourceType = "directory", SourcePath = "directory-game",
                InstallDirectory = "directory-game", LaunchFile = "play.exe"
            };
            var sink = new RecordingSink();
            var controller = new PersonalCloudLibraryUninstallController(
                null, Game("directory-game"), item, settings,
                new GameWorkflowNotificationService(sink));

            Assert.Throws<InvalidOperationException>(
                () => controller.Uninstall(new UninstallActionArgs()));
            Assert.That(File.Exists(launch), Is.True);
            Assert.That(File.Exists(personal), Is.True);
            Assert.That(sink.Messages[0].Text, Does.Contain("refused"));
        }

        [Test]
        public void Install_OutsideCacheDestination_BlocksBeforeQueueing()
        {
            var game = Game("blocked-destination");
            game.IsInstalled = false;
            var settings = FileOnlySettings();
            settings.SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType;
            var outside = Path.Combine(root, "external", "play.exe");
            var item = new PersonalCloudLibraryItem
            {
                Id = "blocked-destination", Title = "Blocked",
                SourcePath = outside, CachePath = outside
            };
            var sink = new RecordingSink();
            var manager = new CloudTransferManager(1);
            using (var queue = new TransferQueueService(manager,
                new CloudTransferExecutor(manager, new LocalTransferAdapter())))
            {
                var controller = new RcloneInstallController(null, game, item, settings, queue,
                    new GameWorkflowNotificationService(sink));
                controller.Install(new InstallActionArgs());
                Assert.That(manager.Jobs, Is.Empty, "Unsafe destinations must never queue an install.");
                Assert.That(sink.Messages, Has.Count.EqualTo(1));
                Assert.That(sink.Messages[0].Text, Does.Contain("inside the configured local cache"));
                Assert.That(game.IsInstalled, Is.False);
                controller.Dispose();
            }
        }

        [Test]
        public void Install_TransferFailure_NotifiesWithoutPlayniteApiOrDesktopDialog()
        {
            var missingSource = Path.Combine(root, "source", "missing.exe");
            var game = Game("game");
            game.IsInstalled = false;
            var item = new PersonalCloudLibraryItem
            {
                Id = "game",
                Title = "Game",
                SourcePath = missingSource,
                CachePath = Path.Combine("game", "play.exe")
            };
            var settings = FileOnlySettings();
            settings.SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType;
            var sink = new RecordingSink();
            var notifications = new GameWorkflowNotificationService(sink);
            var manager = new CloudTransferManager(1);
            using (var queue = new TransferQueueService(manager, new CloudTransferExecutor(manager, new LocalTransferAdapter())))
            {
                var controller = new RcloneInstallController(
                    null,
                    game,
                    item,
                    settings,
                    queue,
                    workflowNotifications: notifications);

                controller.Install(new InstallActionArgs());
                Assert.That(SpinWait.SpinUntil(() => sink.Messages.Count > 0, 5000), Is.True,
                    "The completion observer should publish a failure without a UI dialog.");
            }

            Assert.That(game.IsInstalled, Is.False);
            Assert.That(sink.Messages, Has.Count.EqualTo(1));
            Assert.That(sink.Messages[0].Text, Does.Contain("failed"));
            Assert.That(sink.Messages[0].Text, Does.Contain("does not exist"));
        }

        [Test]
        public void Install_SlowTransfer_ReturnsImmediatelyAndCompletesAfterRelease()
        {
            var source = CreateFile(Path.Combine(root, "original", "play.exe"));
            var game = Game("slow");
            game.IsInstalled = false;
            var settings = FileOnlySettings();
            settings.SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType;
            var destination = Path.Combine(cache, "slow", "play.exe");
            var item = new PersonalCloudLibraryItem
            {
                Id = "slow", Title = "Slow Game", SourcePath = source,
                CachePath = Path.Combine("slow", "play.exe")
            };
            var manager = new CloudTransferManager(1);
            var executor = new PausingTransferExecutor(manager);
            var sink = new RecordingSink();
            using (var queue = new TransferQueueService(manager, executor))
            {
                var controller = new RcloneInstallController(null, game, item, settings, queue,
                    new GameWorkflowNotificationService(sink));
                var stopwatch = Stopwatch.StartNew();
                controller.Install(new InstallActionArgs());
                stopwatch.Stop();
                Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1000),
                    "Install must enqueue and return instead of waiting for a network transfer.");
                Assert.That(executor.Started.Wait(TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(game.IsInstalled, Is.False);

                executor.Release.Set();
                Assert.That(SpinWait.SpinUntil(() => game.IsInstalled && sink.Messages.Count > 0, 5000), Is.True);
                Assert.That(File.Exists(destination), Is.True);
                controller.Dispose();
            }
        }

        [Test]
        public void Install_SuccessfulTransfer_RecordsReceiptWithoutGrantingDeletion()
        {
            var source = CreateFile(Path.Combine(root, "original", "play.exe"));
            var game = Game("receipt-game");
            game.IsInstalled = false;
            var settings = FileOnlySettings();
            settings.SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType;
            var item = new PersonalCloudLibraryItem
            {
                Id = "receipt-game", Title = "Receipt Game",
                SourcePath = source, CachePath = "receipt-game/play.exe"
            };
            var receipts = new CacheOwnershipReceiptStore(Path.Combine(root, "ownership-receipts"));
            var manager = new CloudTransferManager(1);
            using (var queue = new TransferQueueService(manager, new CloudTransferExecutor(manager, new LocalTransferAdapter())))
            {
                var controller = new RcloneInstallController(null, game, item, settings, queue,
                    ownershipReceipts: receipts);
                controller.Install(new InstallActionArgs());
                Assert.That(SpinWait.SpinUntil(() => game.IsInstalled, 5000), Is.True);
                CacheOwnershipReceipt receipt = null;
                Assert.That(SpinWait.SpinUntil(() => receipts.TryRead(cache, item.Id, out receipt), 5000), Is.True);
                Assert.That(receipt.Destination, Is.EqualTo(Path.Combine(cache, "receipt-game", "play.exe")));
                controller.Dispose();
            }
        }

        [Test]
        public void Install_PreexistingCacheFile_DoesNotCreateOwnershipReceipt()
        {
            var source = CreateFile(Path.Combine(root, "original", "play.exe"));
            var existing = CreateFile(Path.Combine(cache, "already", "play.exe"));
            File.WriteAllText(existing, "personal content");
            var game = Game("already");
            game.IsInstalled = false;
            var settings = FileOnlySettings();
            settings.SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType;
            var item = new PersonalCloudLibraryItem
            {
                Id = "already", Title = "Existing File",
                SourcePath = source, CachePath = "already/play.exe"
            };
            var receipts = new CacheOwnershipReceiptStore(Path.Combine(root, "ownership-receipts"));
            var sink = new RecordingSink();
            var manager = new CloudTransferManager(1);
            using (var queue = new TransferQueueService(manager,
                new CloudTransferExecutor(manager, new LocalTransferAdapter())))
            {
                var controller = new RcloneInstallController(null, game, item, settings, queue,
                    new GameWorkflowNotificationService(sink), receipts);
                controller.Install(new InstallActionArgs());
                Assert.That(SpinWait.SpinUntil(() => manager.Jobs.Count > 0 &&
                    manager.Jobs[0].IsTerminal, 5000), Is.True);
                Assert.That(manager.Jobs[0].State, Is.EqualTo(CloudTransferState.Failed));
                CacheOwnershipReceipt receipt;
                Assert.That(receipts.TryRead(cache, item.Id, out receipt), Is.False);
                Assert.That(File.ReadAllText(existing), Is.EqualTo("personal content"));
                controller.Dispose();
            }
        }

        [Test]
        public void Install_PreexistingDirectory_DoesNotIssueV2OwnershipReceipt()
        {
            var source = Path.Combine(root, "original", "directory");
            Directory.CreateDirectory(source);
            CreateFile(Path.Combine(source, "play.exe"));
            var existing = CreateFile(Path.Combine(cache, "already-dir", "personal.txt"));
            File.WriteAllText(existing, "personal data");
            var game = Game("already-dir");
            game.IsInstalled = false;
            var settings = FileOnlySettings();
            settings.SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFolderProviderType;
            settings.LocalLibraryRoot = Path.Combine(root, "original");
            var item = new PersonalCloudLibraryItem
            {
                Id = "already-dir", Title = "Existing Folder",
                SourceType = "directory", SourcePath = "directory",
                InstallDirectory = "already-dir", LaunchFile = "play.exe"
            };
            var receipts = new CacheOwnershipReceiptStore(Path.Combine(root, "ownership-receipts"));
            var manager = new CloudTransferManager(1);
            using (var queue = new TransferQueueService(manager,
                new CloudTransferExecutor(manager, new LocalTransferAdapter())))
            {
                var controller = new RcloneInstallController(null, game, item, settings, queue,
                    ownershipReceipts: receipts);
                controller.Install(new InstallActionArgs());
                Assert.That(SpinWait.SpinUntil(() => manager.Jobs.Count > 0 &&
                    manager.Jobs[0].IsTerminal, 5000), Is.True);
                Assert.That(manager.Jobs[0].State, Is.EqualTo(CloudTransferState.Failed));
                CacheOwnershipReceipt receipt;
                Assert.That(receipts.TryRead(cache, item.Id, out receipt), Is.False);
                Assert.That(File.ReadAllText(existing), Is.EqualTo("personal data"));
                controller.Dispose();
            }
        }

        [Test]
        public void Install_DisposedController_CancelsActiveTransferAndDoesNotMarkInstalled()
        {
            var source = CreateFile(Path.Combine(root, "original", "play.exe"));
            var game = Game("dispose");
            game.IsInstalled = false;
            var settings = FileOnlySettings();
            settings.SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType;
            var item = new PersonalCloudLibraryItem
            {
                Id = "dispose", Title = "Cancelled", SourcePath = source,
                CachePath = Path.Combine("dispose", "play.exe")
            };
            var manager = new CloudTransferManager(1);
            var executor = new PausingTransferExecutor(manager);
            using (var queue = new TransferQueueService(manager, executor))
            {
                var controller = new RcloneInstallController(null, game, item, settings, queue);
                controller.Install(new InstallActionArgs());
                Assert.That(executor.Started.Wait(TimeSpan.FromSeconds(3)), Is.True);
                controller.Dispose();
                executor.Release.Set();
                Assert.That(SpinWait.SpinUntil(() => manager.Jobs[0].IsTerminal, 5000), Is.True);
                Assert.That(manager.Jobs[0].State, Is.EqualTo(CloudTransferState.Cancelled));
                Assert.That(game.IsInstalled, Is.False);
            }
        }

        private sealed class PausingTransferExecutor : ICloudTransferExecutor
        {
            private readonly CloudTransferManager manager;
            public ManualResetEventSlim Started { get; } = new ManualResetEventSlim(false);
            public ManualResetEventSlim Release { get; } = new ManualResetEventSlim(false);

            public PausingTransferExecutor(CloudTransferManager manager) { this.manager = manager; }

            public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
            {
                var job = manager.GetJob(jobId);
                manager.Transition(jobId, CloudTransferState.Transferring);
                Started.Set();
                if (!Release.Wait(TimeSpan.FromSeconds(8)))
                    return CloudTransferExecutionResult.Failure("Timed out in test executor.");
                if (job.CancellationToken.IsCancellationRequested)
                    return CloudTransferExecutionResult.CancelledResult();
                Directory.CreateDirectory(Path.GetDirectoryName(job.Destination));
                File.WriteAllText(job.Destination, "test file");
                return CloudTransferExecutionResult.Success(9, 9);
            }

            public CloudTransferExecutionResult ExecuteRclone(Guid jobId, PersonalCloudLibrarySourceSettings settings)
            {
                return ExecuteLocal(jobId, false);
            }
        }

        private PersonalCloudLibraryUninstallController CreateUninstallController(
            string launch,
            PersonalCloudLibrarySourceSettingsV3 settings,
            RecordingSink sink,
            SafeCacheDeletionExecutor executor = null,
            string sourcePath = "source.exe")
        {
            var item = new PersonalCloudLibraryItem
            {
                Id = "game",
                Title = "Game",
                CachePath = launch,
                SourcePath = sourcePath
            };
            return new PersonalCloudLibraryUninstallController(
                null,
                Game("game"),
                item,
                settings,
                new GameWorkflowNotificationService(sink),
                executor);
        }

        private PersonalCloudLibrarySourceSettingsV3 FileOnlySettings()
        {
            return new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true,
                LocalCacheFolder = cache,
                UninstallBehavior = PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior,
                TreatMissingFilesAsUninstalled = true
            };
        }

        private static Game Game(string id) => new Game("Game") { Id = Guid.NewGuid(), GameId = id, IsInstalled = true };

        private static string CreateFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "stub");
            return path;
        }

        private sealed class RecordingSink : IGameWorkflowNotificationSink
        {
            public List<Playnite.SDK.NotificationMessage> Messages { get; } = new List<Playnite.SDK.NotificationMessage>();
            public void Add(Playnite.SDK.NotificationMessage message) => Messages.Add(message);
            public void Remove(string id) { }
        }
    }
}
