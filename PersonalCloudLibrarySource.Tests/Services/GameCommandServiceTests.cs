using NUnit.Framework;
using Playnite.SDK.Models;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class GameCommandServiceTests
    {
        private string testRoot;
        private readonly Guid pluginId = Guid.Parse("61993828-67a8-4468-93a2-293442e36328");

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-GameCommandTests", Guid.NewGuid().ToString("N"));
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
        public void ResolveTargets_LocalCachedGame_ProvidesOpenAndSafeRemoveActions()
        {
            var sourceRoot = Path.Combine(testRoot, "source");
            var cacheRoot = Path.Combine(testRoot, "cache");
            Directory.CreateDirectory(sourceRoot);
            Directory.CreateDirectory(Path.Combine(cacheRoot, "Game"));
            File.WriteAllText(Path.Combine(sourceRoot, "game.exe"), "source");
            File.WriteAllText(Path.Combine(cacheRoot, "Game", "game.exe"), "cached");

            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                LocalLibraryRoot = sourceRoot,
                LocalCacheFolder = cacheRoot,
                AllowDownloads = true,
                UninstallBehavior = PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior
            };
            var game = new Game("Example")
            {
                Id = Guid.NewGuid(),
                GameId = "example",
                PluginId = pluginId,
                IsInstalled = true
            };
            var item = new PersonalCloudLibraryItem
            {
                Id = "example",
                Title = "Example",
                SourcePath = "game.exe",
                CachePath = Path.Combine("Game", "game.exe"),
                SourceType = "file"
            };

            var target = new GameCommandService().ResolveTargets(new[] { game }, new[] { item }, settings, pluginId)[0];

            Assert.That(target.PolicyContext.HasCachedPath, Is.True);
            Assert.That(target.PolicyContext.CanOpenSourceLocation, Is.True);
            Assert.That(target.PolicyContext.CanRemoveCachedCopy, Is.True);
            Assert.That(target.ResolvedLocalSourcePath, Is.EqualTo(Path.Combine(sourceRoot, "game.exe")));
            Assert.That(target.SafeUninstallTarget, Is.EqualTo(Path.Combine(cacheRoot, "Game", "game.exe")));
        }

        [Test]
        public void ResolveTargets_RcloneRemoteOnlyGame_RemainsInstallableWithoutOpenSourceAction()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneExecutablePath = "rclone",
                RcloneRemoteName = "games",
                RcloneContentRoot = "library",
                RcloneManifestPath = "manifest.json",
                LocalCacheFolder = Path.Combine(testRoot, "cache"),
                AllowDownloads = true
            };
            var game = new Game("Remote")
            {
                Id = Guid.NewGuid(),
                GameId = "remote",
                PluginId = pluginId,
                IsInstalled = false
            };
            var item = new PersonalCloudLibraryItem
            {
                Id = "remote",
                Title = "Remote",
                SourcePath = "PC/remote.zip",
                CachePath = Path.Combine("PC", "remote.zip")
            };

            var target = new GameCommandService().ResolveTargets(new[] { game }, new[] { item }, settings, pluginId)[0];

            Assert.That(target.PolicyContext.CanInstall, Is.True);
            Assert.That(target.PolicyContext.CanOpenSourceLocation, Is.False);
            Assert.That(target.SourceDisplayPath, Is.EqualTo("games:library/PC/remote.zip"));
        }

        [Test]
        public void ResolveTargets_CachedPathOutsideManagedCache_IsNeverRemovableByDefault()
        {
            var outsidePath = Path.Combine(testRoot, "outside.exe");
            File.WriteAllText(outsidePath, "outside");
            var cacheRoot = Path.Combine(testRoot, "cache");
            Directory.CreateDirectory(cacheRoot);

            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalCacheFolder = cacheRoot,
                AllowUninstallOutsideCacheFolder = false,
                UninstallBehavior = PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior
            };
            var game = new Game("Outside")
            {
                Id = Guid.NewGuid(),
                GameId = "outside",
                PluginId = pluginId,
                IsInstalled = true
            };
            var item = new PersonalCloudLibraryItem
            {
                Id = "outside",
                Title = "Outside",
                SourcePath = "outside.exe",
                CachePath = outsidePath
            };

            var target = new GameCommandService().ResolveTargets(new[] { game }, new[] { item }, settings, pluginId)[0];

            Assert.That(target.PolicyContext.HasCachedPath, Is.True);
            Assert.That(target.PolicyContext.CanRemoveCachedCopy, Is.False);
            Assert.That(target.UninstallRefusalReason, Does.Contain("outside LocalCacheFolder"));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void ResolveTargets_MissingContent_UsesConfiguredInstalledSemantics(
            bool treatMissingAsUninstalled,
            bool expectedInstalled)
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneRemoteName = "games",
                LocalCacheFolder = Path.Combine(testRoot, "cache"),
                AllowDownloads = true,
                TreatMissingFilesAsUninstalled = treatMissingAsUninstalled
            };
            var game = new Game("Remote")
            {
                GameId = "remote",
                PluginId = pluginId
            };
            var item = new PersonalCloudLibraryItem
            {
                Id = "remote",
                Title = "Remote",
                SourcePath = "remote.exe",
                CachePath = "remote.exe",
                SourceType = "file"
            };

            var target = new GameCommandService().ResolveTargets(new[] { game }, new[] { item }, settings, pluginId)[0];

            Assert.That(target.PolicyContext.IsInstalled, Is.EqualTo(expectedInstalled));
            Assert.That(target.PolicyContext.HasCachedPath, Is.False);
            Assert.That(target.PolicyContext.CanInstall, Is.True);
        }
    }
}
