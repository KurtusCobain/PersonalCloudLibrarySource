using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class SourceCatalogContinuityPolicyTests
    {
        private string root;
        private string cache;
        private string manifest;
        private const string LegacyId = "770c8eb6-9bd1-4189-b555-18a94425eaf1";
        private const string AdditionalId = "d8ea26ab-0ed1-4f22-922d-22b6036ac97b";

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-Continuity",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            manifest = Path.Combine(root, "library.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private PersonalCloudLibrarySourceSettingsV3 Shared()
            => new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true, LocalCacheFolder = cache,
                AllowUninstallOutsideCacheFolder = false
            };

        private PclsSourceProfileDefinition Profile(bool legacy = true)
            => new PclsSourceProfileDefinition
            {
                ProfileId = legacy ? LegacyId : AdditionalId,
                IsLegacySingleSource = legacy, Enabled = true,
                DisplayName = "Fixture source", ProviderType = "LocalFile",
                LocalManifestPath = manifest
            };

        private ImportOutcome ValidImport(PclsSourceProfileDefinition profile,
            PersonalCloudLibrarySourceSettingsV3 shared, bool empty = false)
        {
            File.WriteAllText(manifest, empty
                ? "{\"version\":3,\"items\":[]}"
                : "{\"version\":3,\"items\":[{" +
                  "\"id\":\"demo\",\"title\":\"Demo\",\"sourceType\":\"file\"," +
                  "\"sourcePath\":\"demo.exe\",\"cachePath\":\"one/demo.exe\"}]}");
            var settings = new SourceProfileSettingsProjector().Project(profile, shared);
            var result = new ImportOutcomeService().Import(settings, null);
            Assert.That(result.Succeeded, Is.True, result.Error);
            return result;
        }

        private static ImportOutcome Offline()
            => ImportOutcome.Failure(ImportFailureKind.SourceUnavailable,
                "disposable", "unavailable");

        [Test]
        public void HealthyLegacyCatalogRetainsOriginalIdsAsOfflineCandidate()
        {
            var profile = Profile();
            var shared = Shared();
            var policy = new SourceCatalogContinuityPolicy();
            var fresh = ValidImport(profile, shared);
            var history = policy.CaptureSuccessful(profile, shared, fresh);
            var outcome = policy.Examine(profile, shared, Offline(), history);

            Assert.That(history.GameIds.Single(), Is.EqualTo("demo"));
            Assert.That(outcome.State, Is.EqualTo(
                SourceCatalogContinuityState.OfflineIdentityCandidate));
            Assert.That(outcome.RetainedGameCount, Is.EqualTo(1));
            Assert.That(outcome.HasOfflineIdentityCandidate, Is.True);
            Assert.That(File.Exists(manifest), Is.True);
        }

        [Test]
        public void NewSourceIdentityRetainsNamespaceAndIgnoresDisplayRename()
        {
            var profile = Profile(false);
            var shared = Shared();
            var policy = new SourceCatalogContinuityPolicy();
            var history = policy.CaptureSuccessful(
                profile, shared, ValidImport(profile, shared));
            profile.DisplayName = "Renamed but same origin";
            var outcome = policy.Examine(profile, shared, Offline(), history);

            Assert.That(history.GameIds.Single(), Does.StartWith("pcls1-"));
            Assert.That(outcome.State, Is.EqualTo(
                SourceCatalogContinuityState.OfflineIdentityCandidate));
        }

        [Test]
        public void ChangedManifestLocationOrSharedCacheCannotAdoptOldInventory()
        {
            var profile = Profile();
            var shared = Shared();
            var policy = new SourceCatalogContinuityPolicy();
            var history = policy.CaptureSuccessful(
                profile, shared, ValidImport(profile, shared));
            profile.LocalManifestPath = Path.Combine(root, "other.json");

            Assert.That(policy.Examine(profile, shared, Offline(), history).State,
                Is.EqualTo(SourceCatalogContinuityState.HoldChangedBinding));
            profile.LocalManifestPath = manifest;
            shared.LocalCacheFolder = Path.Combine(root, "new-cache");
            Assert.That(policy.Examine(profile, shared, Offline(), history).State,
                Is.EqualTo(SourceCatalogContinuityState.HoldChangedBinding));
        }

        [Test]
        public void DifferentProfileGuidAndLegacyReclassificationCannotUseHistory()
        {
            var profile = Profile();
            var shared = Shared();
            var policy = new SourceCatalogContinuityPolicy();
            var history = policy.CaptureSuccessful(
                profile, shared, ValidImport(profile, shared));

            profile.ProfileId = Guid.NewGuid().ToString("D");
            Assert.That(policy.Examine(profile, shared, Offline(), history).State,
                Is.EqualTo(SourceCatalogContinuityState.HoldChangedBinding));
            profile.ProfileId = LegacyId;
            profile.IsLegacySingleSource = false;
            Assert.That(policy.Examine(profile, shared, Offline(), history).State,
                Is.EqualTo(SourceCatalogContinuityState.HoldChangedBinding));
        }

        [Test]
        public void MissingOrEmptyHistoryCannotReplayAZeroGameCatalog()
        {
            var profile = Profile();
            var shared = Shared();
            var policy = new SourceCatalogContinuityPolicy();
            Assert.That(policy.Examine(profile, shared, Offline(), null).State,
                Is.EqualTo(SourceCatalogContinuityState.HoldMissingHistory));
            var history = policy.CaptureSuccessful(
                profile, shared, ValidImport(profile, shared, true));
            Assert.That(history.GameCount, Is.Zero);
            Assert.That(policy.Examine(profile, shared, Offline(), history).State,
                Is.EqualTo(SourceCatalogContinuityState.HoldMissingHistory));
        }

        [Test]
        public void CorruptUnsupportedAndUnknownImportFailuresDoNotBecomeOffline()
        {
            var profile = Profile();
            var shared = Shared();
            var policy = new SourceCatalogContinuityPolicy();
            var history = policy.CaptureSuccessful(
                profile, shared, ValidImport(profile, shared));
            foreach (var kind in new[]
            {
                ImportFailureKind.InvalidManifest, ImportFailureKind.UnsupportedSchema,
                ImportFailureKind.None
            })
            {
                var failure = ImportOutcome.Failure(kind, "fixture", "not valid");
                Assert.That(policy.Examine(profile, shared, failure, history).State,
                    Is.EqualTo(SourceCatalogContinuityState.HoldInvalidOrUnsafe));
            }
            Assert.That(policy.Examine(profile, shared, null, history).State,
                Is.EqualTo(SourceCatalogContinuityState.HoldInvalidOrUnsafe));
        }

        [Test]
        public void DisabledSourceCannotUseHistory()
        {
            var profile = Profile();
            var shared = Shared();
            var policy = new SourceCatalogContinuityPolicy();
            var history = policy.CaptureSuccessful(
                profile, shared, ValidImport(profile, shared));
            profile.Enabled = false;
            Assert.That(policy.Examine(profile, shared, Offline(), history).State,
                Is.EqualTo(SourceCatalogContinuityState.HoldDisabled));
        }

        [Test]
        public void ForgedSuccessfulImportWithMissingGamesIsNeverCaptured()
        {
            var profile = Profile();
            var shared = Shared();
            var policy = new SourceCatalogContinuityPolicy();
            var actual = ValidImport(profile, shared);
            var forged = ImportOutcome.Success("fixture", actual.ValidItems,
                new Playnite.SDK.Models.GameMetadata[0]);
            Assert.Throws<InvalidOperationException>(() =>
                policy.CaptureSuccessful(profile, shared, forged));
        }

        [Test]
        public void SnapshotIsImmutableWhenCallerChangesSourceIdentity()
        {
            var profile = Profile();
            var shared = Shared();
            var policy = new SourceCatalogContinuityPolicy();
            var history = policy.CaptureSuccessful(
                profile, shared, ValidImport(profile, shared));
            profile.DisplayName = "another";
            profile.LocalManifestPath = "elsewhere";
            Assert.That(history.GameIds.Single(), Is.EqualTo("demo"));
            Assert.That(history.ProfileId, Is.EqualTo(LegacyId));
            Assert.That(policy.Examine(profile, shared, Offline(), history).State,
                Is.EqualTo(SourceCatalogContinuityState.HoldChangedBinding));
        }

        [Test]
        public void FreshValidEmptyCatalogIsNotAnOfflineHistoryReplay()
        {
            var profile = Profile();
            var shared = Shared();
            var policy = new SourceCatalogContinuityPolicy();
            var fresh = ValidImport(profile, shared, true);
            var outcome = policy.Examine(profile, shared, fresh, null);
            Assert.That(outcome.State, Is.EqualTo(
                SourceCatalogContinuityState.FreshValidated));
            Assert.That(outcome.HasOfflineIdentityCandidate, Is.False);
            Assert.That(outcome.RetainedGameCount, Is.Zero);
        }
    }
}
