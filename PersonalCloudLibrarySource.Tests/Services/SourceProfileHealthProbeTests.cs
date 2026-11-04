using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class SourceProfileHealthProbeTests
    {
        private string root;
        private string manifest;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ProfileHealth",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            manifest = Path.Combine(root, "games.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private PersonalCloudLibrarySourceSettingsV3 Shared()
            => new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true, LocalCacheFolder = root,
                AllowUninstallOutsideCacheFolder = false
            };

        private PclsSourceProfileDefinition Local()
            => new PclsSourceProfileDefinition
            {
                ProfileId = Guid.NewGuid().ToString("D"),
                DisplayName = "Test",
                ProviderType = "LocalFile",
                LocalManifestPath = manifest,
                IsLegacySingleSource = true
            };

        [Test]
        public void ValidManifestReturnsReadOnlyCountWithoutChangingFiles()
        {
            var original = "{\"version\":3,\"items\":[]}";
            File.WriteAllText(manifest, original);
            var profile = Local();
            var result = new SourceProfileHealthProbe().Probe(profile, Shared(), null);
            Assert.That(result.Status, Is.EqualTo(SourceManifestHealth.ManifestValid));
            Assert.That(result.ManifestReadable, Is.True);
            Assert.That(result.ManifestItemCount, Is.Zero);
            Assert.That(result.ProfileId, Is.EqualTo(profile.ProfileId));
            Assert.That(File.ReadAllText(manifest), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(root), Has.Length.EqualTo(1));
        }

        [Test]
        public void MissingSourceNeverReportsEmptySuccessfulCatalog()
        {
            var result = new SourceProfileHealthProbe().Probe(Local(), Shared(), null);
            Assert.That(result.Status, Is.EqualTo(SourceManifestHealth.SourceUnavailable));
            Assert.That(result.ManifestReadable, Is.False);
            Assert.That(result.ManifestItemCount, Is.Zero);
            Assert.That(Directory.GetFiles(root), Is.Empty);
        }

        [Test]
        public void MalformedManifestIsNotConflatedWithOfflineSource()
        {
            File.WriteAllText(manifest, "{bad-json");
            var result = new SourceProfileHealthProbe().Probe(Local(), Shared(), null);
            Assert.That(result.Status, Is.EqualTo(SourceManifestHealth.InvalidManifest));
            Assert.That(result.ManifestReadable, Is.False);
        }

        [Test]
        public void UnsupportedSchemaIsReportedSeparately()
        {
            File.WriteAllText(manifest, "{\"version\":999,\"items\":[]}");
            var result = new SourceProfileHealthProbe().Probe(Local(), Shared(), null);
            Assert.That(result.Status, Is.EqualTo(SourceManifestHealth.UnsupportedSchema));
        }

        [Test]
        public void InvalidConfigurationAndDisabledProfileNeverReadSource()
        {
            var profile = Local();
            profile.LocalManifestPath = "";
            var invalid = new SourceProfileHealthProbe().Probe(profile, Shared(), null);
            Assert.That(invalid.Status, Is.EqualTo(SourceManifestHealth.InvalidConfiguration));
            profile.Enabled = false;
            var disabled = new SourceProfileHealthProbe().Probe(profile, Shared(), null);
            Assert.That(disabled.Status, Is.EqualTo(SourceManifestHealth.Disabled));
        }

        [Test]
        public void RcloneCallbackErrorsAreUnavailableWithoutExposingSecrets()
        {
            var profile = new PclsSourceProfileDefinition
            {
                ProfileId = Guid.NewGuid().ToString("D"),
                DisplayName = "Remote",
                ProviderType = "RcloneRemote",
                RcloneExecutablePath = "rclone",
                RcloneRemoteName = "private-drive",
                RcloneManifestPath = "games.json",
                RcloneTimeoutSeconds = 30
            };
            var result = new SourceProfileHealthProbe().Probe(
                profile, Shared(), _ =>
                {
                    throw new IOException("secret remote path and token");
                });
            Assert.That(result.Status, Is.EqualTo(SourceManifestHealth.SourceUnavailable));
            Assert.That(result.ProfileId, Is.EqualTo(profile.ProfileId));
            Assert.That(result.ManifestItemCount, Is.Zero);
        }
    }
}
