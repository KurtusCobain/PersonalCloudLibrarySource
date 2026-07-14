using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;

namespace PersonalCloudLibrarySource.Tests
{
    [TestFixture]
    public class SettingsMigrationServiceTests
    {
        [Test]
        public void LoadAndMigrate_Deserialized011Fixture_PreservesPathsProviderAndSafetyChoices()
        {
            var result = SettingsMigrationService.LoadAndMigrate(
                () => DeserializeFlatSettingsFixture(ReadFixture("settings-v0.1.1.yaml")),
                null);

            Assert.That(result.WasRecoveredFromCorruptSettings, Is.False);
            Assert.That(result.AppliedVersions, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(result.Settings.SourceProviderType, Is.EqualTo(PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType));
            Assert.That(result.Settings.LocalManifestPath, Is.EqualTo(@"D:\Legacy\catalog.json"));
            Assert.That(result.Settings.LocalLibraryRoot, Is.EqualTo(@"D:\Legacy\Games"));
            Assert.That(result.Settings.LocalCacheFolder, Is.EqualTo(@"E:\LegacyCache"));
            Assert.That(result.Settings.RcloneExecutablePath, Is.EqualTo(@"C:\Tools\rclone.exe"));
            Assert.That(result.Settings.RcloneRemoteName, Is.EqualTo("archive"));
            Assert.That(result.Settings.RcloneManifestPath, Is.EqualTo("manifests/library.json"));
            Assert.That(result.Settings.RcloneContentRoot, Is.EqualTo("library"));
            Assert.That(result.Settings.RcloneTimeoutSeconds, Is.EqualTo(75));
            Assert.That(result.Settings.AllowDownloads, Is.False);
            Assert.That(result.Settings.TreatMissingFilesAsUninstalled, Is.False);
            Assert.That(result.Settings.UninstallBehavior, Is.EqualTo(PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior));
            Assert.That(result.Settings.AllowUninstallOutsideCacheFolder, Is.True);
        }

        [Test]
        public void LoadAndMigrate_Deserialized020Fixture_PreservesGenerationStateAndUpgradesOldTimeoutDefault()
        {
            var result = SettingsMigrationService.LoadAndMigrate(
                () => DeserializeFlatSettingsFixture(ReadFixture("settings-v0.2.0.yaml")),
                null);

            Assert.That(result.WasRecoveredFromCorruptSettings, Is.False);
            Assert.That(result.Settings.SourceProviderType, Is.EqualTo(PersonalCloudLibrarySourceSettings.LocalFolderProviderType));
            Assert.That(result.Settings.LocalManifestPath, Is.EqualTo(@"\\NAS\Catalog\library.json"));
            Assert.That(result.Settings.LocalLibraryRoot, Is.EqualTo(@"\\NAS\Games"));
            Assert.That(result.Settings.AutoRefreshOnApplicationStart, Is.True);
            Assert.That(result.Settings.AutoGenerateManifestOnApplicationStart, Is.True);
            Assert.That(result.Settings.LastManifestGeneratedAt, Is.EqualTo("2026-06-15T12:34:56Z"));
            Assert.That(result.Settings.LastGeneratedManifestPath, Is.EqualTo(@"D:\Generated\library.json"));
            Assert.That(result.Settings.LastGeneratedReportPath, Is.EqualTo(@"D:\Generated\report.txt"));
            Assert.That(result.Settings.LastManifestItemCount, Is.EqualTo(27));
            Assert.That(result.Settings.RcloneTimeoutSeconds, Is.EqualTo(90));
            Assert.That(result.Settings.UninstallBehavior, Is.EqualTo(PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior));
        }

        [Test]
        public void LoadAndMigrate_DeserializerThrows_RecoversCurrentSafeDefaults()
        {
            var result = SettingsMigrationService.LoadAndMigrate(
                () => DeserializeFlatSettingsFixture("SettingsVersion: [ definitely-not-valid"),
                null);

            Assert.That(result.WasRecoveredFromCorruptSettings, Is.True);
            Assert.That(result.Settings.SettingsVersion, Is.EqualTo(PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion));
            Assert.That(result.Settings.TransferConcurrency, Is.EqualTo(1));
            Assert.That(result.Settings.RcloneTimeoutSeconds, Is.EqualTo(90));
            Assert.That(result.Settings.VerifyAfterTransfer, Is.True);
            Assert.That(result.Settings.RemoveIncompleteTransferFiles, Is.True);
        }

        [Test]
        public void LoadAndMigrate_DeserializedPartialSettings_PreservesProvidedChoiceAndNormalizesUnsafeValue()
        {
            const string partial = "SettingsVersion: 3\nRcloneTimeoutSeconds: 75\nTransferConcurrency: 99\nAllowDownloads: false\n";

            var result = SettingsMigrationService.LoadAndMigrate(
                () => DeserializeFlatSettingsFixture(partial),
                null);

            Assert.That(result.WasRecoveredFromCorruptSettings, Is.False);
            Assert.That(result.Settings.RcloneTimeoutSeconds, Is.EqualTo(75));
            Assert.That(result.Settings.TransferConcurrency, Is.EqualTo(1));
            Assert.That(result.Settings.AllowDownloads, Is.False);
            Assert.That(result.Settings.VerifyAfterTransfer, Is.True);
        }

        [TestCase(0, new[] { 1, 2, 3, 4, 5 })]
        [TestCase(1, new[] { 2, 3, 4, 5 })]
        [TestCase(2, new[] { 3, 4, 5 })]
        [TestCase(3, new[] { 4, 5 })]
        [TestCase(4, new[] { 5 })]
        [TestCase(5, new int[0])]
        public void Migrate_AppliesEverySchemaStepInOrderAndIsIdempotent(int startingVersion, int[] expectedVersions)
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SettingsVersion = startingVersion,
                RcloneTimeoutSeconds = 75,
                TransferConcurrency = 4
            };

            var first = SettingsMigrationService.Migrate(settings);
            var second = SettingsMigrationService.Migrate(settings);

            Assert.That(first.AppliedVersions, Is.EqualTo(expectedVersions));
            Assert.That(second.AppliedVersions, Is.Empty);
            Assert.That(second.WasMigrated, Is.False);
            Assert.That(settings.RcloneTimeoutSeconds, Is.EqualTo(75));
            Assert.That(settings.TransferConcurrency, Is.EqualTo(4));
        }

        [Test]
        public void Migrate_Version4ConfiguredSetup_IsRememberedAsPreviouslyCompleted()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SettingsVersion = 4,
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = @"Z:\currently-unavailable\library.json"
            };

            SettingsMigrationService.Migrate(settings);

            Assert.That(settings.SetupCompleted, Is.True);
            Assert.That(settings.SetupDismissed, Is.False);
        }

        [Test]
        public void Migrate_Version4UnconfiguredDefaults_RemainNewSetup()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SettingsVersion = 4,
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = string.Empty
            };

            SettingsMigrationService.Migrate(settings);

            Assert.That(settings.SetupCompleted, Is.False);
            Assert.That(settings.SetupDismissed, Is.False);
        }

        [Test]
        public void LoadLegacyOrDefault_LoaderThrows_ReturnsSafeLegacyDefaults()
        {
            var settings = SettingsMigrationService.LoadLegacyOrDefault(
                () => throw new InvalidDataException("corrupt settings"));

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.SourceProviderType, Is.EqualTo(PersonalCloudLibrarySourceSettings.LocalFileProviderType));
            Assert.That(settings.RcloneTimeoutSeconds, Is.EqualTo(30));
            Assert.That(settings.AllowDownloads, Is.True);
        }

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

        private static string ReadFixture(string fileName)
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", fileName);
            return File.ReadAllText(path);
        }

        private static PersonalCloudLibrarySourceSettingsV3 DeserializeFlatSettingsFixture(string yaml)
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3();
            var lines = yaml.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                var separator = line.IndexOf(':');
                if (separator <= 0)
                {
                    throw new FormatException("Expected a flat YAML key/value entry.");
                }

                var name = line.Substring(0, separator).Trim();
                var value = line.Substring(separator + 1).Trim();
                if (value.StartsWith("[", StringComparison.Ordinal))
                {
                    throw new FormatException("Collections are not part of the historical settings shape.");
                }

                if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
                {
                    value = value.Substring(1, value.Length - 2).Replace("''", "'");
                }

                var property = typeof(PersonalCloudLibrarySourceSettingsV3).GetProperty(
                    name,
                    BindingFlags.Instance | BindingFlags.Public);
                if (property == null || !property.CanWrite)
                {
                    throw new FormatException("Unknown historical settings property: " + name);
                }

                object converted;
                if (property.PropertyType == typeof(bool))
                {
                    converted = bool.Parse(value);
                }
                else if (property.PropertyType == typeof(int))
                {
                    converted = int.Parse(value);
                }
                else if (property.PropertyType == typeof(string))
                {
                    converted = value;
                }
                else
                {
                    throw new FormatException("Unsupported fixture property type: " + property.PropertyType.Name);
                }

                property.SetValue(settings, converted);
            }

            return settings;
        }
    }
}
