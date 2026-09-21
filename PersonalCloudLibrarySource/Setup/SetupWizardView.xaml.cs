using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;

namespace PersonalCloudLibrarySource
{
    public partial class SetupWizardView : UserControl
    {
        private readonly SetupWizardViewModel viewModel;
        private readonly Func<bool> completed;
        private readonly Action cancelled;

        public SetupWizardView(
            SetupWizardViewModel viewModel,
            Func<bool> completed,
            Action cancelled)
        {
            InitializeComponent();
            this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            this.completed = completed ?? throw new ArgumentNullException(nameof(completed));
            this.cancelled = cancelled ?? throw new ArgumentNullException(nameof(cancelled));
            DataContext = viewModel;
            RefreshView();
        }

        private void ExistingManifestRadio_Click(object sender, RoutedEventArgs e)
        {
            viewModel.SelectSource(SetupSourceKind.ExistingManifest);
            RefreshView();
        }

        private void LocalFolderRadio_Click(object sender, RoutedEventArgs e)
        {
            viewModel.SelectSource(SetupSourceKind.LocalFolder);
            RefreshView();
        }

        private void NetworkFolderRadio_Click(object sender, RoutedEventArgs e)
        {
            viewModel.SelectSource(SetupSourceKind.NetworkFolder);
            RefreshView();
        }

        private void RcloneRemoteRadio_Click(object sender, RoutedEventArgs e)
        {
            viewModel.SelectSource(SetupSourceKind.RcloneRemote);
            RefreshView();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Next();
            RefreshView();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Back();
            RefreshView();
        }

        private void Finish_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.Complete())
            {
                if (completed())
                {
                    return;
                }

                viewModel.ReactivateReviewAfterSaveFailure(PclsResources.Get(
                    "LOCPLSSetupNotSaved",
                    "Setup was not saved. Review the values and try again."));
            }

            RefreshView();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Cancel();
            cancelled();
        }

        private void BrowseManifest_Click(object sender, RoutedEventArgs e)
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

            if (!string.IsNullOrWhiteSpace(viewModel.Draft.LocalManifestPath))
            {
                dialog.FileName = viewModel.Draft.LocalManifestPath;
            }

            if (dialog.ShowDialog() == true)
            {
                viewModel.Draft.LocalManifestPath = dialog.FileName;
            }
        }

        private void BrowseLibraryRoot_Click(object sender, RoutedEventArgs e)
        {
            var selected = BrowseForFolder(
                PclsResources.Get("LOCPLSSetupChooseLibraryFolder", "Choose the local, external, or network library folder."),
                viewModel.Draft.LocalLibraryRoot);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                viewModel.Draft.LocalLibraryRoot = selected;
            }
        }

        private void BrowseCache_Click(object sender, RoutedEventArgs e)
        {
            var selected = BrowseForFolder(
                PclsResources.Get("LOCPLSSetupChooseCacheFolder", "Choose the local cache folder."),
                viewModel.Draft.CachePath);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                viewModel.Draft.CachePath = selected;
            }
        }

        private void BrowseRclone_Click(object sender, RoutedEventArgs e)
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

            if (!string.IsNullOrWhiteSpace(viewModel.Draft.RcloneExecutablePath) &&
                !string.Equals(viewModel.Draft.RcloneExecutablePath, "rclone", StringComparison.OrdinalIgnoreCase))
            {
                dialog.FileName = viewModel.Draft.RcloneExecutablePath;
            }

            if (dialog.ShowDialog() == true)
            {
                viewModel.Draft.RcloneExecutablePath = dialog.FileName;
            }
        }

        private void RefreshView()
        {
            ChooseSourcePanel.Visibility = GetVisibility(SetupWizardStep.ChooseSource);
            ConfigureSourcePanel.Visibility = GetVisibility(SetupWizardStep.ConfigureSource);
            ScanPreviewPanel.Visibility = GetVisibility(SetupWizardStep.ScanPreview);
            CacheBehaviorPanel.Visibility = GetVisibility(SetupWizardStep.CacheBehavior);
            ReviewPanel.Visibility = GetVisibility(SetupWizardStep.Review);

            RefreshSourceSelection();
            RefreshConfigurePanel();
            RefreshSummary();

            var stepNumber = Math.Min(5, (int)viewModel.CurrentStep + 1);
            StepIndicatorText.Text = PclsResources.Format(
                "LOCPLSSetupStepIndicator",
                "Step {0} of 5 — {1}",
                stepNumber,
                GetStepTitle(viewModel.CurrentStep));
            ValidationBorder.Visibility = viewModel.ValidationErrors.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            BackButton.IsEnabled = viewModel.CanGoBack;
            BackButton.Visibility = viewModel.CurrentStep == SetupWizardStep.ChooseSource
                ? Visibility.Hidden
                : Visibility.Visible;
            NextButton.Visibility = viewModel.CanGoNext ? Visibility.Visible : Visibility.Collapsed;
            FinishButton.Visibility = viewModel.CanComplete ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility GetVisibility(SetupWizardStep step)
        {
            return viewModel.CurrentStep == step ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshSourceSelection()
        {
            var selected = viewModel.Draft.SelectedSource;
            ExistingManifestRadio.IsChecked = selected == SetupSourceKind.ExistingManifest;
            LocalFolderRadio.IsChecked = selected == SetupSourceKind.LocalFolder;
            NetworkFolderRadio.IsChecked = selected == SetupSourceKind.NetworkFolder;
            RcloneRemoteRadio.IsChecked = selected == SetupSourceKind.RcloneRemote;
        }

        private void RefreshConfigurePanel()
        {
            var selected = viewModel.Draft.SelectedSource;
            ExistingManifestPanel.Visibility = selected == SetupSourceKind.ExistingManifest
                ? Visibility.Visible
                : Visibility.Collapsed;
            FolderSourcePanel.Visibility = selected == SetupSourceKind.LocalFolder || selected == SetupSourceKind.NetworkFolder
                ? Visibility.Visible
                : Visibility.Collapsed;
            RcloneSourcePanel.Visibility = selected == SetupSourceKind.RcloneRemote
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void RefreshSummary()
        {
            var sourceType = GetSourceKindDisplayName(viewModel.Draft.SelectedSource);
            var sourcePath = GetSourceDescription();
            ScanSourceTypeText.Text = sourceType;
            ScanSourcePathText.Text = sourcePath;
            ReviewSourceTypeText.Text = sourceType;
            ReviewSourcePathText.Text = sourcePath;
            ReviewInstallBehaviorText.Text = viewModel.Draft.AllowDownloads
                ? PclsResources.Get("LOCPLSSetupInstallDownloads", "Download or copy when Install is selected")
                : PclsResources.Get("LOCPLSSetupInstallCatalogOnly", "Catalog only");
        }

        private string GetSourceDescription()
        {
            switch (viewModel.Draft.SelectedSource)
            {
                case SetupSourceKind.LocalFolder:
                case SetupSourceKind.NetworkFolder:
                    return EmptyFallback(viewModel.Draft.LocalLibraryRoot);
                case SetupSourceKind.RcloneRemote:
                    var remote = viewModel.Draft.RcloneRemoteName;
                    var root = viewModel.Draft.RcloneContentRoot;
                    if (string.IsNullOrWhiteSpace(remote))
                    {
                return PclsResources.Get("LOCPLSNotConfigured", "Not configured");
                    }
                    return string.IsNullOrWhiteSpace(root) ? remote + ":" : remote + ":" + root;
                default:
                    return EmptyFallback(viewModel.Draft.LocalManifestPath);
            }
        }

        private static string GetSourceKindDisplayName(SetupSourceKind? sourceKind)
        {
            switch (sourceKind)
            {
                case SetupSourceKind.LocalFolder:
                    return PclsResources.Get("LOCPLSSetupSourceLocal", "Local or external drive");
                case SetupSourceKind.NetworkFolder:
                    return PclsResources.Get("LOCPLSSetupSourceNetwork", "NAS or network folder");
                case SetupSourceKind.RcloneRemote:
                    return PclsResources.Get("LOCPLSSourceRclone", "Cloud storage through rclone");
                case SetupSourceKind.ExistingManifest:
                    return PclsResources.Get("LOCPLSSourceExistingManifest", "Existing manifest file");
                default:
                    return PclsResources.Get("LOCPLSNotSelected", "Not selected");
            }
        }

        private static string GetStepTitle(SetupWizardStep step)
        {
            switch (step)
            {
                case SetupWizardStep.ConfigureSource:
                    return PclsResources.Get("LOCPLSSetupStepConfigure", "Configure source");
                case SetupWizardStep.ScanPreview:
                    return PclsResources.Get("LOCPLSSetupStepReviewSource", "Review source");
                case SetupWizardStep.CacheBehavior:
                    return PclsResources.Get("LOCPLSSetupStepCache", "Cache behavior");
                case SetupWizardStep.Review:
                    return PclsResources.Get("LOCPLSSetupStepFinalReview", "Final review");
                case SetupWizardStep.Completed:
                    return PclsResources.Get("LOCPLSSetupStepCompleted", "Completed");
                default:
                    return PclsResources.Get("LOCPLSSetupStepChoose", "Choose source");
            }
        }

        private static string EmptyFallback(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? PclsResources.Get("LOCPLSNotConfigured", "Not configured")
                : value;
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
    }
}
