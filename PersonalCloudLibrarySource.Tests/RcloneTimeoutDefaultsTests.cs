using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests
{
    [TestFixture]
    public class RcloneTimeoutDefaultsTests
    {
        [Test]
        public void NewSettings_DefaultToNinetySecondRcloneTimeout()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3();

            Assert.That(settings.RcloneTimeoutSeconds, Is.EqualTo(90));
        }

        [Test]
        public void NewSetupDraft_DefaultsToNinetySecondRcloneTimeout()
        {
            var draft = new SetupDraft();

            Assert.That(draft.RcloneTimeoutSeconds, Is.EqualTo(90));
        }

        [Test]
        public void Migrate_PreviousDefaultTimeout_UpgradesToNinetySeconds()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SettingsVersion = 3,
                RcloneTimeoutSeconds = 30
            };

            var result = SettingsMigrationService.Migrate(settings);

            Assert.That(result.WasMigrated, Is.True);
            Assert.That(settings.SettingsVersion, Is.EqualTo(PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion));
            Assert.That(settings.RcloneTimeoutSeconds, Is.EqualTo(90));
        }

        [Test]
        public void Migrate_CustomTimeout_PreservesUserChoice()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SettingsVersion = 3,
                RcloneTimeoutSeconds = 75
            };

            SettingsMigrationService.Migrate(settings);

            Assert.That(settings.RcloneTimeoutSeconds, Is.EqualTo(75));
        }
    }
}
