using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public sealed class PersonalCloudLibrarySourceSettingsV3ViewModel : PersonalCloudLibrarySourceSettingsViewModel, ISettings
    {
        private readonly PersonalCloudLibrarySource plugin;
        private readonly SourceProfilesManagementService profileManager =
            new SourceProfilesManagementService();
        private List<PclsSourceProfileDefinition> sourceProfilesBaseline;
        private ObservableCollection<PclsSourceProfileDefinition> sourceProfileCandidates;
        private PclsSourceProfileDefinition selectedSourceProfile;
        private string sourceProfilesStatus = PclsResources.Get(
            "LOCPLSProfilesStatusInitial",
            "Preview only. Existing one-source settings stay active until an explicitly approved release.");
        private string selectedNewSourceType = PersonalCloudLibrarySourceSettings.LocalFileProviderType;

        public ObservableCollection<PclsSourceProfileDefinition> SourceProfileCandidates
        {
            get => sourceProfileCandidates;
            private set { sourceProfileCandidates = value; OnPropertyChanged(); }
        }

        public PclsSourceProfileDefinition SelectedSourceProfile
        {
            get => selectedSourceProfile;
            set { selectedSourceProfile = value; OnPropertyChanged(); }
        }

        public string SourceProfilesStatus
        {
            get => sourceProfilesStatus;
            private set => SetValue(ref sourceProfilesStatus, value);
        }

        public string SelectedNewSourceType
        {
            get => selectedNewSourceType;
            set => SetValue(ref selectedNewSourceType, value);
        }

        public void LoadSourceProfilesPreview()
        {
            try
            {
                var baseline = profileManager.OpenOrPreviewLegacy(
                    plugin.GetPluginDataDirectory(),
                    GetRuntimeSettingsSnapshot(), Guid.NewGuid());
                sourceProfilesBaseline = profileManager.Clone(baseline);
                SourceProfileCandidates = new ObservableCollection<PclsSourceProfileDefinition>(
                    profileManager.Clone(baseline));
                SelectedSourceProfile = SourceProfileCandidates.FirstOrDefault();
                SourceProfilesStatus = PclsResources.Format(
                    "LOCPLSProfilesStatusLoaded",
                    "Loaded {0} profile(s) for editing. This does not activate multiple sources.",
                    SourceProfileCandidates.Count);
            }
            catch (Exception ex)
            {
                SourceProfilesStatus = PclsResources.Format(
                    "LOCPLSProfilesStatusLoadFailed", "Could not load source profiles: {0}", ex.Message);
            }
        }

        public void AddSourceProfilePreview()
        {
            if (SourceProfileCandidates == null)
            {
                SourceProfilesStatus = PclsResources.Get(
                    "LOCPLSProfilesStatusNeedLoad", "Load or preview the existing library first.");
                return;
            }
            try
            {
                var edited = SourceProfileCandidates.ToList();
                profileManager.AddProfile(edited, SelectedNewSourceType, Guid.NewGuid());
                SourceProfileCandidates.Add(edited.Last());
                SelectedSourceProfile = edited.Last();
                SourceProfilesStatus = PclsResources.Get(
                    "LOCPLSProfilesStatusAdded",
                    "Added an unsaved source. Complete its fields, then save the profile file.");
            }
            catch (Exception ex)
            {
                SourceProfilesStatus = PclsResources.Format(
                    "LOCPLSProfilesStatusAddFailed", "Source could not be added: {0}", ex.Message);
            }
        }

        public void SaveSourceProfilesPreview()
        {
            if (SourceProfileCandidates == null || sourceProfilesBaseline == null)
            {
                SourceProfilesStatus = PclsResources.Get(
                    "LOCPLSProfilesStatusNeedSaveLoad", "Load the current source profiles before saving.");
                return;
            }
            // Do not imply this changes the active Playnite library. The
            // existing Settings edit transaction remains separate.
            if (MessageBox.Show(
                PclsResources.Get("LOCPLSProfilesSaveConfirm",
                    "Save the source-profile document? This does not activate multi-source mode or modify installed games. The previous valid profile configuration will be backed up."),
                PclsResources.Get("LOCPLSProfilesDialogTitle", "PCLS source profiles preview"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            try
            {
                var profiles = SourceProfileCandidates.ToList();
                var backup = profileManager.SaveWithoutActivation(
                    plugin.GetPluginDataDirectory(), sourceProfilesBaseline,
                    profiles, GetRuntimeSettingsSnapshot());
                sourceProfilesBaseline = profileManager.Clone(profiles);
                SourceProfilesStatus = string.IsNullOrWhiteSpace(backup)
                    ? PclsResources.Get("LOCPLSProfilesStatusSaved",
                        "Profile file saved. Multi-source mode remains off.")
                    : PclsResources.Get("LOCPLSProfilesStatusSavedBackup",
                        "Profile file saved and previous profile data backed up. Multi-source mode remains off.");
            }
            catch (Exception ex)
            {
                SourceProfilesStatus = PclsResources.Format(
                    "LOCPLSProfilesStatusSaveFailed",
                    "Save refused; no activation: {0}", ex.Message);
            }
        }

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
