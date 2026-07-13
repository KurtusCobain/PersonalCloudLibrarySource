using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public sealed class PersonalCloudLibrarySourceSettingsV3ViewModel : PersonalCloudLibrarySourceSettingsViewModel, ISettings
    {
        private readonly PersonalCloudLibrarySource plugin;
        private PersonalCloudLibrarySourceSettingsV3 editingClone;

        public PersonalCloudLibrarySourceSettingsV3ViewModel(PersonalCloudLibrarySource plugin)
            : base(plugin)
        {
            this.plugin = plugin;

            var loadedSettings = plugin.LoadPluginSettings<PersonalCloudLibrarySourceSettingsV3>();
            var sourceSettings = loadedSettings != null
                ? (PersonalCloudLibrarySourceSettings)loadedSettings
                : base.Settings;
            var migration = SettingsMigrationService.Migrate(sourceSettings);
            base.Settings = migration.Settings;

            if (migration.WasMigrated)
            {
                plugin.SavePluginSettings(migration.Settings);
            }
        }

        public new PersonalCloudLibrarySourceSettingsV3 Settings
        {
            get
            {
                var versionedSettings = base.Settings as PersonalCloudLibrarySourceSettingsV3;
                if (versionedSettings != null)
                {
                    return versionedSettings;
                }

                var migration = SettingsMigrationService.Migrate(base.Settings);
                base.Settings = migration.Settings;
                plugin.SavePluginSettings(migration.Settings);
                return migration.Settings;
            }
            set => base.Settings = value ?? new PersonalCloudLibrarySourceSettingsV3
            {
                SettingsVersion = PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion
            };
        }

        public new void BeginEdit()
        {
            editingClone = (PersonalCloudLibrarySourceSettingsV3)SettingsMigrationService.CloneForEditing(Settings);
        }

        public new void CancelEdit()
        {
            Settings = editingClone ?? SettingsMigrationService.Migrate(base.Settings).Settings;
            editingClone = null;
        }

        public new void EndEdit()
        {
            base.Settings = Settings;
            base.EndEdit();
            editingClone = null;
        }

        public new bool VerifySettings(out List<string> errors)
        {
            base.Settings = Settings;
            return base.VerifySettings(out errors);
        }

        public new void VerifySetup()
        {
            try
            {
                List<string> errors;
                VerifySettings(out errors);
                var report = plugin.GenerateVerificationReport(Settings, errors);
                VerificationDashboardStateService.LatestReport = report;

                var passed = report.ConfigurationErrorsCount == 0 && report.ManifestLoadSucceeded;
                SetupStatusHeadline = passed
                    ? "Setup verification completed."
                    : "Setup verification found issues.";
                SetupStatusDetails = report.ManifestLoadSucceeded
                    ? report.TotalManifestItems + " manifest items were checked."
                    : "Manifest load failed: " + report.ManifestLoadError;

                MessageBox.Show(
                    VerificationMessageBuilder.Build(report),
                    "Personal Cloud Library Source");
            }
            catch (Exception ex)
            {
                VerificationDashboardStateService.LatestReport = null;
                SetupStatusHeadline = "Setup verification failed.";
                SetupStatusDetails = ex.Message;
                MessageBox.Show(
                    "Setup verification failed: " + ex.Message,
                    "Personal Cloud Library Source");
            }
        }
    }
}
