using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class ManifestItemMapperTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-ManifestMapperTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }

        [Test]
        public void Map_RemoteOnlyItem_IsUninstalledWithoutPlayAction()
        {
            var item = new PersonalCloudLibraryItem { Id = "one", Title = "One", SourcePath = "one.exe", CachePath = "one.exe" };
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneRemoteName = "games",
                LocalCacheFolder = Path.Combine(testRoot, "cache"),
                TreatMissingFilesAsUninstalled = true,
                AllowDownloads = true
            };

            var games = new ManifestItemMapper().Map(new[] { item }, settings, new List<string>());

            Assert.That(games.Count, Is.EqualTo(1));
            Assert.That(games[0].IsInstalled, Is.False);
            Assert.That(games[0].GameActions, Is.Null.Or.Empty);
        }

        [Test]
        public void Map_CachedDirectoryWithoutLaunchFile_IsInstalledButNotPlayable()
        {
            var directory = Path.Combine(testRoot, "cache", "one");
            Directory.CreateDirectory(directory);
            var item = new PersonalCloudLibraryItem
            {
                Id = "one",
                Title = "One",
                SourceType = "directory",
                InstallDirectory = "one",
                LaunchFile = "missing.exe"
            };
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                LocalCacheFolder = Path.Combine(testRoot, "cache"),
                TreatMissingFilesAsUninstalled = true
            };

            var games = new ManifestItemMapper().Map(new[] { item }, settings, new List<string>());

            Assert.That(games[0].IsInstalled, Is.True);
            Assert.That(games[0].GameActions, Is.Null.Or.Empty);
        }

        [Test]
        public void Map_RemoteDirectory_DoesNotTreatExistingCacheRootAsItsInstall()
        {
            var cacheRoot = Path.Combine(testRoot, "cache");
            Directory.CreateDirectory(cacheRoot);
            var item = new PersonalCloudLibraryItem
            {
                Id = "remote-directory",
                Title = "Remote Directory",
                SourceType = "directory",
                SourcePath = "remote-directory"
            };
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneRemoteName = "games",
                LocalCacheFolder = cacheRoot,
                TreatMissingFilesAsUninstalled = true
            };

            var games = new ManifestItemMapper().Map(new[] { item }, settings, new List<string>());

            Assert.That(games[0].IsInstalled, Is.False);
            Assert.That(games[0].InstallDirectory, Is.EqualTo(Path.Combine(cacheRoot, item.Id)));
        }

        [Test]
        public void Map_CachedFile_ExposesStandardPlayPathAndWorkingDirectory()
        {
            var cache = Path.Combine(testRoot, "cache");
            var launch = Path.Combine(cache, "one", "play.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(launch));
            File.WriteAllText(launch, "stub");
            var item = new PersonalCloudLibraryItem { Id = "one", Title = "One", CachePath = "one/play.exe" };
            var settings = new PersonalCloudLibrarySourceSettingsV3 { LocalCacheFolder = cache, TreatMissingFilesAsUninstalled = true };

            var game = new ManifestItemMapper().Map(new[] { item }, settings, new List<string>())[0];

            Assert.That(game.IsInstalled, Is.True);
            Assert.That(game.GameActions, Has.Count.EqualTo(1));
            Assert.That(game.GameActions[0].IsPlayAction, Is.True);
            Assert.That(game.GameActions[0].Path, Is.EqualTo(launch));
            Assert.That(game.GameActions[0].WorkingDir, Is.EqualTo(Path.GetDirectoryName(launch)));
        }
    }
}
