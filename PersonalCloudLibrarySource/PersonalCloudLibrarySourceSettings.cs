using Microsoft.Win32;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Forms = System.Windows.Forms;

namespace PersonalCloudLibrarySource
{
    public class PersonalCloudLibrarySourceSettings : ObservableObject
    {
        public const string LocalFileProviderType = "LocalFile";
        public const string LocalFolderProviderType = "LocalFolder";
        public const string RcloneRemoteProviderType = "RcloneRemote";
        public const string LocalFileManifestSourceMode = LocalFileProviderType;
        public const string RcloneRemoteManifestSourceMode = RcloneRemoteProviderType;
        public const string RemoveCachedFileOnlyUninstallBehavior = "RemoveCachedFileOnly";
        public const string RemoveCachedInstallFolderUninstallBehavior = "RemoveCachedInstallFolder";
        public const string AskEachTimeUninstallBehavior = "AskEachTime";

        private bool enabled = true;
        private string libraryDisplayName = "Personal Cloud Library Source";
        private string sourceProviderType = LocalFileProviderType;
        private string localManifestPath = string.Empty;
        private string localLibraryRoot = string.Empty;
        private string manifestRelativePath = string.Empty;
        private string localCacheFolder = string.Empty;
        private bool treatMissingFilesAsUninstalled = true;
        private string rcloneExecutablePath = "rclone";
        private string rcloneRemoteName = string.Empty;
        private string rcloneManifestPath = string.Empty;
        private string rcloneContentRoot = string.Empty;
        private int rcloneTimeoutSeconds = 30;
        private bool allowDownloads = true;
        private bool enableDiagnostics = true;
        private string uninstallBehavior = RemoveCachedInstallFolderUninstallBehavior;
        private bool allowUninstallOutsideCacheFolder;
        private bool autoRefreshOnApplicationStart;
        private bool autoGenerateManifestOnApplicationStart;
        private string lastManifestGeneratedAt = string.Empty;
        private string lastGeneratedManifestPath = string.Empty;
        private string lastGeneratedReportPath = string.Empty;
        private int lastManifestItemCount;

        public bool Enabled
        {
            get => enabled;
            set => SetValue(ref enabled, value);
        }

        public string LibraryDisplayName
        {
            get => libraryDisplayName;
            set => SetValue(ref libraryDisplayName, value);
        }

        public string SourceProviderType
        {
            get => sourceProviderType;
            set => SetValue(ref sourceProviderType, value);
        }

        public string ManifestSourceMode
        {
            get => SourceProviderType;
            set => SourceProviderType = value;
        }

        public string LocalManifestPath
        {
            get => localManifestPath;
            set => SetValue(ref localManifestPath, value);
        }

        public string LocalLibraryRoot
        {
            get => localLibraryRoot;
            set => SetValue(ref localLibraryRoot, value);
        }

        public string ManifestRelativePath
        {
            get => manifestRelativePath;
            set => SetValue(ref manifestRelativePath, value);
        }

        public string LocalCacheFolder
        {
            get => localCacheFolder;
            set => SetValue(ref localCacheFolder, value);
        }

        public bool TreatMissingFilesAsUninstalled
        {
            get => treatMissingFilesAsUninstalled;
            set => SetValue(ref treatMissingFilesAsUninstalled, value);
        }

        public string RcloneExecutablePath
        {
            get => rcloneExecutablePath;
            set => SetValue(ref rcloneExecutablePath, value);
        }

        public string RcloneRemoteName
        {
            get => rcloneRemoteName;
            set => SetValue(ref rcloneRemoteName, value);
        }

        public string RcloneManifestPath
        {
            get => rcloneManifestPath;
            set => SetValue(ref rcloneManifestPath, value);
        }

        public string RcloneContentRoot
        {
            get => rcloneContentRoot;
            set => SetValue(ref rcloneContentRoot, value);
        }

        public int RcloneTimeoutSeconds
        {
            get => rcloneTimeoutSeconds;
            set => SetValue(ref rcloneTimeoutSeconds, value);
        }

        public bool AllowDownloads
        {
            get => allowDownloads;
            set => SetValue(ref allowDownloads, value);
        }

        public bool AllowRcloneDownloads
        {
            get => AllowDownloads;
            set => AllowDownloads = value;
        }

        public bool EnableDiagnostics
        {
            get => enableDiagnostics;
            set => SetValue(ref enableDiagnostics, value);
        }

        public string UninstallBehavior
        {
            get => uninstallBehavior;
            set => SetValue(ref uninstallBehavior, value);
        }

        public bool AllowUninstallOutsideCacheFolder
        {
            get => allowUninstallOutsideCacheFolder;
            set => SetValue(ref allowUninstallOutsideCacheFolder, value);
        }

        public bool AutoRefreshOnApplicationStart
        {
            get => autoRefreshOnApplicationStart;
            set => SetValue(ref autoRefreshOnApplicationStart, value);
        }

        public bool AutoGenerateManifestOnApplicationStart
        {
            get => autoGenerateManifestOnApplicationStart;
            set => SetValue(ref autoGenerateManifestOnApplicationStart, value);
        }

        public string LastManifestGeneratedAt
        {
            get => lastManifestGeneratedAt;
            set => SetValue(ref lastManifestGeneratedAt, value);
        }

        public string LastGeneratedManifestPath
        {
            get => lastGeneratedManifestPath;
            set => SetValue(ref lastGeneratedManifestPath, value);
        }

        public string LastGeneratedReportPath
        {
            get => lastGeneratedReportPath;
            set => SetValue(ref lastGeneratedReportPath, value);
        }

        public int LastManifestItemCount
        {
            get => lastManifestItemCount;
            set => SetValue(ref lastManifestItemCount, value);
        }
    }

    public class PersonalCloudLibrarySourceSettingsViewModel : ObservableObject, ISettings
    {
        private readonly PersonalCloudLibrarySource plugin;
        private readonly SafeFileWriteService safeFileWriteService = new SafeFileWriteService();
        private readonly SettingsEditSession editSession;
        private PersonalCloudLibrarySourceSettings settings;
        private PersonalCloudLibrarySourceSettings runtimeSettingsSnapshot;
        private string setupStatusHeadline;
        private string setupStatusDetails;

        public string[] ProviderTypeOptions { get; } =
        {
            PersonalCloudLibrarySourceSettings.LocalFileProviderType,
            PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
            PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType
        };

        public string[] UninstallBehaviorOptions { get; } =
        {
            PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior,
            PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior,
            PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior
        };

        public PersonalCloudLibrarySourceSettings Settings
        {
            get => settings;
            set
            {
                if (settings != null)
                {
                    settings.PropertyChanged -= Settings_PropertyChanged;
                }

                settings = value;
                if (settings != null)
                {
                    settings.PropertyChanged += Settings_PropertyChanged;
                }

                OnPropertyChanged();
                RefreshBasicSetupStatus();
            }
        }

        public string SetupStatusHeadline
        {
            get => setupStatusHeadline;
            set => SetValue(ref setupStatusHeadline, value);
        }

        public string SetupStatusDetails
        {
            get => setupStatusDetails;
            set => SetValue(ref setupStatusDetails, value);
        }

        public bool LastEditSavedSuccessfully { get; private set; }

        public event EventHandler SettingsCommitted;

        public PersonalCloudLibrarySourceSettingsViewModel(PersonalCloudLibrarySource plugin)
        {
            this.plugin = plugin;
            Settings = SettingsMigrationService.LoadLegacyOrDefault(
                () => plugin.LoadPluginSettings<PersonalCloudLibrarySourceSettings>());
            editSession = new SettingsEditSession(
                () =>
                {
                    List<string> errors;
                    return VerifySettings(out errors);
                },
                snapshot => plugin.SavePluginSettings(snapshot));
            UpdateRuntimeSettingsSnapshot();
        }

        public virtual void BeginEdit()
        {
            editSession.BeginEdit(Settings);
            LastEditSavedSuccessfully = false;
            RefreshBasicSetupStatus();
        }

        public virtual void CancelEdit()
        {
            editSession.CancelEdit(Settings);
            LastEditSavedSuccessfully = false;
        }

        public virtual void EndEdit()
        {
            LastEditSavedSuccessfully = editSession.EndEdit(Settings);
            if (LastEditSavedSuccessfully)
            {
                runtimeSettingsSnapshot = editSession.GetCommittedSnapshot();
                NotifySettingsCommitted();
            }

            RefreshBasicSetupStatus();
        }

        public PersonalCloudLibrarySourceSettings GetRuntimeSettingsSnapshot()
        {
            return SettingsMigrationService.CloneForEditing(runtimeSettingsSnapshot);
        }

        protected void UpdateRuntimeSettingsSnapshot()
        {
            runtimeSettingsSnapshot = SettingsMigrationService.CloneForEditing(Settings);
        }

        protected void NotifySettingsCommitted()
        {
            SettingsCommitted?.Invoke(this, EventArgs.Empty);
        }

        public virtual bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            var sourceProviderType = string.IsNullOrWhiteSpace(Settings.SourceProviderType)
                ? PersonalCloudLibrarySourceSettings.LocalFileProviderType
                : Settings.SourceProviderType;

            if (string.Equals(sourceProviderType, PersonalCloudLibrarySourceSettings.LocalFileProviderType, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(Settings.LocalManifestPath))
                {
                    errors.Add(PclsResources.Format(
                        "LOCPLSValidationLocalManifestRequired",
                        "Choose a local manifest JSON file for {0} mode.",
                        PersonalCloudLibrarySourceSettings.LocalFileProviderType));
                }
                else if (!File.Exists(Settings.LocalManifestPath))
                {
                    errors.Add(PclsResources.Get("LOCPLSValidationLocalManifestMissing", "The local manifest JSON file does not exist."));
                }
            }
            else if (string.Equals(sourceProviderType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(Settings.LocalLibraryRoot))
                {
                    errors.Add(PclsResources.Format(
                        "LOCPLSValidationLibraryRootRequired",
                        "Choose a local library root for {0} mode.",
                        PersonalCloudLibrarySourceSettings.LocalFolderProviderType));
                }
                else if (!Directory.Exists(Settings.LocalLibraryRoot))
                {
                    errors.Add(PclsResources.Get("LOCPLSValidationLibraryRootMissing", "The local library root does not exist."));
                }

                if (string.IsNullOrWhiteSpace(Settings.LocalManifestPath) && string.IsNullOrWhiteSpace(Settings.ManifestRelativePath))
                {
                    errors.Add(PclsResources.Format(
                        "LOCPLSValidationFolderManifestRequired",
                        "Choose or generate a manifest for {0} mode.",
                        PersonalCloudLibrarySourceSettings.LocalFolderProviderType));
                }
                else if (!string.IsNullOrWhiteSpace(Settings.LocalManifestPath) && !File.Exists(Settings.LocalManifestPath))
                {
                    errors.Add(PclsResources.Format(
                        "LOCPLSValidationFolderManifestMissing",
                        "The selected {0} manifest file does not exist.",
                        PersonalCloudLibrarySourceSettings.LocalFolderProviderType));
                }
            }
            else if (string.Equals(sourceProviderType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(Settings.RcloneExecutablePath))
                {
                    errors.Add(PclsResources.Format(
                        "LOCPLSValidationRcloneExecutableRequired",
                        "The rclone executable path is required for {0} mode.",
                        PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType));
                }

                if (string.IsNullOrWhiteSpace(Settings.RcloneRemoteName))
                {
                    errors.Add(PclsResources.Format(
                        "LOCPLSValidationRcloneRemoteRequired",
                        "The rclone remote name is required for {0} mode.",
                        PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType));
                }

                if (string.IsNullOrWhiteSpace(Settings.RcloneManifestPath))
                {
                    errors.Add(PclsResources.Format(
                        "LOCPLSValidationRcloneManifestRequired",
                        "The rclone manifest path is required for {0} mode.",
                        PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType));
                }

                if (Settings.RcloneTimeoutSeconds < 5 || Settings.RcloneTimeoutSeconds > 300)
                {
                    errors.Add(PclsResources.Get("LOCPLSValidationRcloneTimeout", "The rclone timeout must be between 5 and 300 seconds."));
                }
            }
            else
            {
                errors.Add(PclsResources.Format(
                    "LOCPLSValidationProviderType",
                    "Source provider type must be {0}, {1}, or {2}.",
                    PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                    PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                    PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType));
            }

            if (!string.IsNullOrWhiteSpace(Settings.LocalCacheFolder))
            {
                try
                {
                    Path.GetFullPath(Settings.LocalCacheFolder);
                }
                catch (Exception)
                {
                errors.Add(PclsResources.Get("LOCPLSValidationCachePath", "The local cache folder path is invalid."));
                }
            }

            var uninstallBehavior = string.IsNullOrWhiteSpace(Settings.UninstallBehavior)
                ? PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior
                : Settings.UninstallBehavior;
            if (Array.IndexOf(UninstallBehaviorOptions, uninstallBehavior) < 0)
            {
                errors.Add(PclsResources.Get("LOCPLSValidationUninstallBehavior", "Choose a valid uninstall behavior."));
            }

            return errors.Count == 0;
        }

        public void VerifySetup()
        {
            try
            {
                List<string> errors;
                VerifySettings(out errors);
                var report = plugin.GenerateVerificationReport(Settings, errors);
                RefreshSetupStatusFromVerificationReport(report);

                var verificationPassed = report.ConfigurationErrorsCount == 0 && report.ManifestLoadSucceeded;
                var headline = verificationPassed
                    ? PclsResources.Get("LOCPLSVerificationCompleted", "Setup verification completed.")
                    : PclsResources.Get("LOCPLSVerificationIssues", "Setup verification found issues.");
                var nextAction = verificationPassed
                    ? PclsResources.Get("LOCPLSVerificationNextSuccess", "Next: save settings if needed, then run Update Game Library in Playnite.")
                    : PclsResources.Get("LOCPLSVerificationNextIssues", "Next: review the verification report, fix the flagged issues, and run verification again.");

                MessageBox.Show(
                    PclsResources.Format(
                        "LOCPLSVerificationSummary",
                        "{1}{0}{0}Manifest load: {2}{0}Items found: {3}{0}Download/cache-eligible: {4}{0}Cached or installed: {5}{0}Warnings sampled: {6}{0}Configuration errors: {7}{0}{0}Verification report:{0}{8}{0}{0}{9}",
                        Environment.NewLine,
                        headline,
                        report.ManifestLoadSucceeded
                            ? PclsResources.Get("LOCPLSSucceeded", "Succeeded")
                            : PclsResources.Get("LOCPLSFailed", "Failed"),
                        report.TotalManifestItems,
                        report.DownloadEligibleCount,
                        report.CachedInstalledCount,
                        report.WarningSamples.Count,
                        report.ConfigurationErrorsCount,
                        report.ReportPath,
                        nextAction),
                    PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
            }
            catch (Exception ex)
            {
                RefreshSetupStatusFromException(ex);
                MessageBox.Show(
                    PclsResources.Format("LOCPLSVerificationFailedReason", "Setup verification failed: {0}", ex.Message),
                    PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
            }
        }

        public void TestRcloneConnection()
        {
            try
            {
                var executablePath = string.IsNullOrWhiteSpace(Settings.RcloneExecutablePath)
                    ? "rclone"
                    : Settings.RcloneExecutablePath.Trim();

                using (var process = new Process())
                {
                    var output = new StringBuilder();
                    var error = new StringBuilder();

                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = "listremotes",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            output.AppendLine(args.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                        {
                            error.AppendLine(args.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var timeoutSeconds = Settings.RcloneTimeoutSeconds < 5 ? 30 : Settings.RcloneTimeoutSeconds;
                    if (!process.WaitForExit(timeoutSeconds * 1000))
                    {
                        process.Kill();
                        MessageBox.Show(
                            PclsResources.Get("LOCPLSRcloneListRemotesTimeout", "rclone listremotes timed out."),
                            PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
                        return;
                    }

                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        MessageBox.Show(
                            PclsResources.Format(
                                "LOCPLSRcloneSuccess",
                                "rclone responded successfully.{0}{0}Configured remotes:{0}{1}",
                                Environment.NewLine,
                                output),
                            PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
                    }
                    else
                    {
                        MessageBox.Show(
                            PclsResources.Format(
                                "LOCPLSRcloneListRemotesFailed",
                                "rclone listremotes failed:{0}{1}",
                                Environment.NewLine,
                                RcloneManifestReader.TrimForLog(error.ToString())),
                            PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    PclsResources.Format("LOCPLSRcloneRunFailed", "Unable to run rclone: {0}", ex.Message),
                    PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
            }
        }

        public void TestManifestLoad()
        {
            VerifySetup();
        }

        public void BrowseLocalManifestPath()
        {
            var dialog = new OpenFileDialog
            {
                Filter = PclsResources.Format(
                    "LOCPLSJsonFileFilter",
                    "JSON files ({0})|{0}|All files ({1})|{1}",
                    "*.json",
                    "*.*"),
                CheckFileExists = true
            };

            if (!string.IsNullOrWhiteSpace(Settings.LocalManifestPath))
            {
                dialog.FileName = Settings.LocalManifestPath;
            }

            if (dialog.ShowDialog() == true)
            {
                Settings.LocalManifestPath = dialog.FileName;
            }
        }

        public void BrowseLocalLibraryRoot()
        {
            var selectedPath = BrowseForFolder(
                PclsResources.Get("LOCPLSChooseLocalLibraryRoot", "Choose your local library root folder."),
                Settings.LocalLibraryRoot);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                Settings.LocalLibraryRoot = selectedPath;
            }
        }

        public void BrowseLocalCacheFolder()
        {
            var selectedPath = BrowseForFolder(
                PclsResources.Get("LOCPLSChooseLocalCacheFolder", "Choose your local cache folder."),
                Settings.LocalCacheFolder);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                Settings.LocalCacheFolder = selectedPath;
            }
        }

        public void BrowseRcloneExecutablePath()
        {
            var dialog = new OpenFileDialog
            {
                Filter = PclsResources.Format(
                    "LOCPLSRcloneExecutableFilter",
                    "rclone executable ({0})|{0}|Executable files ({1})|{1}|All files ({2})|{2}",
                    "rclone.exe",
                    "*.exe",
                    "*.*"),
                CheckFileExists = true
            };

            if (!string.IsNullOrWhiteSpace(Settings.RcloneExecutablePath) &&
                !string.Equals(Settings.RcloneExecutablePath, "rclone", StringComparison.OrdinalIgnoreCase))
            {
                dialog.FileName = Settings.RcloneExecutablePath;
            }

            if (dialog.ShowDialog() == true)
            {
                Settings.RcloneExecutablePath = dialog.FileName;
            }
        }

        public void OpenCacheFolder()
        {
            OpenFolder(Settings.LocalCacheFolder, createIfMissing: true);
        }

        public void OpenDiagnosticsFolder()
        {
            OpenFolder(plugin.GetDiagnosticsDirectory(), createIfMissing: true);
        }

        public void OpenReportsFolder()
        {
            OpenFolder(plugin.GetReportsDirectory(), createIfMissing: true);
        }

        public void OpenPluginDataFolder()
        {
            OpenFolder(plugin.GetPluginDataDirectory(), createIfMissing: true);
        }

        public void OpenLatestVerificationReport()
        {
            OpenFileInExplorer(
                plugin.GetLatestVerificationReportPath(),
                PclsResources.Get("LOCPLSVerificationReportPathEmpty", "Verification report path is empty."));
        }

        public void CreateSampleManifest()
        {
            try
            {
                var sampleDirectory = Path.Combine(plugin.GetPluginDataDirectory(), "samples");
                Directory.CreateDirectory(sampleDirectory);
                var samplePath = Path.Combine(sampleDirectory, "personal-cloud-library.sample.json");
                safeFileWriteService.WriteAllText(samplePath, GetSampleManifestJson());

                Settings.LocalManifestPath = samplePath;
                RefreshBasicSetupStatus();

                MessageBox.Show(
                    PclsResources.Format("LOCPLSSampleManifestCreated", "Sample manifest created at:{0}{1}", Environment.NewLine, samplePath),
                    PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    PclsResources.Format("LOCPLSSampleManifestFailed", "Could not create sample manifest: {0}", ex.Message),
                    PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
            }
        }

        public void GenerateManifestFromFolder()
        {
            var selectedRoot = Settings.LocalLibraryRoot;
            if (string.IsNullOrWhiteSpace(selectedRoot) || !Directory.Exists(selectedRoot))
            {
                selectedRoot = BrowseForFolder(
                    PclsResources.Get("LOCPLSChooseScanRoot", "Choose the local folder or NAS root to scan."),
                    Settings.LocalLibraryRoot);
            }

            if (string.IsNullOrWhiteSpace(selectedRoot))
            {
                return;
            }

            try
            {
                var report = plugin.GenerateManifestFromFolder(selectedRoot);
                Settings.SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFolderProviderType;
                Settings.LocalLibraryRoot = selectedRoot;
                Settings.LocalManifestPath = report.OutputPath;
                Settings.ManifestRelativePath = string.Empty;
                if (string.IsNullOrWhiteSpace(Settings.LocalCacheFolder))
                {
                    Settings.LocalCacheFolder = plugin.GetDefaultLocalCacheFolder();
                }

                Settings.LastGeneratedManifestPath = report.OutputPath;
                Settings.LastGeneratedReportPath = report.ReportPath;
                Settings.LastManifestGeneratedAt = report.Manifest.GeneratedAt;
                Settings.LastManifestItemCount = report.ItemCount;

                RefreshSetupStatusFromGeneration(report);

                MessageBox.Show(
                    PclsResources.Format(
                        "LOCPLSManifestGenerationCompleted",
                        "Manifest generation completed.{0}{0}Success: yes{0}Detected items: {1}{0}Detected directory packages: {2}{0}Skipped entries: {3}{0}Warnings: {4}{0}{0}Manifest path:{0}{5}{0}{0}Manifest report path:{0}{6}{0}{0}Run Update Game Library in Playnite when you are ready to import the generated manifest.",
                        Environment.NewLine,
                        report.ItemCount,
                        report.DetectedDirectoryItemCount,
                        report.SkippedEntries.Count,
                        report.Warnings.Count,
                        report.OutputPath,
                        report.ReportPath),
                    PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
            }
            catch (Exception ex)
            {
                RefreshSetupStatusFromException(ex);
                MessageBox.Show(
                    PclsResources.Format("LOCPLSManifestGenerationFailed", "Manifest generation failed: {0}", ex.Message),
                    PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
            }
        }

        public void OpenGeneratedManifest()
        {
            var path = string.IsNullOrWhiteSpace(Settings.LastGeneratedManifestPath)
                ? plugin.GetDefaultGeneratedManifestPath()
                : Settings.LastGeneratedManifestPath;
            OpenFileInExplorer(
                path,
                PclsResources.Get("LOCPLSGeneratedManifestPathEmpty", "Generated manifest path is empty."));
        }

        public void OpenGeneratedReport()
        {
            var path = string.IsNullOrWhiteSpace(Settings.LastGeneratedReportPath)
                ? plugin.GetDefaultGeneratedReportPath()
                : Settings.LastGeneratedReportPath;
            OpenFileInExplorer(
                path,
                PclsResources.Get("LOCPLSGeneratedReportPathEmpty", "Generated report path is empty."));
        }

        public void ShowUpdateLibraryInstructions()
        {
            MessageBox.Show(
                PclsResources.Format(
                    "LOCPLSUpdateLibraryInstructions",
                    "After saving or generating your manifest:{0}{0}1. Close Extension settings.{0}2. Run Update Game Library in Playnite.{0}3. Review imported entries, then use Download to local cache for any cloud-only items you want to cache.",
                    Environment.NewLine),
                PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RefreshBasicSetupStatus();
        }

        private void RefreshBasicSetupStatus()
        {
            var provider = PersonalCloudLibrarySource.GetProviderType(Settings);
            var manifestDescription = plugin.DescribeManifestPath(Settings);
            var details = new StringBuilder();
            var notConfigured = PclsResources.Get("LOCPLSNotConfiguredYet", "Not configured yet");
            details.AppendLine(PclsResources.Format("LOCPLSStatusProvider", "Provider: {0}", provider));
            details.AppendLine(PclsResources.Format(
                "LOCPLSStatusManifest",
                "Manifest: {0}",
                string.IsNullOrWhiteSpace(manifestDescription) ? notConfigured : manifestDescription));
            details.AppendLine(PclsResources.Format(
                "LOCPLSStatusCacheFolder",
                "Cache folder: {0}",
                string.IsNullOrWhiteSpace(Settings.LocalCacheFolder) ? notConfigured : Settings.LocalCacheFolder));

            if (!string.IsNullOrWhiteSpace(Settings.LastManifestGeneratedAt))
            {
                details.AppendLine(PclsResources.Format(
                    "LOCPLSStatusLastGenerated",
                    "Last generated: {0}",
                    Settings.LastManifestGeneratedAt));
                details.AppendLine(PclsResources.Format(
                    "LOCPLSStatusLastGeneratedItems",
                    "Last generated items: {0}",
                    Settings.LastManifestItemCount));
            }

            SetupStatusHeadline = Settings.Enabled
                ? PclsResources.Get("LOCPLSSettingsSetupStatus", "Setup status")
                : PclsResources.Get("LOCPLSLibrarySourceDisabled", "Library source disabled");
            SetupStatusDetails = details.ToString().TrimEnd();
        }

        private void RefreshSetupStatusFromErrors(List<string> errors)
        {
            SetupStatusHeadline = PclsResources.Get("LOCPLSSetupNeedsAttention", "Setup needs attention");
            SetupStatusDetails = string.Join(Environment.NewLine, errors);
        }

        private void RefreshSetupStatusFromValidation(ManifestValidationSummary summary)
        {
            SetupStatusHeadline = PclsResources.Get("LOCPLSSetupLooksValid", "Setup looks valid");
            SetupStatusDetails = PclsResources.Format(
                "LOCPLSValidationStatusDetails",
                "Items found: {1}{0}Download-eligible: {2}{0}Cached or installed: {3}{0}Warnings: {4}{0}Manifest: {5}",
                Environment.NewLine,
                summary.ItemsFound,
                summary.DownloadEligible,
                summary.CachedInstalled,
                summary.Warnings,
                plugin.DescribeManifestPath(Settings));
        }

        private void RefreshSetupStatusFromVerificationReport(LibraryVerificationReport report)
        {
            var hasIssues =
                report == null ||
                report.ConfigurationErrorsCount > 0 ||
                !report.ManifestLoadSucceeded ||
                report.InvalidOrMissingSourcePathCount > 0 ||
                report.InvalidOrMissingCachePathCount > 0 ||
                report.RclonePathDoublingWarningCount > 0 ||
                report.LocalFolderPathWarningCount > 0;

            SetupStatusHeadline = hasIssues
                ? PclsResources.Get("LOCPLSSetupNeedsAttention", "Setup needs attention")
                : PclsResources.Get("LOCPLSSetupLooksValid", "Setup looks valid");
            SetupStatusDetails = PclsResources.Format(
                "LOCPLSVerificationStatusDetails",
                "Manifest load: {1}{0}Items found: {2}{0}Download/cache-eligible: {3}{0}Cached or installed: {4}{0}Configuration errors: {5}{0}Warnings sampled: {6}{0}Verification report: {7}",
                Environment.NewLine,
                report != null && report.ManifestLoadSucceeded
                    ? PclsResources.Get("LOCPLSSucceeded", "Succeeded")
                    : PclsResources.Get("LOCPLSFailed", "Failed"),
                report?.TotalManifestItems ?? 0,
                report?.DownloadEligibleCount ?? 0,
                report?.CachedInstalledCount ?? 0,
                report?.ConfigurationErrorsCount ?? 0,
                report?.WarningSamples.Count ?? 0,
                report?.ReportPath ?? plugin.GetLatestVerificationReportPath());
        }

        private void RefreshSetupStatusFromGeneration(ManifestGenerationReport report)
        {
            SetupStatusHeadline = PclsResources.Get("LOCPLSManifestGeneratedSuccessfully", "Manifest generated successfully");
            SetupStatusDetails = PclsResources.Format(
                "LOCPLSGenerationStatusDetails",
                "Items detected: {1}{0}Skipped entries: {2}{0}Warnings: {3}{0}Manifest: {4}",
                Environment.NewLine,
                report.ItemCount,
                report.SkippedEntries.Count,
                report.Warnings.Count,
                report.OutputPath);
        }

        private void RefreshSetupStatusFromException(Exception ex)
        {
            SetupStatusHeadline = PclsResources.Get("LOCPLSSetupCheckFailed", "Setup check failed");
            SetupStatusDetails = ex.Message;
        }

        private static string BrowseForFolder(string description, string selectedPath)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = description;
                if (!string.IsNullOrWhiteSpace(selectedPath) && Directory.Exists(selectedPath))
                {
                    dialog.SelectedPath = selectedPath;
                }

                return dialog.ShowDialog() == Forms.DialogResult.OK
                    ? dialog.SelectedPath
                    : string.Empty;
            }
        }

        private static void OpenFolder(string folderPath, bool createIfMissing)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                MessageBox.Show(
                    PclsResources.Get("LOCPLSFolderPathEmpty", "Folder path is empty."),
                    PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
                return;
            }

            if (createIfMissing)
            {
                Directory.CreateDirectory(folderPath);
            }

            Process.Start("explorer.exe", folderPath);
        }

        private static void OpenFileInExplorer(string path, string emptyPathMessage)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(emptyPathMessage, PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
                return;
            }

            if (!File.Exists(path))
            {
                MessageBox.Show(
                    PclsResources.Format("LOCPLSFileNotFound", "File not found:{0}{1}", Environment.NewLine, path),
                    PclsResources.Get("LOCPLSSettingsTitle", "Personal Cloud Library Source"));
                return;
            }

            Process.Start("explorer.exe", "/select,\"" + path + "\"");
        }

        private static string GetSampleManifestJson()
        {
            return @"{
  ""version"": 3,
  ""generatedBy"": ""Personal Cloud Library Source sample manifest"",
  ""generatedAt"": ""2026-06-15T00:00:00Z"",
  ""sourceMode"": ""filesystem"",
  ""itemCount"": 2,
  ""items"": [
    {
      ""id"": ""example-cartridge-demo"",
      ""title"": ""Example Cartridge Demo"",
      ""platform"": ""Nintendo NES"",
      ""sourcePath"": ""Nintendo Entertainment System/Example Cartridge Demo.nes"",
      ""sourceType"": ""file"",
      ""cachePath"": ""Nintendo Entertainment System\\Example Cartridge Demo.nes"",
      ""installDirectory"": ""Nintendo Entertainment System"",
      ""launchFile"": ""Example Cartridge Demo.nes"",
      ""notes"": ""Fake sample entry for testing only.""
    },
    {
      ""id"": ""example-disc-package"",
      ""title"": ""Example Disc Package"",
      ""platform"": ""Sony PlayStation"",
      ""sourcePath"": ""PlayStation/Example Disc Package"",
      ""sourceType"": ""directory"",
      ""cachePath"": ""PlayStation\\Example Disc Package\\Example Disc Package.cue"",
      ""installDirectory"": ""PlayStation\\Example Disc Package"",
      ""launchFile"": ""Example Disc Package.cue"",
      ""notes"": ""Fake directory package sample entry for testing only.""
    }
  ]
}";
        }
    }
}
