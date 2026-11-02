using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class MultiSourceRuntimeCoordinatorTests
    {
        private string root;
        private string cache;
        private string firstManifest;
        private string secondManifest;
        private const string LegacyId = "770c8eb6-9bd1-4189-b555-18a94425eaf1";
        private const string NewId = "d8ea26ab-0ed1-4f22-922d-22b6036ac97b";

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(),
                "PCLS-MultiSourceRouting", Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            firstManifest = Path.Combine(root, "first.json");
            secondManifest = Path.Combine(root, "second.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private PersonalCloudLibrarySourceSettingsV3 Shared()
            => new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true, LocalCacheFolder = cache,
                AllowUninstallOutsideCacheFolder = false
            };

        private static PclsSourceProfileDefinition Profile(
            string id, string path, bool legacy = false)
            => new PclsSourceProfileDefinition
            {
                ProfileId = id, DisplayName = "Test source " + id,
                ProviderType = "LocalFile", IsLegacySingleSource = legacy,
                LocalManifestPath = path
            };

        private static void SaveManifest(string path, string id,
            string target, string sourceType = "file", string installRoot = null)
        {
            File.WriteAllText(path, "{\"version\":3,\"items\":[{" +
                "\"id\":\"" + id + "\",\"title\":\"" + id + "\"," +
                "\"sourcePath\":\"game.exe\",\"sourceType\":\"" + sourceType + "\"," +
                "\"cachePath\":\"" + target + "\"" +
                (installRoot == null ? "" : ",\"installDirectory\":\"" + installRoot + "\"") +
                "}]}");
        }

        [Test]
        public void TwoSourcesWithSameOriginalIdResolveToOwnSettingsWithoutChangingOldId()
        {
            SaveManifest(firstManifest, "same", "legacy/game.exe");
            SaveManifest(secondManifest, "same", "remote/game.exe");
            var first = Profile(LegacyId, firstManifest, true);
            var second = Profile(NewId, secondManifest);
            var shared = Shared();
            var coordinator = new MultiSourceRuntimeCoordinator();

            var result = coordinator.Prepare(new[] { first, second }, shared, null);

            Assert.That(result.CanPublish, Is.True, result.RefusalReason);
            Assert.That(result.Games, Has.Count.EqualTo(2));
            Assert.That(result.Games[0].GameId, Is.EqualTo("same"));
            Assert.That(result.Games[1].GameId, Does.StartWith("pcls1-"));
            SourceScopedGameAction legacy;
            SourceScopedGameAction added;
            Assert.That(result.TryResolveAction("same", out legacy), Is.True);
            Assert.That(result.TryResolveAction(result.Games[1].GameId, out added), Is.True);
            Assert.That(legacy.ProfileId, Is.EqualTo(LegacyId));
            Assert.That(added.ProfileId, Is.EqualTo(NewId));
            Assert.That(legacy.OriginalItemId, Is.EqualTo("same"));
            Assert.That(added.OriginalItemId, Is.EqualTo("same"));
            Assert.That(legacy.CreateItemSnapshot().CachePath, Is.EqualTo("legacy/game.exe"));
            Assert.That(added.CreateItemSnapshot().CachePath, Is.EqualTo("remote/game.exe"));
            Assert.That(legacy.CreateSettingsSnapshot().LocalManifestPath, Is.EqualTo(firstManifest));
            Assert.That(added.CreateSettingsSnapshot().LocalManifestPath, Is.EqualTo(secondManifest));
        }

        [Test]
        public void PublishedActionRoutesAreIndependentOfSubsequentProfileAndSnapshotEdits()
        {
            SaveManifest(firstManifest, "game", "legacy/game.exe");
            var first = Profile(LegacyId, firstManifest, true);
            var shared = Shared();
            var result = new MultiSourceRuntimeCoordinator().Prepare(
                new[] { first }, shared, null);
            Assert.That(result.CanPublish, Is.True, result.RefusalReason);
            SourceScopedGameAction original;
            Assert.That(result.TryResolveAction("game", out original), Is.True);

            first.LocalManifestPath = secondManifest;
            shared.LocalCacheFolder = Path.Combine(root, "wrong");
            var modifiedItem = original.CreateItemSnapshot();
            modifiedItem.CachePath = "../other.exe";
            var modifiedSettings = original.CreateSettingsSnapshot();
            modifiedSettings.LocalCacheFolder = @"C:\SomewhereElse";

            Assert.That(original.CreateItemSnapshot().CachePath, Is.EqualTo("legacy/game.exe"));
            Assert.That(original.CreateSettingsSnapshot().LocalCacheFolder, Is.EqualTo(cache));
            Assert.That(original.CreateSettingsSnapshot().LocalManifestPath,
                Is.EqualTo(firstManifest));
            SourceScopedGameAction missing;
            Assert.That(result.TryResolveAction("unknown", out missing), Is.False);
        }

        [Test]
        public void LocalAndRcloneProfilesKeepTheirOwnSourceSpecificActionSettings()
        {
            SaveManifest(firstManifest, "game", "local/game.exe");
            var local = Profile(LegacyId, firstManifest, true);
            var remote = new PclsSourceProfileDefinition
            {
                ProfileId = NewId, DisplayName = "Disposable cloud",
                ProviderType = "RcloneRemote",
                RcloneExecutablePath = @"C:\Tools\rclone.exe",
                RcloneRemoteName = "sandbox",
                RcloneManifestPath = "catalog.json",
                RcloneContentRoot = "games",
                RcloneTimeoutSeconds = 121
            };
            var readCount = 0;
            var result = new MultiSourceRuntimeCoordinator().Prepare(
                new[] { local, remote }, Shared(), settings =>
                {
                    readCount++;
                    Assert.That(settings.RcloneRemoteName, Is.EqualTo("sandbox"));
                    Assert.That(settings.RcloneTimeoutSeconds, Is.EqualTo(121));
                    return "{\"version\":3,\"items\":[{\"id\":\"game\"," +
                        "\"title\":\"Remote Game\",\"sourceType\":\"file\"," +
                        "\"remotePath\":\"cloud/game.exe\"," +
                        "\"cachePath\":\"remote/game.exe\"}]}";
                });

            Assert.That(result.CanPublish, Is.True, result.RefusalReason);
            Assert.That(readCount, Is.EqualTo(1));
            Assert.That(result.Games, Has.Count.EqualTo(2));
            SourceScopedGameAction sourceRoute;
            var cloudId = result.Games.Single(x => x.GameId != "game").GameId;
            Assert.That(result.TryResolveAction(cloudId, out sourceRoute), Is.True);
            Assert.That(sourceRoute.CreateSettingsSnapshot().SourceProviderType,
                Is.EqualTo("RcloneRemote"));
            Assert.That(sourceRoute.CreateSettingsSnapshot().RcloneRemoteName,
                Is.EqualTo("sandbox"));
            Assert.That(sourceRoute.CreateSettingsSnapshot().RcloneContentRoot,
                Is.EqualTo("games"));
            Assert.That(sourceRoute.CreateItemSnapshot().RemotePath,
                Is.EqualTo("cloud/game.exe"));
            Assert.That(sourceRoute.CreateSettingsSnapshot().LocalCacheFolder,
                Is.EqualTo(cache));
        }

        [Test]
        public void TwoSourceIdsCanStillCollideOnOneCacheFileAndMustNotPublish()
        {
            SaveManifest(firstManifest, "legacy", "shared/game.exe");
            SaveManifest(secondManifest, "other", "shared/game.exe");
            var result = new MultiSourceRuntimeCoordinator().Prepare(new[]
            {
                Profile(LegacyId, firstManifest, true), Profile(NewId, secondManifest)
            }, Shared(), null);

            Assert.That(result.CanPublish, Is.False);
            Assert.That(result.Games, Is.Empty);
            Assert.That(result.RefusalReason, Does.Contain("same cache destination"));
            SourceScopedGameAction action;
            Assert.That(result.TryResolveAction("legacy", out action), Is.False);
        }

        [Test]
        public void DirectorySourceCannotOwnParentOfAnotherProfileFile()
        {
            SaveManifest(firstManifest, "folder", "shared/launcher.exe",
                "directory", "shared");
            SaveManifest(secondManifest, "file", "shared/extra.exe");
            var result = new MultiSourceRuntimeCoordinator().Prepare(new[]
            {
                Profile(LegacyId, firstManifest, true), Profile(NewId, secondManifest)
            }, Shared(), null);

            Assert.That(result.CanPublish, Is.False);
            Assert.That(result.RefusalReason, Does.Contain("overlapping cache"));
            Assert.That(result.Games, Is.Empty);
        }

        [Test]
        public void OfflineManifestAbortsWholePublicationAndRecoversAfterReconnect()
        {
            SaveManifest(firstManifest, "legacy", "old/game.exe");
            var sources = new[]
            {
                Profile(LegacyId, firstManifest, true),
                Profile(NewId, secondManifest)
            };
            var coordinator = new MultiSourceRuntimeCoordinator();
            var offline = coordinator.Prepare(sources, Shared(), null);
            Assert.That(offline.CanPublish, Is.False);
            Assert.That(offline.Games, Is.Empty);
            SourceScopedGameAction missing;
            Assert.That(offline.TryResolveAction("legacy", out missing), Is.False);

            SaveManifest(secondManifest, "new", "new/game.exe");
            var online = coordinator.Prepare(sources, Shared(), null);
            Assert.That(online.CanPublish, Is.True, online.RefusalReason);
            Assert.That(online.Games, Has.Count.EqualTo(2));
            Assert.That(File.Exists(firstManifest), Is.True);
            Assert.That(File.Exists(secondManifest), Is.True);
        }

        [Test]
        public void DisablingProfileOrOutsideCacheUninstallRefusesWithoutRewritingSettings()
        {
            SaveManifest(firstManifest, "legacy", "legacy/game.exe");
            var profile = Profile(LegacyId, firstManifest, true);
            profile.Enabled = false;
            var settings = Shared();
            var coordinator = new MultiSourceRuntimeCoordinator();
            var disabled = coordinator.Prepare(new[] { profile }, settings, null);
            Assert.That(disabled.CanPublish, Is.False);
            profile.Enabled = true;
            settings.AllowUninstallOutsideCacheFolder = true;
            var unsafePlan = coordinator.Prepare(new[] { profile }, settings, null);
            Assert.That(unsafePlan.CanPublish, Is.False);
            Assert.That(settings.AllowUninstallOutsideCacheFolder, Is.True);
        }

        [Test]
        public void ExplicitCacheOutsideSharedRootRefusesRatherThanAdoptingLegacyFiles()
        {
            SaveManifest(firstManifest, "legacy", "../outside/game.exe");
            var result = new MultiSourceRuntimeCoordinator().Prepare(
                new[] { Profile(LegacyId, firstManifest, true) },
                Shared(), null);
            Assert.That(result.CanPublish, Is.False);
            Assert.That(result.Games, Is.Empty);
            Assert.That(Directory.Exists(cache), Is.True);
        }
    }
}
