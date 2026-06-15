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
        private PersonalCloudLibrarySourceSettings editingClone;
        private PersonalCloudLibrarySourceSettings settings;
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

        public PersonalCloudLibrarySourceSettingsViewModel(PersonalCloudLibrarySource plugin)
        {
            this.plugin = plugin;
            var savedSettings = plugin.LoadPluginSettings<PersonalCloudLibrarySourceSettings>();
            Settings = savedSettings ?? new PersonalCloudLibrarySourceSettings();
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
            RefreshBasicSetupStatus();
        }

        public void CancelEdit()
        {
            Settings = editingClone ?? new PersonalCloudLibrarySourceSettings();
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
            RefreshBasicSetupStatus();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            var sourceProviderType = string.IsNullOrWhiteSpace(Settings.SourceProviderType)
                ? PersonalCloudLibrarySourceSettings.LocalFileProviderType
                : Settings.SourceProviderType;

            if (string.Equals(sourceProviderType, PersonalCloudLibrarySourceSettings.LocalFileProviderType, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(Settings.LocalManifestPath))
                {
                    errors.Add("Choose a local manifest JSON file for LocalFile mode.");
                }
                else if (!File.Exists(Settings.LocalManifestPath))
                {
                    errors.Add("The local manifest JSON file does not exist.");
                }
            }
            else if (string.Equals(sourceProviderType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(Settings.LocalLibraryRoot))
                {
                    errors.Add("Choose a local library root for LocalFolder mode.");
                }
                else if (!Directory.Exists(Settings.LocalLibraryRoot))
                {
                    errors.Add("The local library root does not exist.");
                }

                if (string.IsNullOrWhiteSpace(Settings.LocalManifestPath) && string.IsNullOrWhiteSpace(Settings.ManifestRelativePath))
                {
                    errors.Add("Choose or generate a manifest for LocalFolder mode.");
                }
                else if (!string.IsNullOrWhiteSpace(Settings.LocalManifestPath) && !File.Exists(Settings.LocalManifestPath))
                {
                    errors.Add("The selected LocalFolder manifest file does not exist.");
                }
            }
            else if (string.Equals(sourceProviderType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(Settings.RcloneExecutablePath))
                {
                    errors.Add("The rclone executable path is required for RcloneRemote mode.");
                }

                if (string.IsNullOrWhiteSpace(Settings.RcloneRemoteName))
                {
                    errors.Add("The rclone remote name is required for RcloneRemote mode.");
                }

                if (string.IsNullOrWhiteSpace(Settings.RcloneManifestPath))
                {
                    errors.Add("The rclone manifest path is required for RcloneRemote mode.");
                }

                if (Settings.RcloneTimeoutSeconds < 5 || Settings.RcloneTimeoutSeconds > 300)
                {
                    errors.Add("The rclone timeout must be between 5 and 300 seconds.");
                }
            }
            else
            {
                errors.Add("Source provider type must be LocalFile, LocalFolder, or RcloneRemote.");
            }

            if (!string.IsNullOrWhiteSpace(Settings.LocalCacheFolder))
            {
                try
                {
                    Path.GetFullPath(Settings.LocalCacheFolder);
                }
                catch (Exception)
                {
                    errors.Add("The local cache folder path is invalid.");
                }
            }

            var uninstallBehavior = string.IsNullOrWhiteSpace(Settings.UninstallBehavior)
                ? PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior
                : Settings.UninstallBehavior;
            if (Array.IndexOf(UninstallBehaviorOptions, uninstallBehavior) < 0)
            {
                errors.Add("Choose a valid uninstall behavior.");
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
                    ? "Setup verification completed."
                    : "Setup verification found issues.";
                var nextAction = verificationPassed
                    ? "Next: save settings if needed, then run Update Game Library in Playnite."
                    : "Next: review the verification report, fix the flagged issues, and run verification again.";

                MessageBox.Show(
                    headline + Environment.NewLine +
                    Environment.NewLine +
                    "Manifest load: " + (report.ManifestLoadSucceeded ? "Succeeded" : "Failed") + Environment.NewLine +
                    "Items found: " + report.TotalManifestItems + Environment.NewLine +
                    "Download/cache-eligible: " + report.DownloadEligibleCount + Environment.NewLine +
                    "Cached or installed: " + report.CachedInstalledCount + Environment.NewLine +
                    "Warnings sampled: " + report.WarningSamples.Count + Environment.NewLine +
                    "Configuration errors: " + report.ConfigurationErrorsCount + Environment.NewLine +
                    Environment.NewLine +
                    "Verification report:" + Environment.NewLine +
                    report.ReportPath + Environment.NewLine + Environment.NewLine +
                    nextAction,
                    "Personal Cloud Library Source");
            }
            catch (Exception ex)
            {
                RefreshSetupStatusFromException(ex);
                MessageBox.Show("Setup verification failed: " + ex.Message, "Personal Cloud Library Source");
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
                        MessageBox.Show("rclone listremotes timed out.", "Personal Cloud Library Source");
                        return;
                    }

                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        MessageBox.Show(
                            "rclone responded successfully." + Environment.NewLine + Environment.NewLine +
                            "Configured remotes:" + Environment.NewLine + output,
                            "Personal Cloud Library Source");
                    }
                    else
                    {
                        MessageBox.Show(
                            "rclone listremotes failed:" + Environment.NewLine + RcloneManifestReader.TrimForLog(error.ToString()),
                            "Personal Cloud Library Source");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to run rclone: " + ex.Message, "Personal Cloud Library Source");
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
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
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
            var selectedPath = BrowseForFolder("Choose your local library root folder.", Settings.LocalLibraryRoot);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                Settings.LocalLibraryRoot = selectedPath;
            }
        }

        public void BrowseLocalCacheFolder()
        {
            var selectedPath = BrowseForFolder("Choose your local cache folder.", Settings.LocalCacheFolder);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                Settings.LocalCacheFolder = selectedPath;
            }
        }

        public void BrowseRcloneExecutablePath()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "rclone executable (rclone.exe)|rclone.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
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
            OpenFileInExplorer(plugin.GetLatestVerificationReportPath(), "Verification report path is empty.");
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

                MessageBox.Show("Sample manifest created at:" + Environment.NewLine + samplePath, "Personal Cloud Library Source");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not create sample manifest: " + ex.Message, "Personal Cloud Library Source");
            }
        }

        public void GenerateManifestFromFolder()
        {
            var selectedRoot = Settings.LocalLibraryRoot;
            if (string.IsNullOrWhiteSpace(selectedRoot) || !Directory.Exists(selectedRoot))
            {
                selectedRoot = BrowseForFolder("Choose the local folder or NAS root to scan.", Settings.LocalLibraryRoot);
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
                    "Manifest generation completed." + Environment.NewLine + Environment.NewLine +
                    "Success: yes" + Environment.NewLine +
                    "Detected items: " + report.ItemCount + Environment.NewLine +
                    "Detected directory packages: " + report.DetectedDirectoryItemCount + Environment.NewLine +
                    "Skipped entries: " + report.SkippedEntries.Count + Environment.NewLine +
                    "Warnings: " + report.Warnings.Count + Environment.NewLine + Environment.NewLine +
                    "Manifest path:" + Environment.NewLine + report.OutputPath + Environment.NewLine + Environment.NewLine +
                    "Manifest report path:" + Environment.NewLine + report.ReportPath + Environment.NewLine + Environment.NewLine +
                    "Run Update Game Library in Playnite when you are ready to import the generated manifest.",
                    "Personal Cloud Library Source");
            }
            catch (Exception ex)
            {
                RefreshSetupStatusFromException(ex);
                MessageBox.Show("Manifest generation failed: " + ex.Message, "Personal Cloud Library Source");
            }
        }

        public void OpenGeneratedManifest()
        {
            var path = string.IsNullOrWhiteSpace(Settings.LastGeneratedManifestPath)
                ? plugin.GetDefaultGeneratedManifestPath()
                : Settings.LastGeneratedManifestPath;
            OpenFileInExplorer(path, "Generated manifest path is empty.");
        }

        public void OpenGeneratedReport()
        {
            var path = string.IsNullOrWhiteSpace(Settings.LastGeneratedReportPath)
                ? plugin.GetDefaultGeneratedReportPath()
                : Settings.LastGeneratedReportPath;
            OpenFileInExplorer(path, "Generated report path is empty.");
        }

        public void ShowUpdateLibraryInstructions()
        {
            MessageBox.Show(
                "After saving or generating your manifest:" + Environment.NewLine + Environment.NewLine +
                "1. Close Extension settings." + Environment.NewLine +
                "2. Run Update Game Library in Playnite." + Environment.NewLine +
                "3. Review imported entries, then use Download to local cache for any cloud-only items you want to cache.",
                "Personal Cloud Library Source");
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
            details.AppendLine("Provider: " + provider);
            details.AppendLine("Manifest: " + (string.IsNullOrWhiteSpace(manifestDescription) ? "Not configured yet" : manifestDescription));
            details.AppendLine("Cache folder: " + (string.IsNullOrWhiteSpace(Settings.LocalCacheFolder) ? "Not configured yet" : Settings.LocalCacheFolder));

            if (!string.IsNullOrWhiteSpace(Settings.LastManifestGeneratedAt))
            {
                details.AppendLine("Last generated: " + Settings.LastManifestGeneratedAt);
                details.AppendLine("Last generated items: " + Settings.LastManifestItemCount);
            }

            SetupStatusHeadline = Settings.Enabled ? "Setup status" : "Library source disabled";
            SetupStatusDetails = details.ToString().TrimEnd();
        }

        private void RefreshSetupStatusFromErrors(List<string> errors)
        {
            SetupStatusHeadline = "Setup needs attention";
            SetupStatusDetails = string.Join(Environment.NewLine, errors);
        }

        private void RefreshSetupStatusFromValidation(ManifestValidationSummary summary)
        {
            SetupStatusHeadline = "Setup looks valid";
            SetupStatusDetails =
                "Items found: " + summary.ItemsFound + Environment.NewLine +
                "Download-eligible: " + summary.DownloadEligible + Environment.NewLine +
                "Cached or installed: " + summary.CachedInstalled + Environment.NewLine +
                "Warnings: " + summary.Warnings + Environment.NewLine +
                "Manifest: " + plugin.DescribeManifestPath(Settings);
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

            SetupStatusHeadline = hasIssues ? "Setup needs attention" : "Setup looks valid";
            SetupStatusDetails =
                "Manifest load: " + (report != null && report.ManifestLoadSucceeded ? "Succeeded" : "Failed") + Environment.NewLine +
                "Items found: " + (report?.TotalManifestItems ?? 0) + Environment.NewLine +
                "Download/cache-eligible: " + (report?.DownloadEligibleCount ?? 0) + Environment.NewLine +
                "Cached or installed: " + (report?.CachedInstalledCount ?? 0) + Environment.NewLine +
                "Configuration errors: " + (report?.ConfigurationErrorsCount ?? 0) + Environment.NewLine +
                "Warnings sampled: " + (report?.WarningSamples.Count ?? 0) + Environment.NewLine +
                "Verification report: " + (report?.ReportPath ?? plugin.GetLatestVerificationReportPath());
        }

        private void RefreshSetupStatusFromGeneration(ManifestGenerationReport report)
        {
            SetupStatusHeadline = "Manifest generated successfully";
            SetupStatusDetails =
                "Items detected: " + report.ItemCount + Environment.NewLine +
                "Skipped entries: " + report.SkippedEntries.Count + Environment.NewLine +
                "Warnings: " + report.Warnings.Count + Environment.NewLine +
                "Manifest: " + report.OutputPath;
        }

        private void RefreshSetupStatusFromException(Exception ex)
        {
            SetupStatusHeadline = "Setup check failed";
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
                MessageBox.Show("Folder path is empty.", "Personal Cloud Library Source");
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
                MessageBox.Show(emptyPathMessage, "Personal Cloud Library Source");
                return;
            }

            if (!File.Exists(path))
            {
                MessageBox.Show("File not found:" + Environment.NewLine + path, "Personal Cloud Library Source");
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
