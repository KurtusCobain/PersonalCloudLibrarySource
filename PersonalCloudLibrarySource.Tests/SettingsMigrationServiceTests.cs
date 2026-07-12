using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests
{
    [TestFixture]
    public class SettingsMigrationServiceTests
    {
        [Test]
        public void Migrate_LegacySettings_PreservesExistingConfigurationAndAddsSafeDefaults()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
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
            Assert.That(settings.SettingsVersion, Is.EqualTo(PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion));
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
        public void Migrate_LegacyBaseSettings_PromotesToV3AndPreservesConfiguration()
        {
            PersonalCloudLibrarySourceSettings legacySettings = new PersonalCloudLibrarySourceSettings
            {
                Enabled = false,
                LibraryDisplayName = "My Cloud Library",
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                LocalManifestPath = @"D:\Catalog\library.json",
                LocalLibraryRoot = @"D:\Games",
                ManifestRelativePath = "catalog/library.json",
                LocalCacheFolder = @"E:\PlayniteCache",
                TreatMissingFilesAsUninstalled = false,
                RcloneExecutablePath = @"C:\Tools\rclone.exe",
                RcloneRemoteName = "romcade_drive",
                RcloneManifestPath = "PersonalLibrary/library.json",
                RcloneContentRoot = "PersonalLibrary",
                RcloneTimeoutSeconds = 75,
                AllowDownloads = false,
                EnableDiagnostics = false,
                UninstallBehavior = PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior,
                AllowUninstallOutsideCacheFolder = true,
                AutoRefreshOnApplicationStart = true,
                AutoGenerateManifestOnApplicationStart = true,
                LastManifestGeneratedAt = "2026-07-12T12:00:00Z",
                LastGeneratedManifestPath = @"D:\Catalog\generated.json",
                LastGeneratedReportPath = @"D:\Catalog\report.txt",
                LastManifestItemCount = 42
            };

            var result = SettingsMigrationService.Migrate(legacySettings);

            Assert.That(result.Settings, Is.TypeOf<PersonalCloudLibrarySourceSettingsV3>());
            Assert.That(result.WasMigrated, Is.True);
            Assert.That(result.PreviousVersion, Is.EqualTo(0));
            Assert.That(result.Settings.SettingsVersion, Is.EqualTo(PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion));
            Assert.That(result.Settings.Enabled, Is.False);
            Assert.That(result.Settings.LibraryDisplayName, Is.EqualTo("My Cloud Library"));
            Assert.That(result.Settings.SourceProviderType, Is.EqualTo(PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType));
            Assert.That(result.Settings.LocalManifestPath, Is.EqualTo(@"D:\Catalog\library.json"));
            Assert.That(result.Settings.LocalLibraryRoot, Is.EqualTo(@"D:\Games"));
            Assert.That(result.Settings.ManifestRelativePath, Is.EqualTo("catalog/library.json"));
            Assert.That(result.Settings.LocalCacheFolder, Is.EqualTo(@"E:\PlayniteCache"));
            Assert.That(result.Settings.TreatMissingFilesAsUninstalled, Is.False);
            Assert.That(result.Settings.RcloneExecutablePath, Is.EqualTo(@"C:\Tools\rclone.exe"));
            Assert.That(result.Settings.RcloneRemoteName, Is.EqualTo("romcade_drive"));
            Assert.That(result.Settings.RcloneManifestPath, Is.EqualTo("PersonalLibrary/library.json"));
            Assert.That(result.Settings.RcloneContentRoot, Is.EqualTo("PersonalLibrary"));
            Assert.That(result.Settings.RcloneTimeoutSeconds, Is.EqualTo(75));
            Assert.That(result.Settings.AllowDownloads, Is.False);
            Assert.That(result.Settings.EnableDiagnostics, Is.False);
            Assert.That(result.Settings.UninstallBehavior, Is.EqualTo(PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior));
            Assert.That(result.Settings.AllowUninstallOutsideCacheFolder, Is.True);
            Assert.That(result.Settings.AutoRefreshOnApplicationStart, Is.True);
            Assert.That(result.Settings.AutoGenerateManifestOnApplicationStart, Is.True);
            Assert.That(result.Settings.LastManifestGeneratedAt, Is.EqualTo("2026-07-12T12:00:00Z"));
            Assert.That(result.Settings.LastGeneratedManifestPath, Is.EqualTo(@"D:\Catalog\generated.json"));
            Assert.That(result.Settings.LastGeneratedReportPath, Is.EqualTo(@"D:\Catalog\report.txt"));
            Assert.That(result.Settings.LastManifestItemCount, Is.EqualTo(42));
        }

        [Test]
        public void CloneForEditing_V3Settings_PreservesRuntimeTypeAndV3Preferences()
        {
            PersonalCloudLibrarySourceSettings settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SettingsVersion = PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion,
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneRemoteName = "romcade_drive",
                ShowTopPanelButton = false,
                ShowSidebarDashboard = false,
                OpenDashboardAtStartup = true,
                TransferConcurrency = 4
            };

            var clone = SettingsMigrationService.CloneForEditing(settings);
            var v3Clone = clone as PersonalCloudLibrarySourceSettingsV3;

            Assert.That(v3Clone, Is.Not.Null);
            Assert.That(v3Clone, Is.Not.SameAs(settings));
            Assert.That(v3Clone.SettingsVersion, Is.EqualTo(PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion));
            Assert.That(v3Clone.RcloneRemoteName, Is.EqualTo("romcade_drive"));
            Assert.That(v3Clone.ShowTopPanelButton, Is.False);
            Assert.That(v3Clone.ShowSidebarDashboard, Is.False);
            Assert.That(v3Clone.OpenDashboardAtStartup, Is.True);
            Assert.That(v3Clone.TransferConcurrency, Is.EqualTo(4));
        }

        [Test]
        public void Migrate_CurrentSettings_DoesNotOverwriteUserPreferences()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SettingsVersion = PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion,
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
            Assert.That(result.PreviousVersion, Is.EqualTo(PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion));
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
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SettingsVersion = PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion,
                TransferConcurrency = invalidValue
            };

            SettingsMigrationService.Migrate(settings);

            Assert.That(settings.TransferConcurrency, Is.EqualTo(1));
        }
    }
}
