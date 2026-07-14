using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class GameWorkflowPolicyServiceTests
    {
        private string root;
        private string cache;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-FullscreenWorkflowTests", Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void Evaluate_RemoteUncachedGame_IsInstallableButNotUninstallable()
        {
            var item = new PersonalCloudLibraryItem { Id = "remote", Title = "Remote", SourcePath = "games/remote.exe", CachePath = "remote/play.exe" };

            var result = new GameWorkflowPolicyService().Evaluate(item, RcloneSettings());

            Assert.That(result.CanInstall, Is.True, result.InstallRefusalReason);
            Assert.That(result.CanUninstall, Is.False);
            Assert.That(result.State.IsInstalled, Is.False);
        }

        [Test]
        public void Evaluate_CachedGame_HasStandardPlayContractAndSafeUninstall()
        {
            var launch = Path.Combine(cache, "cached", "play.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(launch));
            File.WriteAllText(launch, "stub");
            var item = new PersonalCloudLibraryItem { Id = "cached", Title = "Cached", SourcePath = "games/cached.exe", CachePath = "cached/play.exe" };

            var result = new GameWorkflowPolicyService().Evaluate(item, RcloneSettings());

            Assert.That(result.CanInstall, Is.False);
            Assert.That(result.CanUninstall, Is.True, result.UninstallRefusalReason);
            Assert.That(result.State.HasPlayAction, Is.True);
            Assert.That(result.State.PlayPath, Is.EqualTo(launch));
            Assert.That(result.State.WorkingDirectory, Is.EqualTo(Path.GetDirectoryName(launch)));
        }

        [Test]
        public void Evaluate_UnsafeOutsideCacheTarget_RefusesUninstall()
        {
            var outside = Path.Combine(root, "outside", "play.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(outside));
            File.WriteAllText(outside, "stub");
            var settings = RcloneSettings();
            settings.AllowUninstallOutsideCacheFolder = false;
            var item = new PersonalCloudLibraryItem { Id = "outside", Title = "Outside", CachePath = outside, SourcePath = "games/outside.exe" };

            var result = new GameWorkflowPolicyService().Evaluate(item, settings);

            Assert.That(result.CanUninstall, Is.False);
            Assert.That(result.UninstallRefusalReason, Does.Contain("outside"));
        }

        [Test]
        public void Evaluate_UnresolvableSource_ExplainsWhyInstallIsUnavailable()
        {
            var settings = RcloneSettings();
            settings.RcloneRemoteName = string.Empty;

            var result = new GameWorkflowPolicyService().Evaluate(
                new PersonalCloudLibraryItem { Id = "missing", Title = "Missing", SourcePath = "missing.exe" },
                settings);

            Assert.That(result.CanInstall, Is.False);
            Assert.That(result.InstallRefusalReason, Does.Contain("source"));
        }

        private PersonalCloudLibrarySourceSettingsV3 RcloneSettings()
        {
            return new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true,
                AllowDownloads = true,
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneRemoteName = "games",
                LocalCacheFolder = cache,
                TreatMissingFilesAsUninstalled = true,
                UninstallBehavior = PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior
            };
        }
    }
}
