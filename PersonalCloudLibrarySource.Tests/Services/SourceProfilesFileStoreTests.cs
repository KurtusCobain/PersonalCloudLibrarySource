using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class SourceProfilesFileStoreTests
    {
        private string root;
        private const string LegacyId = "9eac75ad-97f2-41e2-8c18-177d387ee029";
        private const string NewId = "2d834471-c071-4ea7-90fa-d35e5e4accb8";

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ProfilesFileStore",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private static PclsSourceProfileDefinition Profile(string id, string name)
            => new PclsSourceProfileDefinition
            {
                ProfileId = id, DisplayName = name, ProviderType = "LocalFile",
                LocalManifestPath = "fixture.json"
            };

        [Test]
        public void NewProfileFileRoundTripsWithoutChangingInputInstances()
        {
            var first = Profile(LegacyId, "Original library");
            first.IsLegacySingleSource = true;
            var store = new SourceProfilesFileStore();

            var backup = store.Save(root, new[] { first });
            first.DisplayName = "Modified caller";
            var loaded = store.Load(root);

            Assert.That(backup, Is.Empty);
            Assert.That(loaded.Succeeded, Is.True, loaded.Error);
            Assert.That(loaded.Profiles.Single().DisplayName,
                Is.EqualTo("Original library"));
            Assert.That(loaded.Profiles.Single().ProfileId, Is.EqualTo(LegacyId));
            Assert.That(File.Exists(Path.Combine(root, SourceProfilesFileStore.FileName)),
                Is.True);
            Assert.That(Directory.GetFiles(root, "*.tmp"), Is.Empty);
        }

        [Test]
        public void UpdatingProfileAtomicallyKeepsPriorVersionInUniqueBackup()
        {
            var store = new SourceProfilesFileStore();
            store.Save(root, new[] { Profile(LegacyId, "Before update") });
            var backup = store.Save(root, new[]
            {
                Profile(LegacyId, "After update"),
                Profile(NewId, "New provider")
            });
            var active = store.Load(root);
            var historical = store.InspectBackup(root, Path.GetFileName(backup));

            Assert.That(backup, Does.StartWith(root));
            Assert.That(active.Succeeded, Is.True, active.Error);
            Assert.That(active.Profiles, Has.Count.EqualTo(2));
            Assert.That(active.Profiles[0].DisplayName, Is.EqualTo("After update"));
            Assert.That(historical.Succeeded, Is.True, historical.Error);
            Assert.That(historical.Profiles, Has.Count.EqualTo(1));
            Assert.That(historical.Profiles[0].DisplayName, Is.EqualTo("Before update"));
            Assert.That(Directory.GetFiles(root, "*.tmp"), Is.Empty);
        }

        [Test]
        public void ExistingCorruptProfileFileIsNotSilentlyAdoptedOrOverwritten()
        {
            var target = Path.Combine(root, SourceProfilesFileStore.FileName);
            File.WriteAllText(target, "corrupted legacy data");
            var store = new SourceProfilesFileStore();

            Assert.That(store.Load(root).Succeeded, Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                store.Save(root, new[] { Profile(LegacyId, "Replacement") }));
            Assert.That(File.ReadAllText(target), Is.EqualTo("corrupted legacy data"));
            Assert.That(Directory.GetFiles(root, "*.tmp"), Is.Empty);
        }

        [Test]
        public void OversizedOrUnsupportedSchemaCannotBeLoadedOrOverwritten()
        {
            var target = Path.Combine(root, SourceProfilesFileStore.FileName);
            File.WriteAllText(target, "{\"SchemaVersion\":99,\"Profiles\":[]}");
            var store = new SourceProfilesFileStore();
            Assert.That(store.Load(root).Succeeded, Is.False);
            Assert.Throws<InvalidOperationException>(() =>
                store.Save(root, new[] { Profile(LegacyId, "Test") }));

            File.WriteAllText(target, new string('x',
                SourceProfilesConfigurationCodec.MaxDocumentCharacters * 4 + 1));
            Assert.That(store.Load(root).Succeeded, Is.False);
        }

        [Test]
        public void InspectBackupRejectsTraversalAndNonBackupFiles()
        {
            var store = new SourceProfilesFileStore();
            Assert.Throws<InvalidOperationException>(() =>
                store.InspectBackup(root, "../secret.json"));
            Assert.Throws<InvalidOperationException>(() =>
                store.InspectBackup(root, SourceProfilesFileStore.FileName));
            Assert.Throws<InvalidOperationException>(() =>
                store.InspectBackup(root, @"C:\source-profiles-backup-1.json"));
        }

        [Test]
        public void MissingStoreDirectoryFailsWithoutCreatingOrWritingAnyFiles()
        {
            var directory = Path.Combine(root, "does-not-exist");
            var store = new SourceProfilesFileStore();

            Assert.Throws<DirectoryNotFoundException>(() =>
                store.Save(directory, new[] { Profile(LegacyId, "Test") }));
            Assert.That(Directory.Exists(directory), Is.False);
            Assert.That(Directory.GetFiles(root), Is.Empty);
        }

        [Test]
        public void ProfilesWithDuplicateIdentityCannotBeCommitted()
        {
            var store = new SourceProfilesFileStore();
            Assert.Throws<InvalidOperationException>(() => store.Save(root,
                new[] { Profile(LegacyId, "One"), Profile(LegacyId, "Two") }));
            Assert.That(Directory.GetFiles(root), Is.Empty);
        }
    }
}
