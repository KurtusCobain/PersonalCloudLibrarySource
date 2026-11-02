using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class SourceCatalogSnapshotStoreTests
    {
        private string root;
        private string manifest;
        private string cache;
        private const string ProfileId = "770c8eb6-9bd1-4189-b555-18a94425eaf1";

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-SnapshotStore",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            manifest = Path.Combine(root, "fixture.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private PclsSourceProfileDefinition Profile() => new PclsSourceProfileDefinition
        {
            ProfileId = ProfileId,
            DisplayName = "Fixture",
            IsLegacySingleSource = true,
            ProviderType = "LocalFile",
            LocalManifestPath = manifest
        };

        private PersonalCloudLibrarySourceSettingsV3 Shared()
            => new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true, LocalCacheFolder = cache,
                AllowUninstallOutsideCacheFolder = false
            };

        private SourceCatalogIdentitySnapshot Snapshot(bool two = false)
        {
            var first = "{\"id\":\"fixture\",\"title\":\"Fixture\",\"sourceType\":\"file\",\"sourcePath\":\"game.exe\",\"cachePath\":\"fixture/game.exe\"}";
            var second = "{\"id\":\"second\",\"title\":\"Second\",\"sourceType\":\"file\",\"sourcePath\":\"another.exe\",\"cachePath\":\"second/another.exe\"}";
            File.WriteAllText(manifest, "{\"version\":3,\"items\":[" +
                first + (two ? "," + second : "") + "]}");
            var profile = Profile();
            var shared = Shared();
            var import = new ImportOutcomeService().Import(
                new SourceProfileSettingsProjector().Project(profile, shared), null);
            Assert.That(import.Succeeded, Is.True, import.Error);
            return new SourceCatalogContinuityPolicy().CaptureSuccessful(profile, shared, import);
        }

        [Test]
        public void ExplicitSaveAndLoadRoundTripPreservesOnlyIdentities()
        {
            var store = new SourceCatalogSnapshotStore();
            var captured = Snapshot();
            var path = store.Save(root, captured);
            var loaded = store.Load(root, ProfileId);

            Assert.That(loaded.Succeeded, Is.True, loaded.Reason);
            Assert.That(loaded.Snapshot.GameIds, Is.EquivalentTo(new[] { "fixture" }));
            Assert.That(loaded.Snapshot.ProfileId, Is.EqualTo(ProfileId));
            var json = File.ReadAllText(path);
            Assert.That(json, Does.Contain("SchemaVersion"));
            Assert.That(json, Does.Not.Contain("game.exe"));
            Assert.That(json, Does.Not.Contain(cache));
            Assert.That(json, Does.Not.Contain(manifest));
            Assert.That(json, Does.Not.Contain("GameActions"));
            Assert.That(Directory.GetFiles(root, "*.tmp"), Is.Empty);
        }

        [Test]
        public void ValidReplacedSnapshotRetainsPreviousVersionAndNoTemps()
        {
            var store = new SourceCatalogSnapshotStore();
            var path = store.Save(root, Snapshot());
            store.Save(root, Snapshot(true));
            Assert.That(store.Load(root, ProfileId).Snapshot.GameCount, Is.EqualTo(2));
            Assert.That(Directory.GetFiles(root, "source-catalog-identities-backup-*.json"),
                Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(root, "*.tmp"), Is.Empty);
            Assert.That(File.Exists(path), Is.True);
        }

        [Test]
        public void TamperedFileIsRejectedAndNeverOverwritten()
        {
            var store = new SourceCatalogSnapshotStore();
            var path = store.Save(root, Snapshot());
            File.WriteAllText(path, File.ReadAllText(path).Replace("fixture", "forged"));
            Assert.That(store.Load(root, ProfileId).Succeeded, Is.False);
            Assert.Throws<InvalidOperationException>(() => store.Save(root, Snapshot()));
            Assert.That(File.ReadAllText(path), Does.Contain("forged"));
            Assert.That(Directory.GetFiles(root, "*.tmp"), Is.Empty);
        }

        [Test]
        public void UnsupportedSchemaIsNotAdoptedOrOverwritten()
        {
            var store = new SourceCatalogSnapshotStore();
            var path = store.Save(root, Snapshot());
            File.WriteAllText(path, File.ReadAllText(path).Replace(
                "\"SchemaVersion\":1", "\"SchemaVersion\":99"));
            Assert.That(store.Load(root, ProfileId).Succeeded, Is.False);
            Assert.Throws<InvalidOperationException>(() => store.Save(root, Snapshot()));
        }

        [Test]
        public void WrongProfileIdAndTraversalNamesAreRejected()
        {
            var store = new SourceCatalogSnapshotStore();
            store.Save(root, Snapshot());
            var other = Guid.NewGuid().ToString("D");
            Assert.That(store.Load(root, other).Succeeded, Is.False);
            Assert.Throws<InvalidOperationException>(() => store.Load(root, "../escape"));
            Assert.Throws<InvalidOperationException>(() => store.Load(root, "not-a-guid"));
        }

        [Test]
        public void OversizedAndCorruptedDocumentsFailClosed()
        {
            var store = new SourceCatalogSnapshotStore();
            var path = store.Save(root, Snapshot());
            File.WriteAllText(path, new string('x', SourceCatalogSnapshotStore.MaxFileBytes + 1));
            Assert.That(store.Load(root, ProfileId).Succeeded, Is.False);
            File.WriteAllText(path, "{broken");
            Assert.That(store.Load(root, ProfileId).Succeeded, Is.False);
            File.WriteAllBytes(path, new byte[] { 0xff, 0xfe, 0xff });
            Assert.That(store.Load(root, ProfileId).Succeeded, Is.False);
        }

        [Test]
        public void MissingRootOrTargetNeverCreatesDirectories()
        {
            var store = new SourceCatalogSnapshotStore();
            var snapshot = Snapshot();
            var missing = Path.Combine(root, "missing");
            Assert.Throws<DirectoryNotFoundException>(() => store.Save(missing, snapshot));
            Assert.That(Directory.Exists(missing), Is.False);
            Assert.That(store.Load(root, ProfileId).Succeeded, Is.False);
        }

        [Test]
        public void DurableSnapshotStillCannotAuthorizeOfflineImport()
        {
            var profile = Profile();
            var shared = Shared();
            var snapshot = Snapshot();
            var store = new SourceCatalogSnapshotStore();
            store.Save(root, snapshot);
            var loaded = store.Load(root, ProfileId);
            var policy = new SourceCatalogContinuityPolicy();
            var examined = policy.Examine(profile, shared,
                ImportOutcome.Failure(ImportFailureKind.SourceUnavailable,
                    "offline", "disconnected"), loaded.Snapshot);
            Assert.That(examined.HasOfflineIdentityCandidate, Is.True);
            Assert.That(examined.RetainedGameCount, Is.EqualTo(1));
            File.Delete(manifest);
            // Existing live multi-source coordinator has no history parameter,
            // and therefore must continue to reject the incomplete catalog.
            var plan = new MultiSourceRuntimeCoordinator().Prepare(
                new[] { profile }, shared, null);
            Assert.That(plan.CanPublish, Is.False);
            Assert.That(plan.Games, Is.Empty);
        }

        [Test]
        public void ChangedSourceBindingCannotUseLoadedSnapshot()
        {
            var profile = Profile();
            var shared = Shared();
            var store = new SourceCatalogSnapshotStore();
            store.Save(root, Snapshot());
            var loaded = store.Load(root, ProfileId);
            profile.LocalManifestPath = Path.Combine(root, "moved.json");
            var examined = new SourceCatalogContinuityPolicy().Examine(profile,
                shared, ImportOutcome.Failure(ImportFailureKind.SourceUnavailable,
                    "offline", "disconnected"), loaded.Snapshot);
            Assert.That(examined.State, Is.EqualTo(
                SourceCatalogContinuityState.HoldChangedBinding));
        }

        [Test]
        public void FilePathOccupiedByDirectoryIsNeverDeleted()
        {
            var store = new SourceCatalogSnapshotStore();
            var path = store.Save(root, Snapshot());
            File.Delete(path);
            Directory.CreateDirectory(path);
            Assert.Throws<InvalidOperationException>(() => store.Save(root, Snapshot()));
            Assert.That(Directory.Exists(path), Is.True);
        }
    }
}
