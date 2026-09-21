using NUnit.Framework;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests
{
    [TestFixture]
    public class SettingsEditSessionTests
    {
        [Test]
        public void CancelEdit_RestoresExactPreEditSnapshotWithoutSaving()
        {
            var original = CreateConfiguredSettings();
            var originalValues = original.GetType()
                .GetProperties()
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .ToDictionary(property => property.Name, property => property.GetValue(original));
            var saveCount = 0;
            var session = new SettingsEditSession(
                () => true,
                saved => saveCount++);

            var working = (PersonalCloudLibrarySourceSettingsV3)session.BeginEdit(original);
            Assert.That(working, Is.SameAs(original));
            working.LibraryDisplayName = "Changed";
            working.RcloneRemoteName = "changed";
            working.RcloneTimeoutSeconds = 5;
            working.TransferConcurrency = 1;
            working.ShowTopPanelButton = true;

            var restored = session.CancelEdit(working);

            Assert.That(restored, Is.SameAs(original));
            foreach (var property in restored.GetType().GetProperties().Where(property => property.CanRead && property.GetIndexParameters().Length == 0))
            {
                Assert.That(property.GetValue(restored), Is.EqualTo(originalValues[property.Name]), property.Name);
            }
            Assert.That(saveCount, Is.Zero);
        }

        [Test]
        public void EndEdit_ValidatesAndSavesOneIndependentSnapshotOnce()
        {
            var settings = CreateConfiguredSettings();
            var validationCount = 0;
            var saveCount = 0;
            PersonalCloudLibrarySourceSettings saved = null;
            var session = new SettingsEditSession(
                () =>
                {
                    validationCount++;
                    return true;
                },
                snapshot =>
                {
                    saveCount++;
                    saved = snapshot;
                });
            var working = session.BeginEdit(settings);
            working.LibraryDisplayName = "Saved once";

            Assert.That(session.EndEdit(working), Is.True);
            working.LibraryDisplayName = "Mutated after save";
            Assert.That(session.EndEdit(working), Is.False);

            Assert.That(validationCount, Is.EqualTo(1));
            Assert.That(saveCount, Is.EqualTo(1));
            Assert.That(saved, Is.Not.SameAs(working));
            Assert.That(saved.LibraryDisplayName, Is.EqualTo("Saved once"));
            var runtimeSnapshot = session.GetCommittedSnapshot();
            Assert.That(runtimeSnapshot, Is.Not.SameAs(working));
            Assert.That(runtimeSnapshot, Is.Not.SameAs(saved));
            Assert.That(runtimeSnapshot.LibraryDisplayName, Is.EqualTo("Saved once"));
        }

        [Test]
        public void EndEdit_InvalidSettingsDoesNotSaveAndKeepsSessionActiveForCorrection()
        {
            var settings = CreateConfiguredSettings();
            var isValid = false;
            var saveCount = 0;
            var session = new SettingsEditSession(
                () => isValid,
                saved => saveCount++);
            var working = session.BeginEdit(settings);

            Assert.That(session.EndEdit(working), Is.False);
            isValid = true;
            Assert.That(session.EndEdit(working), Is.True);
            Assert.That(saveCount, Is.EqualTo(1));
        }

        [Test]
        public void BeginEdit_WhenAlreadyActive_PreservesOriginalCancelSnapshot()
        {
            var settings = CreateConfiguredSettings();
            var session = new SettingsEditSession(() => true, saved => { });
            session.BeginEdit(settings);
            settings.LibraryDisplayName = "First surface edit";

            session.BeginEdit(settings);
            settings.LibraryDisplayName = "Second surface edit";
            session.CancelEdit(settings);

            Assert.That(settings.LibraryDisplayName, Is.EqualTo("Original"));
        }

        private static PersonalCloudLibrarySourceSettingsV3 CreateConfiguredSettings()
        {
            return new PersonalCloudLibrarySourceSettingsV3
            {
                SettingsVersion = PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion,
                LibraryDisplayName = "Original",
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneRemoteName = "archive",
                RcloneManifestPath = "catalog/library.json",
                RcloneTimeoutSeconds = 75,
                TransferConcurrency = 4,
                ShowTopPanelButton = false,
                VerifyAfterTransfer = false
            };
        }
    }
}
