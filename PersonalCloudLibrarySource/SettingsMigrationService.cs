using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource
{
    public sealed class SettingsMigrationResult
    {
        public SettingsMigrationResult(
            PersonalCloudLibrarySourceSettingsV3 settings,
            int previousVersion,
            bool wasMigrated,
            IEnumerable<int> appliedVersions = null,
            bool wasRecoveredFromCorruptSettings = false)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            PreviousVersion = previousVersion;
            WasMigrated = wasMigrated;
            AppliedVersions = new List<int>(appliedVersions ?? new int[0]).AsReadOnly();
            WasRecoveredFromCorruptSettings = wasRecoveredFromCorruptSettings;
        }

        public PersonalCloudLibrarySourceSettingsV3 Settings { get; }

        public int PreviousVersion { get; }

        public bool WasMigrated { get; }

        public IReadOnlyList<int> AppliedVersions { get; }

        public bool WasRecoveredFromCorruptSettings { get; }
    }

    public static class SettingsMigrationService
    {
        public static PersonalCloudLibrarySourceSettings LoadLegacyOrDefault(
            Func<PersonalCloudLibrarySourceSettings> load)
        {
            try
            {
                return load?.Invoke() ?? new PersonalCloudLibrarySourceSettings();
            }
            catch (Exception)
            {
                return new PersonalCloudLibrarySourceSettings();
            }
        }

        public static SettingsMigrationResult LoadAndMigrate(
            Func<PersonalCloudLibrarySourceSettingsV3> loadVersioned,
            PersonalCloudLibrarySourceSettings legacyFallback)
        {
            var recoveredFromCorruptSettings = false;
            PersonalCloudLibrarySourceSettings loadedSettings = null;
            try
            {
                loadedSettings = loadVersioned?.Invoke();
            }
            catch (Exception)
            {
                recoveredFromCorruptSettings = true;
            }

            var migrated = Migrate(loadedSettings ?? legacyFallback);
            return new SettingsMigrationResult(
                migrated.Settings,
                migrated.PreviousVersion,
                migrated.WasMigrated,
                migrated.AppliedVersions,
                recoveredFromCorruptSettings);
        }

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
            var appliedVersions = new List<int>();

            while (settings.SettingsVersion < PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion)
            {
                switch (settings.SettingsVersion)
                {
                    case 0:
                        ApplyVersion1(settings);
                        break;
                    case 1:
                        ApplyVersion2(settings);
                        break;
                    case 2:
                        ApplyVersion3(settings);
                        break;
                    case 3:
                        ApplyVersion4(settings);
                        break;
                    default:
                        settings.SettingsVersion = 0;
                        continue;
                }

                appliedVersions.Add(settings.SettingsVersion);
            }

            if (settings.TransferConcurrency < 1 || settings.TransferConcurrency > 4)
            {
                settings.TransferConcurrency = 1;
            }

            return new SettingsMigrationResult(
                settings,
                previousVersion,
                appliedVersions.Count > 0,
                appliedVersions);
        }

        public static PersonalCloudLibrarySourceSettings CloneForEditing(PersonalCloudLibrarySourceSettings settings)
        {
            if (settings is PersonalCloudLibrarySourceSettingsV3 versionedSettings)
            {
                var clone = PromoteLegacySettings(versionedSettings);
                CopyVersionedProperties(versionedSettings, clone);
                return clone;
            }

            return CopyLegacySettings(settings);
        }

        public static void RestoreSnapshot(
            PersonalCloudLibrarySourceSettings snapshot,
            PersonalCloudLibrarySourceSettings destination)
        {
            CopyBaseProperties(snapshot, destination);
            if (snapshot is PersonalCloudLibrarySourceSettingsV3 sourceV3 &&
                destination is PersonalCloudLibrarySourceSettingsV3 destinationV3)
            {
                CopyVersionedProperties(sourceV3, destinationV3);
            }
        }

        private static PersonalCloudLibrarySourceSettingsV3 PromoteLegacySettings(PersonalCloudLibrarySourceSettings legacySettings)
        {
            var promoted = new PersonalCloudLibrarySourceSettingsV3();
            CopyBaseProperties(legacySettings, promoted);
            return promoted;
        }

        private static void ApplyVersion1(PersonalCloudLibrarySourceSettingsV3 settings)
        {
            // 0.1.1 persisted the unversioned base settings shape.
            settings.SettingsVersion = 1;
        }

        private static void ApplyVersion2(PersonalCloudLibrarySourceSettingsV3 settings)
        {
            // 0.2.0 added startup and manifest-generation state to the same
            // unversioned base shape. Deserialization preserves those names.
            settings.SettingsVersion = 2;
        }

        private static void ApplyVersion3(PersonalCloudLibrarySourceSettingsV3 settings)
        {
            // CLR type V3 introduced the dashboard, transfer, and notification
            // preferences. Constructor defaults supply values absent from YAML.
            settings.SettingsVersion = 3;
        }

        private static void ApplyVersion4(PersonalCloudLibrarySourceSettingsV3 settings)
        {
            // Schema version 4 still uses the serialization-compatible V3 CLR
            // type. Upgrade only the old 30-second default; custom values live.
            if (settings.RcloneTimeoutSeconds == 30)
            {
                settings.RcloneTimeoutSeconds = 90;
            }

            settings.SettingsVersion = 4;
        }

        private static PersonalCloudLibrarySourceSettings CopyLegacySettings(PersonalCloudLibrarySourceSettings settings)
        {
            var clone = new PersonalCloudLibrarySourceSettings();
            CopyBaseProperties(settings, clone);
            return clone;
        }

        private static void CopyVersionedProperties(
            PersonalCloudLibrarySourceSettingsV3 source,
            PersonalCloudLibrarySourceSettingsV3 destination)
        {
            destination.SettingsVersion = source.SettingsVersion;
            destination.ShowTopPanelButton = source.ShowTopPanelButton;
            destination.ShowSidebarDashboard = source.ShowSidebarDashboard;
            destination.ShowSetupReminders = source.ShowSetupReminders;
            destination.OpenDashboardAtStartup = source.OpenDashboardAtStartup;
            destination.TransferConcurrency = source.TransferConcurrency;
            destination.VerifyAfterTransfer = source.VerifyAfterTransfer;
            destination.RemoveIncompleteTransferFiles = source.RemoveIncompleteTransferFiles;
            destination.NotifyLibraryUpdates = source.NotifyLibraryUpdates;
            destination.NotifyTransferCompleted = source.NotifyTransferCompleted;
            destination.NotifyTransferFailed = source.NotifyTransferFailed;
            destination.NotifySourceUnavailable = source.NotifySourceUnavailable;
            destination.NotifyVerificationWarnings = source.NotifyVerificationWarnings;
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
