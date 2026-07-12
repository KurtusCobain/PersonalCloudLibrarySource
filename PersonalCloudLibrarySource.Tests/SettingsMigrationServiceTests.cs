using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests
{
    [TestFixture]
    public class SettingsMigrationServiceTests
    {
        [Test]
        public void Migrate_LegacySettings_PreservesExistingConfigurationAndAddsSafeDefaults()
        {
            var settings = new PersonalCloudLibrarySourceSettings
            {
                SettingsVersion = 0,
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneExecutablePath = @"C:\Tools\rclone.exe",
                RcloneRemoteName = "games",
                RcloneManifestPath = "catalog/library.json",
                RcloneContentRoot = "content",
                LocalCacheFolder = @"D:\PlayniteCache",
                AllowDownloads = false,
                AllowUninstallOutsideCacheFolder = true,
                UninstallBehavior = PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior
            };

            var result = SettingsMigrationService.Migrate(settings);

            Assert.That(result.WasMigrated, Is.True);
            Assert.That(result.PreviousVersion, Is.EqualTo(0));
            Assert.That(settings.SettingsVersion, Is.EqualTo(PersonalCloudLibrarySourceSettings.CurrentSettingsVersion));
            Assert.That(settings.SourceProviderType, Is.EqualTo(PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType));
            Assert.That(settings.RcloneExecutablePath, Is.EqualTo(@"C:\Tools\rclone.exe"));
            Assert.That(settings.RcloneRemoteName, Is.EqualTo("games"));
            Assert.That(settings.RcloneManifestPath, Is.EqualTo("catalog/library.json"));
            Assert.That(settings.RcloneContentRoot, Is.EqualTo("content"));
            Assert.That(settings.LocalCacheFolder, Is.EqualTo(@"D:\PlayniteCache"));
            Assert.That(settings.AllowDownloads, Is.False);
            Assert.That(settings.AllowUninstallOutsideCacheFolder, Is.True);
            Assert.That(settings.UninstallBehavior, Is.EqualTo(PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior));
            Assert.That(settings.ShowTopPanelButton, Is.True);
            Assert.That(settings.ShowSidebarDashboard, Is.True);
            Assert.That(settings.ShowSetupReminders, Is.True);
            Assert.That(settings.OpenDashboardAtStartup, Is.False);
            Assert.That(settings.TransferConcurrency, Is.EqualTo(1));
            Assert.That(settings.VerifyAfterTransfer, Is.True);
            Assert.That(settings.RemoveIncompleteTransferFiles, Is.True);
        }

        [Test]
        public void Migrate_CurrentSettings_DoesNotOverwriteUserPreferences()
        {
            var settings = new PersonalCloudLibrarySourceSettings
            {
                SettingsVersion = PersonalCloudLibrarySourceSettings.CurrentSettingsVersion,
                ShowTopPanelButton = false,
                ShowSidebarDashboard = false,
                ShowSetupReminders = false,
                OpenDashboardAtStartup = true,
                TransferConcurrency = 4,
                VerifyAfterTransfer = false,
                RemoveIncompleteTransferFiles = false
            };

            var result = SettingsMigrationService.Migrate(settings);

            Assert.That(result.WasMigrated, Is.False);
            Assert.That(result.PreviousVersion, Is.EqualTo(PersonalCloudLibrarySourceSettings.CurrentSettingsVersion));
            Assert.That(settings.ShowTopPanelButton, Is.False);
            Assert.That(settings.ShowSidebarDashboard, Is.False);
            Assert.That(settings.ShowSetupReminders, Is.False);
            Assert.That(settings.OpenDashboardAtStartup, Is.True);
            Assert.That(settings.TransferConcurrency, Is.EqualTo(4));
            Assert.That(settings.VerifyAfterTransfer, Is.False);
            Assert.That(settings.RemoveIncompleteTransferFiles, Is.False);
        }

        [TestCase(0)]
        [TestCase(5)]
        public void Migrate_InvalidTransferConcurrency_ResetsToOne(int invalidValue)
        {
            var settings = new PersonalCloudLibrarySourceSettings
            {
                SettingsVersion = PersonalCloudLibrarySourceSettings.CurrentSettingsVersion,
                TransferConcurrency = invalidValue
            };

            SettingsMigrationService.Migrate(settings);

            Assert.That(settings.TransferConcurrency, Is.EqualTo(1));
        }
    }
}
