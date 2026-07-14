using NUnit.Framework;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;

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

            controller.Uninstall(new UninstallActionArgs());

            Assert.That(File.Exists(outside), Is.True);
            Assert.That(deleted, Is.False);
            Assert.That(sink.Messages[0].Text, Does.Contain("refused"));
        }

        [Test]
        public void Uninstall_AskEachTime_FailsClosedWithoutDeleteOrDesktopDialog()
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

            controller.Uninstall(new UninstallActionArgs());

            Assert.That(File.Exists(launch), Is.True);
            Assert.That(deleted, Is.False);
            Assert.That(sink.Messages[0].Text, Does.Contain("deterministic"));
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
            var controller = new RcloneInstallController(
                null,
                game,
                item,
                settings,
                new RcloneFileCopier(),
                new LocalFileCopier(),
                workflowNotifications: notifications);

            controller.Install(new InstallActionArgs());

            Assert.That(game.IsInstalled, Is.False);
            Assert.That(sink.Messages, Has.Count.EqualTo(1));
            Assert.That(sink.Messages[0].Text, Does.Contain("failed"));
            Assert.That(sink.Messages[0].Text, Does.Contain("does not exist"));
        }

        private PersonalCloudLibraryUninstallController CreateUninstallController(
            string launch,
            PersonalCloudLibrarySourceSettingsV3 settings,
            RecordingSink sink,
            SafeCacheDeletionExecutor executor = null)
        {
            var item = new PersonalCloudLibraryItem
            {
                Id = "game",
                Title = "Game",
                CachePath = launch,
                SourcePath = "source.exe"
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
