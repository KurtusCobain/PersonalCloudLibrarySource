using NUnit.Framework;
using System.Collections.Generic;

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
