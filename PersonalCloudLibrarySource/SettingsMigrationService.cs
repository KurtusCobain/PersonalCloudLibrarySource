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

            // Version 3 used 30 seconds as the default. Upgrade only that exact
            // legacy value so explicit user choices such as 75 seconds survive.
            if (wasMigrated && previousVersion <= 3 && settings.RcloneTimeoutSeconds == 30)
            {
                settings.RcloneTimeoutSeconds = 90;
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
                var clone = PromoteLegacySettings(versionedSettings);
                clone.SettingsVersion = versionedSettings.SettingsVersion;
                clone.ShowTopPanelButton = versionedSettings.ShowTopPanelButton;
                clone.ShowSidebarDashboard = versionedSettings.ShowSidebarDashboard;
                clone.ShowSetupReminders = versionedSettings.ShowSetupReminders;
                clone.OpenDashboardAtStartup = versionedSettings.OpenDashboardAtStartup;
                clone.TransferConcurrency = versionedSettings.TransferConcurrency;
                clone.VerifyAfterTransfer = versionedSettings.VerifyAfterTransfer;
                clone.RemoveIncompleteTransferFiles = versionedSettings.RemoveIncompleteTransferFiles;
                clone.NotifyLibraryUpdates = versionedSettings.NotifyLibraryUpdates;
                clone.NotifyTransferCompleted = versionedSettings.NotifyTransferCompleted;
                clone.NotifyTransferFailed = versionedSettings.NotifyTransferFailed;
                clone.NotifySourceUnavailable = versionedSettings.NotifySourceUnavailable;
                clone.NotifyVerificationWarnings = versionedSettings.NotifyVerificationWarnings;
                return clone;
            }

            return CopyLegacySettings(settings);
        }

        private static PersonalCloudLibrarySourceSettingsV3 PromoteLegacySettings(PersonalCloudLibrarySourceSettings legacySettings)
        {
            var promoted = new PersonalCloudLibrarySourceSettingsV3();
            CopyBaseProperties(legacySettings, promoted);
            return promoted;
        }

        private static PersonalCloudLibrarySourceSettings CopyLegacySettings(PersonalCloudLibrarySourceSettings settings)
        {
            var clone = new PersonalCloudLibrarySourceSettings();
            CopyBaseProperties(settings, clone);
            return clone;
        }

        private static void CopyBaseProperties(
            PersonalCloudLibrarySourceSettings source,
            PersonalCloudLibrarySourceSettings destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            destination.Enabled = source.Enabled;
            destination.LibraryDisplayName = source.LibraryDisplayName;
            destination.SourceProviderType = source.SourceProviderType;
            destination.LocalManifestPath = source.LocalManifestPath;
            destination.LocalLibraryRoot = source.LocalLibraryRoot;
            destination.ManifestRelativePath = source.ManifestRelativePath;
            destination.LocalCacheFolder = source.LocalCacheFolder;
            destination.TreatMissingFilesAsUninstalled = source.TreatMissingFilesAsUninstalled;
            destination.RcloneExecutablePath = source.RcloneExecutablePath;
            destination.RcloneRemoteName = source.RcloneRemoteName;
            destination.RcloneManifestPath = source.RcloneManifestPath;
            destination.RcloneContentRoot = source.RcloneContentRoot;
            destination.RcloneTimeoutSeconds = source.RcloneTimeoutSeconds;
            destination.AllowDownloads = source.AllowDownloads;
            destination.EnableDiagnostics = source.EnableDiagnostics;
            destination.UninstallBehavior = source.UninstallBehavior;
            destination.AllowUninstallOutsideCacheFolder = source.AllowUninstallOutsideCacheFolder;
            destination.AutoRefreshOnApplicationStart = source.AutoRefreshOnApplicationStart;
            destination.AutoGenerateManifestOnApplicationStart = source.AutoGenerateManifestOnApplicationStart;
            destination.LastManifestGeneratedAt = source.LastManifestGeneratedAt;
            destination.LastGeneratedManifestPath = source.LastGeneratedManifestPath;
            destination.LastGeneratedReportPath = source.LastGeneratedReportPath;
            destination.LastManifestItemCount = source.LastManifestItemCount;
        }
    }
}
