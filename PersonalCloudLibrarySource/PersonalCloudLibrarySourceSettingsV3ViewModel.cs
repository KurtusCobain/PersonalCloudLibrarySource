using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public sealed class PersonalCloudLibrarySourceSettingsV3ViewModel : PersonalCloudLibrarySourceSettingsViewModel, ISettings
    {
        private readonly PersonalCloudLibrarySource plugin;
        private readonly SourceProfilesManagementService profileManager =
            new SourceProfilesManagementService();
        private bool sourceHealthCheckRunning;
        private string sourceHealthStatus = PclsResources.Get(
            "LOCPLSProfilesHealthInitial",
            "Select a source and check its manifest. This does not update Playnite games.");
        public string SourceHealthStatus
        {
            get => sourceHealthStatus;
            private set => SetValue(ref sourceHealthStatus, value);
        }
        public bool CanCheckSelectedSource => selectedSourceProfile != null && !sourceHealthCheckRunning;

        // Explicit and read-only. Never retry automatically or run on the UI thread.
        public async void CheckSelectedSourceHealth()
        {
            if (!CanCheckSelectedSource)
                return;
            PclsSourceProfileDefinition source;
            PersonalCloudLibrarySourceSettingsV3 shared;
            try
            {
                source = profileManager.Clone(new[] { SelectedSourceProfile }).Single();
                shared = GetRuntimeSettingsSnapshot();
            }
            catch (Exception)
            {
                SourceHealthStatus = PclsResources.Get("LOCPLSProfilesHealthInvalid",
                    "Selected source configuration is invalid.");
                return;
            }
            sourceHealthCheckRunning = true;
            OnPropertyChanged(nameof(CanCheckSelectedSource));
            SourceHealthStatus = PclsResources.Get("LOCPLSProfilesHealthChecking",
                "Checking the selected manifest without modifying games or cached files...");
            try
            {
                var result = await Task.Run(() => new SourceProfileHealthProbe().Probe(
                    source, shared, new RcloneManifestReader().ReadManifestJson));
                switch (result.Status)
                {
                    case SourceManifestHealth.ManifestValid:
                        SourceHealthStatus = PclsResources.Format("LOCPLSProfilesHealthValid",
                            "Manifest readable: {0} entries. Game payload availability is not verified.",
                            result.ManifestItemCount);
                        break;
                    case SourceManifestHealth.SourceUnavailable:
                        SourceHealthStatus = PclsResources.Get("LOCPLSProfilesHealthUnavailable",
                            "Manifest unavailable. No games were removed or imported.");
                        break;
                    case SourceManifestHealth.UnsupportedSchema:
                        SourceHealthStatus = PclsResources.Get("LOCPLSProfilesHealthUnsupported",
                            "Manifest uses an unsupported schema. No games were changed.");
                        break;
                    case SourceManifestHealth.InvalidManifest:
                        SourceHealthStatus = PclsResources.Get("LOCPLSProfilesHealthMalformed",
                            "Manifest is invalid. No games were changed.");
                        break;
                    case SourceManifestHealth.Disabled:
                        SourceHealthStatus = PclsResources.Get("LOCPLSProfilesHealthDisabled",
                            "Selected source is disabled; no manifest check was performed.");
                        break;
                    default:
                        SourceHealthStatus = PclsResources.Get("LOCPLSProfilesHealthInvalid",
                            "Selected source configuration is invalid.");
                        break;
                }
            }
            catch (Exception)
            {
                SourceHealthStatus = PclsResources.Get("LOCPLSProfilesHealthUnavailable",
                    "Manifest unavailable. No games were removed or imported.");
            }
            finally
            {
                sourceHealthCheckRunning = false;
                OnPropertyChanged(nameof(CanCheckSelectedSource));
            }
        }

        private List<PclsSourceProfileDefinition> sourceProfilesBaseline;
        private ObservableCollection<PclsSourceProfileDefinition> sourceProfileCandidates;
        private PclsSourceProfileDefinition selectedSourceProfile;
        private string sourceProfilesStatus = PclsResources.Get(
            "LOCPLSProfilesStatusInitial",
            "Preview only. Existing one-source settings stay active until an explicitly approved release.");
        private string selectedNewSourceType = PersonalCloudLibrarySourceSettings.LocalFileProviderType;
        private ObservableCollection<string> profileBackupNames;
        private string selectedProfileBackupName;
        private ObservableCollection<PclsSourceProfileDefinition> backupProfilePreview;
        private List<PclsSourceProfileDefinition> inspectedBackup;
        private string backupPreviewStatus = PclsResources.Get(
            "LOCPLSProfilesBackupInitial",
            "Load source profiles and select a backup to inspect before restoring.");

        public ObservableCollection<string> ProfileBackupNames
        {
            get => profileBackupNames;
            private set { profileBackupNames = value; OnPropertyChanged(); }
        }

        public string SelectedProfileBackupName
        {
            get => selectedProfileBackupName;
            set
            {
                if (string.Equals(selectedProfileBackupName, value, StringComparison.Ordinal))
                    return;
                selectedProfileBackupName = value;
                OnPropertyChanged();
                InvalidateBackupPreview();
            }
        }

        public ObservableCollection<PclsSourceProfileDefinition> BackupProfilePreview
        {
            get => backupProfilePreview;
            private set { backupProfilePreview = value; OnPropertyChanged(); }
        }

        public string BackupPreviewStatus
        {
            get => backupPreviewStatus;
            private set => SetValue(ref backupPreviewStatus, value);
        }

        public bool HasRestorableBackupPreview => inspectedBackup != null &&
            sourceProfilesBaseline != null &&
            !string.IsNullOrWhiteSpace(SelectedProfileBackupName);

        private void InvalidateBackupPreview()
        {
            inspectedBackup = null;
            BackupProfilePreview = null;
            OnPropertyChanged(nameof(HasRestorableBackupPreview));
        }

        private void RefreshBackupNames()
        {
            var backups = new SourceProfilesFileStore()
                .ListBackupNames(plugin.GetPluginDataDirectory());
            ProfileBackupNames = new ObservableCollection<string>(backups);
            SelectedProfileBackupName = ProfileBackupNames.FirstOrDefault();
            InvalidateBackupPreview();
        }

        public void PreviewSelectedSourceBackup()
        {
            InvalidateBackupPreview();
            if (sourceProfilesBaseline == null ||
                string.IsNullOrWhiteSpace(SelectedProfileBackupName))
            {
                BackupPreviewStatus = PclsResources.Get(
                    "LOCPLSProfilesBackupNeedSelection",
                    "Load profiles and select an existing backup first.");
                return;
            }
            try
            {
                var preview = profileManager.PreviewBackupForRestore(
                    plugin.GetPluginDataDirectory(), SelectedProfileBackupName,
                    sourceProfilesBaseline, GetRuntimeSettingsSnapshot());
                inspectedBackup = profileManager.Clone(preview);
                BackupProfilePreview = new ObservableCollection<PclsSourceProfileDefinition>(
                    profileManager.Clone(preview));
                BackupPreviewStatus = PclsResources.Format(
                    "LOCPLSProfilesBackupInspected",
                    "Backup inspected: {0} unchanged source identities. Review the names and providers, then explicitly restore if desired.",
                    preview.Count);
                OnPropertyChanged(nameof(HasRestorableBackupPreview));
            }
            catch (Exception ex)
            {
                BackupPreviewStatus = PclsResources.Format(
                    "LOCPLSProfilesBackupInspectFailed",
                    "Backup inspection refused: {0}", ex.Message);
            }
        }

        public void RestoreSelectedSourceBackup()
        {
            if (!HasRestorableBackupPreview)
                return;
            if (MessageBox.Show(
                PclsResources.Get("LOCPLSProfilesBackupRestoreConfirm",
                    "Restore the inspected source profile settings? All current profile identities must be preserved. The current file will become a separate rollback backup. This does not activate multi-source mode."),
                PclsResources.Get("LOCPLSProfilesDialogTitle", "PCLS source profiles preview"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            try
            {
                profileManager.RestoreBackupWithoutActivation(
                    plugin.GetPluginDataDirectory(), SelectedProfileBackupName,
                    sourceProfilesBaseline, inspectedBackup, GetRuntimeSettingsSnapshot());
                LoadSourceProfilesPreview();
                BackupPreviewStatus = PclsResources.Get("LOCPLSProfilesBackupRestored",
                    "Restored the selected profile backup. The previous current configuration was backed up. Multi-source mode remains off.");
            }
            catch (Exception ex)
            {
                InvalidateBackupPreview();
                BackupPreviewStatus = PclsResources.Format(
                    "LOCPLSProfilesBackupRestoreFailed", "Restore refused: {0}", ex.Message);
            }
        }


        public ObservableCollection<PclsSourceProfileDefinition> SourceProfileCandidates
        {
            get => sourceProfileCandidates;
            private set { sourceProfileCandidates = value; OnPropertyChanged(); }
        }

        public PclsSourceProfileDefinition SelectedSourceProfile
        {
            get => selectedSourceProfile;
            set { selectedSourceProfile = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanCheckSelectedSource)); }
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
                RefreshBackupNames();
                BackupPreviewStatus = PclsResources.Get(
                    "LOCPLSProfilesBackupInitial",
                    "Load source profiles and select a backup to inspect before restoring.");
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
                RefreshBackupNames();
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
