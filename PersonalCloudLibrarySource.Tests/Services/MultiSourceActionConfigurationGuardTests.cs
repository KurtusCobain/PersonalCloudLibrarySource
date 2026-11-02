using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class MultiSourceActionConfigurationGuardTests
    {
        private string root;
        private string manifest;
        private string cache;
        private const string ProfileId = "d8ea26ab-0ed1-4f22-922d-22b6036ac97b";

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ActionDrift",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            manifest = Path.Combine(root, "source.json");
            File.WriteAllText(manifest, "{\"version\":3,\"items\":[{" +
                "\"id\":\"game\",\"title\":\"Disposable\",\"sourceType\":\"file\"," +
                "\"sourcePath\":\"game.exe\",\"cachePath\":\"safe/game.exe\"}]}");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private PclsSourceProfileDefinition Profile()
            => new PclsSourceProfileDefinition
            {
                ProfileId = ProfileId,
                DisplayName = "Fixture",
                ProviderType = "LocalFile",
                LocalManifestPath = manifest
            };

        private PersonalCloudLibrarySourceSettingsV3 Shared()
            => new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true, EnableMultiSourceProfiles = true,
                LocalCacheFolder = cache, AllowDownloads = true,
                AllowUninstallOutsideCacheFolder = false,
                UninstallBehavior =
                    PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior
            };

        private static MultiSourceRuntimePlan Prepare(
            PclsSourceProfileDefinition profile,
            PersonalCloudLibrarySourceSettingsV3 shared)
        {
            var result = new MultiSourceRuntimeCoordinator().Prepare(
                new[] { profile }, shared, null);
            Assert.That(result.CanPublish, Is.True, result.RefusalReason);
            return result;
        }

        [Test]
        public void UnchangedProfileSettingsAllowPublishedRoute()
        {
            var profile = Profile();
            var shared = Shared();
            var plan = Prepare(profile, shared);
            Assert.That(plan.MatchesConfiguration(new[] { profile }, shared), Is.True);
            SourceScopedGameAction route;
            Assert.That(plan.TryResolveAction("game", out route), Is.True);
        }

        [Test]
        public void ManifestOrRemoteConfigurationEditQuarantinesPublishedRoute()
        {
            var profile = Profile();
            var shared = Shared();
            var plan = Prepare(profile, shared);
            profile.LocalManifestPath = Path.Combine(root, "other.json");
            Assert.That(plan.MatchesConfiguration(new[] { profile }, shared), Is.False);
            profile.LocalManifestPath = manifest;
            profile.RcloneRemoteName = "unexpected";
            Assert.That(plan.MatchesConfiguration(new[] { profile }, shared), Is.False);
        }

        [Test]
        public void DisplayRenameOrSourceDisableAlsoRequiresRefresh()
        {
            var profile = Profile();
            var shared = Shared();
            var plan = Prepare(profile, shared);
            profile.DisplayName = "Renamed";
            Assert.That(plan.MatchesConfiguration(new[] { profile }, shared), Is.False);
            profile.DisplayName = "Fixture";
            profile.Enabled = false;
            Assert.That(plan.MatchesConfiguration(new[] { profile }, shared), Is.False);
        }

        [Test]
        public void SharedCacheAndUninstallPolicyEditsRefuseStaleActions()
        {
            var profile = Profile();
            var shared = Shared();
            var plan = Prepare(profile, shared);
            shared.LocalCacheFolder = Path.Combine(root, "different-cache");
            Assert.That(plan.MatchesConfiguration(new[] { profile }, shared), Is.False);
            shared.LocalCacheFolder = cache;
            shared.UninstallBehavior = "KeepAllCachedFiles";
            Assert.That(plan.MatchesConfiguration(new[] { profile }, shared), Is.False);
            shared.UninstallBehavior =
                PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior;
            shared.AllowUninstallOutsideCacheFolder = true;
            Assert.That(plan.MatchesConfiguration(new[] { profile }, shared), Is.False);
        }

        [Test]
        public void TransferAndDownloadSettingsDriftRefusesActions()
        {
            var p = Profile();
            var shared = Shared();
            var plan = Prepare(p, shared);
            shared.AllowDownloads = false;
            Assert.That(plan.MatchesConfiguration(new[] { p }, shared), Is.False);
            shared.AllowDownloads = true;
            shared.VerifyAfterTransfer = !shared.VerifyAfterTransfer;
            Assert.That(plan.MatchesConfiguration(new[] { p }, shared), Is.False);
            shared.VerifyAfterTransfer = !shared.VerifyAfterTransfer;
            shared.RemoveIncompleteTransferFiles = !shared.RemoveIncompleteTransferFiles;
            Assert.That(plan.MatchesConfiguration(new[] { p }, shared), Is.False);
            shared.RemoveIncompleteTransferFiles = !shared.RemoveIncompleteTransferFiles;
            shared.TransferConcurrency++;
            Assert.That(plan.MatchesConfiguration(new[] { p }, shared), Is.False);
        }

        [Test]
        public void UiOnlySettingsDoNotChangeFrozenTransferAuthority()
        {
            var p = Profile();
            var shared = Shared();
            var plan = Prepare(p, shared);
            shared.ShowSidebarDashboard = !shared.ShowSidebarDashboard;
            shared.NotifyLibraryUpdates = !shared.NotifyLibraryUpdates;
            Assert.That(plan.MatchesConfiguration(new[] { p }, shared), Is.True);
        }

        [Test]
        public void DisablingMultiSourceModeQuarantinesActionsImmediately()
        {
            var p = Profile();
            var shared = Shared();
            var plan = Prepare(p, shared);
            shared.EnableMultiSourceProfiles = false;
            Assert.That(plan.MatchesConfiguration(new[] { p }, shared), Is.False);
            shared.Enabled = false;
            Assert.That(plan.MatchesConfiguration(new[] { p }, shared), Is.False);
        }

        [Test]
        public void InvalidOrMissingProfilesCannotAdoptAPlan()
        {
            var p = Profile();
            var shared = Shared();
            var plan = Prepare(p, shared);
            Assert.That(plan.MatchesConfiguration(null, shared), Is.False);
            Assert.That(plan.MatchesConfiguration(new PclsSourceProfileDefinition[0], shared),
                Is.False);
            p.ProfileId = "../malicious";
            Assert.That(plan.MatchesConfiguration(new[] { p }, shared), Is.False);
        }

        [Test]
        public void ProfileFileUpdatesInvalidateRouteBeforeNextImport()
        {
            var p = Profile();
            var shared = Shared();
            var store = new SourceProfilesFileStore();
            store.Save(root, new[] { p });
            var loaded = store.Load(root);
            Assert.That(loaded.Succeeded, Is.True);
            var plan = new MultiSourceRuntimeCoordinator().Prepare(
                loaded.Profiles, shared, null);
            Assert.That(plan.CanPublish, Is.True, plan.RefusalReason);
            Assert.That(plan.MatchesConfiguration(store.Load(root).Profiles, shared), Is.True);

            var edited = Profile();
            edited.LocalManifestPath = Path.Combine(root, "moved.json");
            store.Save(root, new[] { edited });
            Assert.That(plan.MatchesConfiguration(store.Load(root).Profiles, shared), Is.False);
            Assert.That(File.Exists(manifest), Is.True);
        }

        [Test]
        public void SourceChangedDuringRemoteReadIsRejectedWithoutPublishingGames()
        {
            var shared = Shared();
            var remote = Profile();
            remote.ProviderType = "RcloneRemote";
            remote.RcloneExecutablePath = "rclone";
            remote.RcloneRemoteName = "fixture";
            remote.RcloneManifestPath = "catalog.json";
            remote.RcloneContentRoot = "games";
            var coordinator = new MultiSourceRuntimeCoordinator();
            var plan = coordinator.Prepare(new[] { remote }, shared, scoped =>
            {
                shared.AllowDownloads = false;
                return "{\"version\":3,\"items\":[{" +
                    "\"id\":\"game\",\"title\":\"Disposable\",\"sourceType\":\"file\"," +
                    "\"remotePath\":\"cloud/game.exe\",\"cachePath\":\"safe/game.exe\"}]}";
            });

            Assert.That(plan.CanPublish, Is.False);
            Assert.That(plan.Games, Is.Empty);
            SourceScopedGameAction route;
            Assert.That(plan.TryResolveAction("game", out route), Is.False);
            Assert.That(plan.RefusalReason, Does.Contain("changed during import"));
        }

        [Test]
        public void ReorderedProfilesCannotReusePriorFrozenPlan()
        {
            var shared = Shared();
            var first = Profile();
            var second = Profile();
            second.ProfileId = Guid.NewGuid().ToString("D");
            second.LocalManifestPath = Path.Combine(root, "second.json");
            File.WriteAllText(second.LocalManifestPath, "{\"version\":3,\"items\":[{" +
                "\"id\":\"other\",\"title\":\"Other\",\"sourceType\":\"file\"," +
                "\"sourcePath\":\"other.exe\",\"cachePath\":\"other/other.exe\"}]}");
            var plan = new MultiSourceRuntimeCoordinator().Prepare(
                new[] { first, second }, shared, null);
            Assert.That(plan.CanPublish, Is.True, plan.RefusalReason);
            Assert.That(plan.MatchesConfiguration(new[] { first, second }, shared), Is.True);
            Assert.That(plan.MatchesConfiguration(new[] { second, first }, shared), Is.False);
        }
    }
}
