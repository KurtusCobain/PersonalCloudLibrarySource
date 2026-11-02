using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class SourceCatalogDisplaySnapshotStoreTests
    {
        private string root;
        private string cache;
        private string manifest;
        private const string LegacyId = "770c8eb6-9bd1-4189-b555-18a94425eaf1";

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-DisplaySnapshot",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            manifest = Path.Combine(root, "games.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private PclsSourceProfileDefinition Profile(bool legacy = true)
            => new PclsSourceProfileDefinition
            {
                ProfileId = LegacyId, DisplayName = "Original",
                ProviderType = "LocalFile", IsLegacySingleSource = legacy,
                LocalManifestPath = manifest
            };

        private PersonalCloudLibrarySourceSettingsV3 Shared()
            => new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true, LocalCacheFolder = cache,
                AllowUninstallOutsideCacheFolder = false
            };

        private ImportOutcome Import(PclsSourceProfileDefinition profile,
            PersonalCloudLibrarySourceSettingsV3 shared,
            bool more = false, bool withNotes = false)
        {
            var first = "{\"id\":\"one\",\"title\":\"Alpha\",\"sourceType\":\"file\"," +
                "\"sourcePath\":\"alpha.exe\",\"cachePath\":\"alpha/alpha.exe\"" +
                (withNotes ? ",\"notes\":\"A sample description\"" : "") + "}";
            var second = "{\"id\":\"two\",\"title\":\"Beta\",\"sourceType\":\"file\"," +
                "\"sourcePath\":\"beta.exe\",\"cachePath\":\"beta/beta.exe\"}";
            File.WriteAllText(manifest, "{\"version\":3,\"items\":[" +
                first + (more ? "," + second : "") + "]}");
            var outcome = new ImportOutcomeService().Import(
                new SourceProfileSettingsProjector().Project(profile, shared), null);
            Assert.That(outcome.Succeeded, Is.True, outcome.Error);
            return outcome;
        }

        [Test]
        public void CaptureAndPersistContainsNoPlayableOrDeletionFields()
        {
            var p = Profile();
            var shared = Shared();
            var outcome = Import(p, shared, false, true);
            var store = new SourceCatalogDisplaySnapshotStore();
            var snapshot = store.CaptureSuccessful(p, shared, outcome);
            var path = store.Save(root, snapshot);
            var read = store.Load(root, LegacyId);
            Assert.That(read.Succeeded, Is.True, read.Reason);
            Assert.That(read.Snapshot.Entries.Single().GameId, Is.EqualTo("one"));
            Assert.That(read.Snapshot.Entries.Single().Name, Is.EqualTo("Alpha"));
            Assert.That(read.Snapshot.Entries.Single().Description,
                Is.EqualTo("A sample description"));
            var json = File.ReadAllText(path);
            Assert.That(json, Does.Not.Contain("alpha.exe"));
            Assert.That(json, Does.Not.Contain(cache));
            Assert.That(json, Does.Not.Contain(manifest));
            foreach (var forbidden in new[] { "IsInstalled", "GameActions",
                "InstallDirectory", "OwnershipReceiptKey", "UninstallBehavior" })
                Assert.That(json, Does.Not.Contain(forbidden));
        }

        [Test]
        public void NewSourceUsesExistingStableNamespacedGameId()
        {
            var p = Profile(false);
            var shared = Shared();
            var outcome = Import(p, shared);
            var store = new SourceCatalogDisplaySnapshotStore();
            var display = store.CaptureSuccessful(p, shared, outcome);
            var identity = new SourceCatalogContinuityPolicy()
                .CaptureSuccessful(p, shared, outcome);
            Assert.That(display.Entries.Single().GameId, Does.StartWith("pcls1-"));
            Assert.That(store.MatchesIdentity(display, identity), Is.True);
        }

        [Test]
        public void DisplayEvidenceMustExactlyMatchSourceBoundIdentityInventory()
        {
            var p = Profile();
            var shared = Shared();
            var store = new SourceCatalogDisplaySnapshotStore();
            var outcome = Import(p, shared);
            var display = store.CaptureSuccessful(p, shared, outcome);
            var identity = new SourceCatalogContinuityPolicy()
                .CaptureSuccessful(p, shared, outcome);
            Assert.That(store.MatchesIdentity(display, identity), Is.True);

            var changed = Import(p, shared, true);
            var different = store.CaptureSuccessful(p, shared, changed);
            Assert.That(store.MatchesIdentity(different, identity), Is.False);

            p.LocalManifestPath = Path.Combine(root, "moved.json");
            // Build a separate valid binding by copying fixture to moved path.
            File.Copy(manifest, p.LocalManifestPath);
            var migrated = new ImportOutcomeService().Import(
                new SourceProfileSettingsProjector().Project(p, shared), null);
            var differentBinding = store.CaptureSuccessful(p, shared, migrated);
            Assert.That(store.MatchesIdentity(differentBinding, identity), Is.False);
        }

        [Test]
        public void CallerMutationCannotChangeCapturedEvidence()
        {
            var p = Profile();
            var shared = Shared();
            var outcome = Import(p, shared);
            var store = new SourceCatalogDisplaySnapshotStore();
            var snapshot = store.CaptureSuccessful(p, shared, outcome);
            var exposed = snapshot.Entries.Single();
            exposed.Name = "Forged";
            Assert.That(snapshot.Entries.Single().Name, Is.EqualTo("Alpha"));
            store.Save(root, snapshot);
            Assert.That(store.Load(root, LegacyId).Snapshot.Entries.Single().Name,
                Is.EqualTo("Alpha"));
        }

        [Test]
        public void ValidReplacementPreservesBackupAndRejectsDamagedCurrent()
        {
            var p = Profile();
            var shared = Shared();
            var store = new SourceCatalogDisplaySnapshotStore();
            var path = store.Save(root, store.CaptureSuccessful(
                p, shared, Import(p, shared)));
            store.Save(root, store.CaptureSuccessful(p, shared, Import(p, shared, true)));
            Assert.That(store.Load(root, LegacyId).Snapshot.Entries.Count, Is.EqualTo(2));
            Assert.That(Directory.GetFiles(root, "source-catalog-display-backup-*.json"),
                Has.Length.EqualTo(1));
            File.WriteAllText(path, File.ReadAllText(path).Replace("Alpha", "Tampered"));
            Assert.That(store.Load(root, LegacyId).Succeeded, Is.False);
            Assert.Throws<InvalidOperationException>(() => store.Save(root,
                store.CaptureSuccessful(p, shared, Import(p, shared))));
            Assert.That(Directory.GetFiles(root, "*.tmp"), Is.Empty);
        }

        [Test]
        public void UnsupportedSchemaAndMalformedJsonAreNeverAdopted()
        {
            var p = Profile();
            var shared = Shared();
            var store = new SourceCatalogDisplaySnapshotStore();
            var path = store.Save(root, store.CaptureSuccessful(
                p, shared, Import(p, shared)));
            File.WriteAllText(path, File.ReadAllText(path).Replace(
                "\"SchemaVersion\":1", "\"SchemaVersion\":99"));
            Assert.That(store.Load(root, LegacyId).Succeeded, Is.False);
            File.WriteAllText(path, "{broken");
            Assert.That(store.Load(root, LegacyId).Succeeded, Is.False);
        }

        [Test]
        public void OversizedFileAndSourceTraversalFailClosed()
        {
            var p = Profile();
            var shared = Shared();
            var store = new SourceCatalogDisplaySnapshotStore();
            var path = store.Save(root, store.CaptureSuccessful(p, shared,
                Import(p, shared)));
            File.WriteAllText(path, new string('x',
                SourceCatalogDisplaySnapshotStore.MaxBytes + 1));
            Assert.That(store.Load(root, LegacyId).Succeeded, Is.False);
            Assert.Throws<InvalidOperationException>(() => store.Load(root, "../other"));
        }

        [Test]
        public void MissingRootAndOccupiedDirectoryCannotBeOverwritten()
        {
            var p = Profile();
            var shared = Shared();
            var store = new SourceCatalogDisplaySnapshotStore();
            var snapshot = store.CaptureSuccessful(p, shared, Import(p, shared));
            var missing = Path.Combine(root, "missing");
            Assert.Throws<DirectoryNotFoundException>(() => store.Save(missing, snapshot));
            var path = store.Save(root, snapshot);
            File.Delete(path);
            Directory.CreateDirectory(path);
            Assert.Throws<InvalidOperationException>(() => store.Save(root, snapshot));
            Assert.That(Directory.Exists(path), Is.True);
        }

        [Test]
        public void IncompleteImportCannotBeCapturedOrPublished()
        {
            var p = Profile();
            var shared = Shared();
            var outcome = Import(p, shared);
            var forged = ImportOutcome.Success("fixture", outcome.ValidItems,
                new Playnite.SDK.Models.GameMetadata[0]);
            var store = new SourceCatalogDisplaySnapshotStore();
            Assert.Throws<InvalidOperationException>(() =>
                store.CaptureSuccessful(p, shared, forged));
            File.Delete(manifest);
            Assert.That(new MultiSourceRuntimeCoordinator().Prepare(
                new[] { p }, shared, null).CanPublish, Is.False);
        }
    }
}
