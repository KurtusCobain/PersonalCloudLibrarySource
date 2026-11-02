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
        public void ExplicitRestorePreservesCurrentVersionAsNewBackup()
        {
            var settings = Settings();
            var initial = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            manager.SaveWithoutActivation(root, initial, initial, settings);
            var beforeEdit = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            var edited = manager.Clone(beforeEdit);
            edited[0].DisplayName = "Accidentally changed";
            var oldBackup = manager.SaveWithoutActivation(root, beforeEdit, edited, settings);

            var current = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            var selected = Path.GetFileName(oldBackup);
            var preview = manager.PreviewBackupForRestore(root, selected, current, settings);
            Assert.That(preview[0].ProfileId, Is.EqualTo(current[0].ProfileId));
            Assert.That(preview[0].DisplayName, Is.EqualTo(initial[0].DisplayName));
            Assert.That(new SourceProfilesFileStore().Load(root)
                .Profiles.Single().DisplayName, Is.EqualTo("Accidentally changed"),
                "A preview may not restore on its own.");

            var rollbackBackup = manager.RestoreBackupWithoutActivation(
                root, selected, current, preview, settings);

            Assert.That(new SourceProfilesFileStore().Load(root)
                .Profiles.Single().DisplayName, Is.EqualTo(initial[0].DisplayName));
            Assert.That(new SourceProfilesFileStore()
                .InspectBackup(root, Path.GetFileName(rollbackBackup))
                .Profiles.Single().DisplayName, Is.EqualTo("Accidentally changed"));
            Assert.That(File.Exists(oldBackup), Is.True,
                "Restoration must not consume or delete the selected backup.");
            Assert.That(settings.EnableMultiSourceProfiles, Is.False);
        }

        [Test]
        public void RestoreRefusesBackupThatWouldRetireNewlyAddedSource()
        {
            var settings = Settings();
            var initial = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            manager.SaveWithoutActivation(root, initial, initial, settings);
            var beforeAddition = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            var expanded = manager.Clone(beforeAddition);
            manager.AddProfile(expanded, "LocalFile", Guid.NewGuid());
            expanded[1].LocalManifestPath = manifest;
            var olderBackup = manager.SaveWithoutActivation(
                root, beforeAddition, expanded, settings);

            var current = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            Assert.Throws<InvalidOperationException>(() =>
                manager.PreviewBackupForRestore(
                    root, Path.GetFileName(olderBackup), current, settings));
            Assert.That(new SourceProfilesFileStore().Load(root).Profiles, Has.Count.EqualTo(2));
        }

        [Test]
        public void RestoreRefusesChangedOrTamperedBackupAndStaleCurrentState()
        {
            var settings = Settings();
            var original = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            manager.SaveWithoutActivation(root, original, original, settings);
            var before = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            var revised = manager.Clone(before);
            revised[0].DisplayName = "Changed once";
            var backupPath = manager.SaveWithoutActivation(root, before, revised, settings);
            var active = manager.OpenOrPreviewLegacy(root, settings, Guid.NewGuid());
            var name = Path.GetFileName(backupPath);
            var preview = manager.PreviewBackupForRestore(root, name, active, settings);
            preview[0].DisplayName = "Forged preview";
            Assert.Throws<InvalidOperationException>(() =>
                manager.RestoreBackupWithoutActivation(
                    root, name, active, preview, settings));

            File.WriteAllText(backupPath, "corrupted backup");
            Assert.Throws<InvalidOperationException>(() =>
                manager.PreviewBackupForRestore(root, name, active, settings));
            Assert.That(new SourceProfilesFileStore().Load(root)
                .Profiles.Single().DisplayName, Is.EqualTo("Changed once"));
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
