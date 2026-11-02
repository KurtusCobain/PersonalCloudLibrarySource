using NUnit.Framework;
using System;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class SourceProfileSettingsProjectorTests
    {
        private const string LegacyId = "6473bb68-7fd2-4f1a-a33e-4d64c2eb3d02";

        private static PersonalCloudLibrarySourceSettingsV3 Shared()
            => new PersonalCloudLibrarySourceSettingsV3
            {
                LocalCacheFolder = @"C:\Disposable\PclsCache",
                Enabled = true,
                AllowDownloads = true,
                TransferConcurrency = 1,
                VerifyAfterTransfer = true,
                AllowUninstallOutsideCacheFolder = false
            };

        [Test]
        public void LegacyMigrationCopiesProviderWithoutChangingOriginalSettings()
        {
            var original = Shared();
            original.SourceProviderType = "RcloneRemote";
            original.RcloneRemoteName = "legacy-remote";
            original.RcloneExecutablePath = @"C:\Tools\rclone.exe";
            original.RcloneManifestPath = "games/catalog.json";
            original.RcloneContentRoot = "games/content";
            original.RcloneTimeoutSeconds = 117;
            var oldProvider = original.SourceProviderType;

            var profile = new SourceProfileSettingsProjector().FromLegacySettings(
                original, Guid.Parse(LegacyId));

            Assert.That(profile.ProfileId, Is.EqualTo(LegacyId));
            Assert.That(profile.IsLegacySingleSource, Is.True);
            Assert.That(profile.RcloneRemoteName, Is.EqualTo("legacy-remote"));
            Assert.That(profile.RcloneTimeoutSeconds, Is.EqualTo(117));
            Assert.That(profile.RcloneContentRoot, Is.EqualTo("games/content"));
            Assert.That(original.SourceProviderType, Is.EqualTo(oldProvider));
            Assert.That(original.RcloneRemoteName, Is.EqualTo("legacy-remote"));
        }

        [Test]
        public void TwoSourcesProjectDifferentPathsButKeepSharedCacheAndTransferPolicy()
        {
            var shared = Shared();
            var first = new PclsSourceProfileDefinition
            {
                ProfileId = "6473bb68-7fd2-4f1a-a33e-4d64c2eb3d02",
                DisplayName = "Test local",
                ProviderType = "LocalFolder",
                LocalLibraryRoot = @"D:\SourceGames",
                ManifestRelativePath = "catalog.json"
            };
            var second = new PclsSourceProfileDefinition
            {
                ProfileId = "c496b7d2-92d6-493b-a02a-e2abbe9625f4",
                DisplayName = "Test cloud",
                ProviderType = "RcloneRemote",
                RcloneExecutablePath = @"C:\Tools\rclone.exe",
                RcloneRemoteName = "disposable",
                RcloneManifestPath = "manifest.json",
                RcloneTimeoutSeconds = 144
            };
            var projector = new SourceProfileSettingsProjector();
            var local = projector.Project(first, shared);
            var remote = projector.Project(second, shared);

            Assert.That(local.SourceProviderType, Is.EqualTo("LocalFolder"));
            Assert.That(local.LocalLibraryRoot, Is.EqualTo(@"D:\SourceGames"));
            Assert.That(remote.SourceProviderType, Is.EqualTo("RcloneRemote"));
            Assert.That(remote.RcloneRemoteName, Is.EqualTo("disposable"));
            Assert.That(remote.RcloneTimeoutSeconds, Is.EqualTo(144));
            Assert.That(remote.LocalCacheFolder, Is.EqualTo(shared.LocalCacheFolder));
            Assert.That(local.LocalCacheFolder, Is.EqualTo(shared.LocalCacheFolder));
            Assert.That(local.VerifyAfterTransfer && remote.VerifyAfterTransfer, Is.True);
            Assert.That(remote.AllowUninstallOutsideCacheFolder, Is.False);
            Assert.That(shared.SourceProviderType, Is.EqualTo("LocalFile"),
                "Projected profiles must not change active single-source settings.");
        }

        [Test]
        public void LegacyCustomTimeoutSurvivesProjection()
        {
            var shared = Shared();
            shared.SourceProviderType = "RcloneRemote";
            shared.RcloneRemoteName = "old-config";
            shared.RcloneManifestPath = "legacy.json";
            shared.RcloneTimeoutSeconds = 123;
            var projector = new SourceProfileSettingsProjector();
            var profile = projector.FromLegacySettings(shared, Guid.Parse(LegacyId));
            var projected = projector.Project(profile, shared);

            Assert.That(projected.RcloneTimeoutSeconds, Is.EqualTo(123));
            Assert.That(projected.RcloneRemoteName, Is.EqualTo("old-config"));
            Assert.That(profile.IsLegacySingleSource, Is.True);
        }

        [Test]
        public void UnsafeOutsideCacheUninstallIsNotCarriedIntoMultiSourceMode()
        {
            var shared = Shared();
            shared.AllowUninstallOutsideCacheFolder = true;
            var profile = new PclsSourceProfileDefinition
            {
                ProfileId = LegacyId, DisplayName = "Local manifest",
                ProviderType = "LocalFile",
                LocalManifestPath = @"D:\SourceGames\catalog.json"
            };

            Assert.Throws<InvalidOperationException>(() =>
                new SourceProfileSettingsProjector().Project(profile, shared));
            Assert.That(shared.AllowUninstallOutsideCacheFolder, Is.True,
                "Fail without silently overwriting the user's existing setting.");
        }

        [Test]
        public void InvalidRemoteAndMissingLocalManifestFailBeforeProviderAccess()
        {
            var badRemote = new PclsSourceProfileDefinition
            {
                ProfileId = LegacyId, DisplayName = "Bad remote",
                ProviderType = "RcloneRemote", RcloneRemoteName = "test",
                RcloneManifestPath = "manifest.json", RcloneTimeoutSeconds = 1
            };
            var noManifest = new PclsSourceProfileDefinition
            {
                ProfileId = LegacyId, DisplayName = "Bad local",
                ProviderType = "LocalFolder", LocalLibraryRoot = @"D:\Test"
            };

            Assert.Throws<InvalidOperationException>(() =>
                new SourceProfileSettingsProjector().Project(badRemote, Shared()));
            Assert.Throws<InvalidOperationException>(() =>
                new SourceProfileSettingsProjector().Project(noManifest, Shared()));
        }

        [Test]
        public void DisabledProfileDoesNotBecomeEnabledDuringProjection()
        {
            var profile = new PclsSourceProfileDefinition
            {
                ProfileId = LegacyId, DisplayName = "Offline library",
                ProviderType = "LocalFile",
                LocalManifestPath = @"D:\SourceGames\catalog.json",
                Enabled = false
            };
            Assert.That(new SourceProfileSettingsProjector()
                .Project(profile, Shared()).Enabled, Is.False);
        }
    }
}
