using NUnit.Framework;
using System.Collections.Generic;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Setup
{
    [TestFixture]
    public class SetupStatePersistenceServiceTests
    {
        [Test]
        public void MarkDismissed_AfterCancel_PreservesRestoredValuesAndSavesStateExactlyOnce()
        {
            var original = new PersonalCloudLibrarySourceSettingsV3
            {
                LibraryDisplayName = "Saved library",
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = "saved.json",
                SetupCompleted = false,
                SetupDismissed = false
            };
            var working = SettingsMigrationService.CloneForEditing(original) as PersonalCloudLibrarySourceSettingsV3;
            working.LibraryDisplayName = "Leaked draft";
            working.LocalManifestPath = "draft.json";
            SettingsMigrationService.RestoreSnapshot(original, working);
            var saved = new List<PersonalCloudLibrarySourceSettingsV3>();

            new SetupStatePersistenceService().MarkDismissed(
                working,
                snapshot => saved.Add(snapshot));

            Assert.That(working.LibraryDisplayName, Is.EqualTo("Saved library"));
            Assert.That(working.LocalManifestPath, Is.EqualTo("saved.json"));
            Assert.That(working.SetupDismissed, Is.True);
            Assert.That(saved, Has.Count.EqualTo(1));
            Assert.That(saved[0], Is.Not.SameAs(working));
            Assert.That(saved[0].LibraryDisplayName, Is.EqualTo("Saved library"));
            Assert.That(saved[0].LocalManifestPath, Is.EqualTo("saved.json"));
            Assert.That(saved[0].SetupDismissed, Is.True);
        }

        [Test]
        public void MarkDismissed_FailedSaveDoesNotChangeWorkingState()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3 { SetupDismissed = false };
            Assert.Throws<IOException>(() => new SetupStatePersistenceService().MarkDismissed(
                settings, ignored => { throw new IOException("fixture"); }));
            Assert.That(settings.SetupDismissed, Is.False);
        }

        [Test]
        public void ApplyGeneratedManifest_RejectsStaleSourceOrChangedManifestPointer()
        {
            var expected = new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true, SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                LocalLibraryRoot = @"C:\Fixture", LocalManifestPath = "original.json"
            };
            var candidate = (PersonalCloudLibrarySourceSettingsV3)SettingsMigrationService.CloneForEditing(expected);
            var report = new ManifestGenerationReport
            {
                SourceRoot = expected.LocalLibraryRoot, OutputPath = "new.json",
                Manifest = new PersonalCloudLibraryManifest(), ItemCount = 7
            };
            candidate.LocalManifestPath = "newer.json";
            Assert.Throws<InvalidOperationException>(() =>
                new SetupStatePersistenceService().ApplyGeneratedManifest(candidate, report, expected));
            Assert.That(candidate.LocalManifestPath, Is.EqualTo("newer.json"));
            candidate.LocalManifestPath = expected.LocalManifestPath;
            report.SourceRoot = @"C:\Different";
            Assert.Throws<InvalidOperationException>(() =>
                new SetupStatePersistenceService().ApplyGeneratedManifest(candidate, report, expected));
            report.SourceRoot = expected.LocalLibraryRoot;
            new SetupStatePersistenceService().ApplyGeneratedManifest(candidate, report, expected);
            Assert.That(candidate.LocalManifestPath, Is.EqualTo("new.json"));
            Assert.That(candidate.LastManifestItemCount, Is.EqualTo(7));
        }

        [Test]
        public void GeneratedOutputNamesAreIsolatedAndDoNotCreateFiles()
        {
            var output = Path.Combine(Path.GetTempPath(), "PCLS-Output-" + Guid.NewGuid().ToString("N"));
            var first = ManifestGenerationOptions.CreateIsolated(@"C:\Fixture", output, null);
            var second = ManifestGenerationOptions.CreateIsolated(@"C:\Fixture", output, null);
            Assert.That(first.OutputPath, Is.Not.EqualTo(second.OutputPath));
            Assert.That(first.ReportPath, Is.Not.EqualTo(second.ReportPath));
            Assert.That(Path.GetDirectoryName(first.OutputPath), Is.EqualTo(output));
            Assert.That(Directory.Exists(output), Is.False);
        }

        [Test]
        public void MarkCompleted_ChangesStateWithoutSavingOutsideValidatedEdit()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SetupCompleted = false,
                SetupDismissed = true
            };

            new SetupStatePersistenceService().MarkCompleted(settings);

            Assert.That(settings.SetupCompleted, Is.True);
            Assert.That(settings.SetupDismissed, Is.False);
        }
    }
}
