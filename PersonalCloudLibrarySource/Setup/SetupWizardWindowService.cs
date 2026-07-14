using Playnite.SDK;
using System;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public sealed class SetupWizardWindowService
    {
        private readonly IPlayniteAPI playniteApi;
        private readonly PersonalCloudLibrarySourceSettingsV3ViewModel settingsViewModel;
        private readonly Func<string> defaultCachePathProvider;
        private readonly Action prepareSetupCompletion;
        private readonly Action setupSaved;
        private readonly Action setupDismissed;
        private readonly SetupCompletionCoordinator completionCoordinator = new SetupCompletionCoordinator();
        private Window wizardWindow;
        private bool editCompleted;

        public SetupWizardWindowService(
            IPlayniteAPI playniteApi,
            PersonalCloudLibrarySourceSettingsV3ViewModel settingsViewModel,
            Func<string> defaultCachePathProvider,
            Action prepareSetupCompletion,
            Action setupSaved,
            Action setupDismissed)
        {
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
            this.settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
            this.defaultCachePathProvider = defaultCachePathProvider ?? throw new ArgumentNullException(nameof(defaultCachePathProvider));
            this.prepareSetupCompletion = prepareSetupCompletion ?? throw new ArgumentNullException(nameof(prepareSetupCompletion));
            this.setupSaved = setupSaved ?? throw new ArgumentNullException(nameof(setupSaved));
            this.setupDismissed = setupDismissed ?? throw new ArgumentNullException(nameof(setupDismissed));
        }

        public void OpenWizard()
        {
            if (playniteApi.ApplicationInfo.Mode != ApplicationMode.Desktop)
            {
                return;
            }

            playniteApi.MainView.UIDispatcher.Invoke(OpenWizardOnUiThread);
        }

        private void OpenWizardOnUiThread()
        {
            if (wizardWindow != null)
            {
                if (wizardWindow.WindowState == WindowState.Minimized)
                {
                    wizardWindow.WindowState = WindowState.Normal;
                }

                wizardWindow.Activate();
                return;
            }

            editCompleted = false;
            var viewModel = new SetupWizardViewModel(settingsViewModel.Settings, new SetupValidationService());
            if (string.IsNullOrWhiteSpace(viewModel.Draft.CachePath))
            {
                viewModel.Draft.CachePath = defaultCachePathProvider();
            }

            wizardWindow = playniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true
            });
            wizardWindow.Title = ResourceProvider.GetString("LOCPLSSetupWizardTitle") ?? "Personal Cloud Library Setup";
            wizardWindow.Content = new SetupWizardView(viewModel, HandleCompleted, HandleCancelled);
            wizardWindow.Owner = playniteApi.Dialogs.GetCurrentAppWindow();
            wizardWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wizardWindow.Width = 780;
            wizardWindow.Height = 680;
            wizardWindow.MinWidth = 600;
            wizardWindow.MinHeight = 520;
            wizardWindow.Closed += WizardWindow_Closed;
            try
            {
                settingsViewModel.BeginEdit();
                wizardWindow.Show();
            }
            catch (Exception)
            {
                settingsViewModel.CancelEdit();
                wizardWindow.Closed -= WizardWindow_Closed;
                wizardWindow = null;
                throw;
            }
        }

        private bool HandleCompleted()
        {
            try
            {
                var completed = completionCoordinator.Complete(
                    prepareSetupCompletion,
                    () =>
                    {
                        settingsViewModel.EndEdit();
                        return settingsViewModel.LastEditSavedSuccessfully;
                    },
                    setupSaved);
                if (!completed)
                {
                    playniteApi.Dialogs.ShowMessage(
                        "Setup could not be saved. Review the current values and try again.",
                        "Personal Cloud Library Setup");
                    return false;
                }

                editCompleted = true;
                CloseWindow();
                return true;
            }
            catch (Exception ex)
            {
                playniteApi.Dialogs.ShowMessage(
                    "Setup could not be saved: " + ex.Message,
                    "Personal Cloud Library Setup");
                return false;
            }
        }

        private void HandleCancelled()
        {
            settingsViewModel.CancelEdit();
            try
            {
                setupDismissed();
            }
            catch (Exception ex)
            {
                playniteApi.Dialogs.ShowMessage(
                    "The setup reminder state could not be saved: " + ex.Message,
                    "Personal Cloud Library Setup");
            }
            editCompleted = true;
            CloseWindow();
        }

        private void CloseWindow()
        {
            if (wizardWindow != null)
            {
                wizardWindow.Close();
            }
        }

        private void WizardWindow_Closed(object sender, EventArgs e)
        {
            if (!editCompleted)
            {
                settingsViewModel.CancelEdit();
                try
                {
                    setupDismissed();
                }
                catch (Exception ex)
                {
                    playniteApi.Dialogs.ShowMessage(
                        "The setup reminder state could not be saved: " + ex.Message,
                        "Personal Cloud Library Setup");
                }
            }

            if (wizardWindow != null)
            {
                wizardWindow.Closed -= WizardWindow_Closed;
                wizardWindow = null;
            }
        }
    }
}
