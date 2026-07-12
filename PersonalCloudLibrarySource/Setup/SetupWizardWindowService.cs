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
        private readonly Action setupCompleted;
        private Window wizardWindow;

        public SetupWizardWindowService(
            IPlayniteAPI playniteApi,
            PersonalCloudLibrarySourceSettingsV3ViewModel settingsViewModel,
            Func<string> defaultCachePathProvider,
            Action setupCompleted)
        {
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
            this.settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
            this.defaultCachePathProvider = defaultCachePathProvider ?? throw new ArgumentNullException(nameof(defaultCachePathProvider));
            this.setupCompleted = setupCompleted ?? throw new ArgumentNullException(nameof(setupCompleted));
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
            wizardWindow.Show();
        }

        private void HandleCompleted()
        {
            settingsViewModel.EndEdit();
            setupCompleted();
            CloseWindow();
        }

        private void HandleCancelled()
        {
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
            if (wizardWindow != null)
            {
                wizardWindow.Closed -= WizardWindow_Closed;
                wizardWindow = null;
            }
        }
    }
}
