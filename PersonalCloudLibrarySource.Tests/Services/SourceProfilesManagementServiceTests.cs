using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class SourceProfilesManagementServiceTests
    {
        private string root;
        private string manifest;
        private string cache;
        private SourceProfilesManagementService manager;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-SourceProfilesEditor",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            manifest = Path.Combine(root, "library.json");
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            File.WriteAllText(manifest, "{\"version\":3,\"items\":[]}");
            manager = new SourceProfilesManagementService();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private PersonalCloudLibrarySourceSettingsV3 Settings()
            => new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true, SourceProviderType = "LocalFile",
                LocalManifestPath = manifest, LocalCacheFolder = cache,
                AllowUninstallOutsideCacheFolder = false
            };

        [Test]
        public void PreviewLegacyIsInMemoryUntilExplicitSave()
        {
            var original = Settings();
            var guid = Guid.NewGuid();
            var preview = manager.OpenOrPreviewLegacy(root, original, guid);

            Assert.That(preview, Has.Count.EqualTo(1));
            Assert.That(preview[0].IsLegacySingleSource, Is.True);
            Assert.That(preview[0].ProfileId, Is.EqualTo(guid.ToString("D")));
            Assert.That(preview[0].LocalManifestPath, Is.EqualTo(manifest));
            Assert.That(File.Exists(Path.Combine(root, SourceProfilesFileStore.FileName)), Is.False);
            Assert.That(original.EnableMultiSourceProfiles, Is.False);
        }

        [Test]
        public void AddingAndSavingTwoSourcesDoesNotEnableTheLiveFeature()
        {
            var settings = Settings();
            var baseline = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            var editor = manager.Clone(baseline);
            manager.AddProfile(editor, "LocalFile", Guid.NewGuid());
            editor[1].LocalManifestPath = Path.Combine(root, "second.json");
            editor[1].DisplayName = "Second disk";
            manager.SaveWithoutActivation(root, baseline, editor, settings);

            var loaded = new SourceProfilesFileStore().Load(root);
            Assert.That(loaded.Succeeded, Is.True, loaded.Error);
            Assert.That(loaded.Profiles, Has.Count.EqualTo(2));
            Assert.That(loaded.Profiles[0].ProfileId, Is.EqualTo(baseline[0].ProfileId));
            Assert.That(loaded.Profiles[1].DisplayName, Is.EqualTo("Second disk"));
            Assert.That(settings.EnableMultiSourceProfiles, Is.False);
            Assert.That(File.Exists(Path.Combine(root, SourceProfilesFileStore.FileName)), Is.True);
        }

        [Test]
        public void EditingSavedProfileUsesBackupAndDetectsStaleEditor()
        {
            var settings = Settings();
            var baseline = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            manager.SaveWithoutActivation(root, baseline, baseline, settings);
            var firstEditor = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            var secondEditor = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            var revised = manager.Clone(firstEditor);
            revised[0].DisplayName = "Revised";
            var backup = manager.SaveWithoutActivation(root, firstEditor, revised, settings);
            Assert.That(new SourceProfilesFileStore().InspectBackup(
                root, Path.GetFileName(backup)).Succeeded, Is.True);
            Assert.Throws<InvalidOperationException>(() =>
                manager.SaveWithoutActivation(root, secondEditor, secondEditor, settings));
            Assert.That(new SourceProfilesFileStore().Load(root)
                .Profiles.Single().DisplayName, Is.EqualTo("Revised"));
        }

        [Test]
        public void IdentityRemovalAndLegacyFlagMutationNeverOverwrite()
        {
            var settings = Settings();
            var original = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            manager.SaveWithoutActivation(root, original, original, settings);
            var current = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            var noProfiles = manager.Clone(current);
            noProfiles.Clear();
            Assert.Throws<InvalidOperationException>(() =>
                manager.SaveWithoutActivation(root, current, noProfiles, settings));
            var wrongLegacyFlag = manager.Clone(current);
            wrongLegacyFlag[0].IsLegacySingleSource = false;
            Assert.Throws<InvalidOperationException>(() =>
                manager.SaveWithoutActivation(root, current, wrongLegacyFlag, settings));
            Assert.That(new SourceProfilesFileStore().Load(root).Succeeded, Is.True);
        }

        [Test]
        public void DisabledProfileOrInvalidNewProviderNeverGetsSaved()
        {
            var settings = Settings();
            var baseline = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            var edited = manager.Clone(baseline);
            edited[0].Enabled = false;
            Assert.Throws<InvalidOperationException>(() =>
                manager.SaveWithoutActivation(root, baseline, edited, settings));
            Assert.Throws<InvalidOperationException>(() =>
                manager.AddProfile(edited, "Unsupported", Guid.NewGuid()));
            Assert.That(File.Exists(Path.Combine(root, SourceProfilesFileStore.FileName)), Is.False);
        }

        [Test]
        public void ExistingCorruptFileRefusesPreviewAndDoesNotReplaceIt()
        {
            var path = Path.Combine(root, SourceProfilesFileStore.FileName);
            File.WriteAllText(path, "corrupt");
            Assert.Throws<InvalidOperationException>(() =>
                manager.OpenOrPreviewLegacy(root, Settings(), Guid.NewGuid()));
            Assert.That(File.ReadAllText(path), Is.EqualTo("corrupt"));
        }
    }
}
