using Playnite.SDK.Data;
using System;

namespace PersonalCloudLibrarySource
{
    public sealed class SettingsMigrationResult
    {
        public SettingsMigrationResult(
            PersonalCloudLibrarySourceSettingsV3 settings,
            int previousVersion,
            bool wasMigrated)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            PreviousVersion = previousVersion;
            WasMigrated = wasMigrated;
        }

        public PersonalCloudLibrarySourceSettingsV3 Settings { get; }

        public int PreviousVersion { get; }

        public bool WasMigrated { get; }
    }

    public static class SettingsMigrationService
    {
        public static SettingsMigrationResult Migrate(PersonalCloudLibrarySourceSettings settings)
        {
            var versionedSettings = settings as PersonalCloudLibrarySourceSettingsV3;
            if (versionedSettings == null)
            {
                versionedSettings = PromoteLegacySettings(settings);
            }

            return Migrate(versionedSettings);
        }

        public static SettingsMigrationResult Migrate(PersonalCloudLibrarySourceSettingsV3 settings)
        {
            settings = settings ?? new PersonalCloudLibrarySourceSettingsV3();

            var previousVersion = settings.SettingsVersion;
            var wasMigrated = previousVersion < PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion;

            if (settings.TransferConcurrency < 1 || settings.TransferConcurrency > 4)
            {
                settings.TransferConcurrency = 1;
            }

            if (wasMigrated)
            {
                settings.SettingsVersion = PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion;
            }

            return new SettingsMigrationResult(settings, previousVersion, wasMigrated);
        }

        public static PersonalCloudLibrarySourceSettings CloneForEditing(PersonalCloudLibrarySourceSettings settings)
        {
            if (settings is PersonalCloudLibrarySourceSettingsV3 versionedSettings)
            {
                return Serialization.GetClone(versionedSettings);
            }

            return Serialization.GetClone(settings ?? new PersonalCloudLibrarySourceSettings());
        }

        private static PersonalCloudLibrarySourceSettingsV3 PromoteLegacySettings(PersonalCloudLibrarySourceSettings legacySettings)
        {
            if (legacySettings == null)
            {
                return new PersonalCloudLibrarySourceSettingsV3();
            }

            return new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = legacySettings.Enabled,
                LibraryDisplayName = legacySettings.LibraryDisplayName,
                SourceProviderType = legacySettings.SourceProviderType,
                LocalManifestPath = legacySettings.LocalManifestPath,
                LocalLibraryRoot = legacySettings.LocalLibraryRoot,
                ManifestRelativePath = legacySettings.ManifestRelativePath,
                LocalCacheFolder = legacySettings.LocalCacheFolder,
                TreatMissingFilesAsUninstalled = legacySettings.TreatMissingFilesAsUninstalled,
                RcloneExecutablePath = legacySettings.RcloneExecutablePath,
                RcloneRemoteName = legacySettings.RcloneRemoteName,
                RcloneManifestPath = legacySettings.RcloneManifestPath,
                RcloneContentRoot = legacySettings.RcloneContentRoot,
                RcloneTimeoutSeconds = legacySettings.RcloneTimeoutSeconds,
                AllowDownloads = legacySettings.AllowDownloads,
                EnableDiagnostics = legacySettings.EnableDiagnostics,
                UninstallBehavior = legacySettings.UninstallBehavior,
                AllowUninstallOutsideCacheFolder = legacySettings.AllowUninstallOutsideCacheFolder,
                AutoRefreshOnApplicationStart = legacySettings.AutoRefreshOnApplicationStart,
                AutoGenerateManifestOnApplicationStart = legacySettings.AutoGenerateManifestOnApplicationStart,
                LastManifestGeneratedAt = legacySettings.LastManifestGeneratedAt,
                LastGeneratedManifestPath = legacySettings.LastGeneratedManifestPath,
                LastGeneratedReportPath = legacySettings.LastGeneratedReportPath,
                LastManifestItemCount = legacySettings.LastManifestItemCount
            };
        }
    }
}
