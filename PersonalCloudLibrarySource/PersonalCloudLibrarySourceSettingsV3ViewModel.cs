using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public sealed class PersonalCloudLibrarySourceSettingsV3ViewModel : PersonalCloudLibrarySourceSettingsViewModel, ISettings
    {
        private readonly PersonalCloudLibrarySource plugin;

        public PersonalCloudLibrarySourceSettingsV3ViewModel(PersonalCloudLibrarySource plugin)
            : base(plugin)
        {
            this.plugin = plugin;

            var migration = SettingsMigrationService.LoadAndMigrate(
                () => plugin.LoadPluginSettings<PersonalCloudLibrarySourceSettingsV3>(),
                base.Settings);
            base.Settings = migration.Settings;
            UpdateRuntimeSettingsSnapshot();

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

        public new PersonalCloudLibrarySourceSettingsV3 GetRuntimeSettingsSnapshot()
        {
            return (PersonalCloudLibrarySourceSettingsV3)base.GetRuntimeSettingsSnapshot();
        }

        public void MarkSetupCompleted()
        {
            new SetupStatePersistenceService().MarkCompleted(Settings);
        }

        public void PersistSetupDismissed()
        {
            new SetupStatePersistenceService().MarkDismissed(
                Settings,
                snapshot => plugin.SavePluginSettings(snapshot));
            UpdateRuntimeSettingsSnapshot();
            NotifySettingsCommitted();
        }

        public void PersistGeneratedManifestState(ManifestGenerationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            Settings.LocalManifestPath = report.OutputPath;
            Settings.ManifestRelativePath = string.Empty;
            Settings.LastGeneratedManifestPath = report.OutputPath;
            Settings.LastGeneratedReportPath = report.ReportPath;
            Settings.LastManifestGeneratedAt = report.Manifest.GeneratedAt;
            Settings.LastManifestItemCount = report.ItemCount;
            plugin.SavePluginSettings(SettingsMigrationService.CloneForEditing(Settings));
            UpdateRuntimeSettingsSnapshot();
            NotifySettingsCommitted();
        }

        public override bool VerifySettings(out List<string> errors)
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
                    ? PclsResources.Get("LOCPLSVerificationCompleted", "Setup verification completed.")
                    : PclsResources.Get("LOCPLSVerificationIssues", "Setup verification found issues.");
                SetupStatusDetails = report.ManifestLoadSucceeded
                    ? PclsResources.Format("LOCPLSVerificationItemsChecked", "{0} manifest items were checked.", report.TotalManifestItems)
                    : PclsResources.Format("LOCPLSManifestLoadFailed", "Manifest load failed: {0}", report.ManifestLoadError);

                MessageBox.Show(
                    VerificationMessageBuilder.Build(report),
                    PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
            }
            catch (Exception ex)
            {
                VerificationDashboardStateService.LatestReport = null;
                SetupStatusHeadline = PclsResources.Get("LOCPLSVerificationFailed", "Setup verification failed.");
                SetupStatusDetails = ex.Message;
                MessageBox.Show(
                    PclsResources.Format("LOCPLSVerificationFailedReason", "Setup verification failed: {0}", ex.Message),
                    PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
            }
        }
    }
}
