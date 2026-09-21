using Playnite.SDK;
using System;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudGameDetailsWindowService
    {
        private readonly IPlayniteAPI playniteApi;

        public CloudGameDetailsWindowService(IPlayniteAPI playniteApi)
        {
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
        }

        public void Open(CloudGameDetailsViewModel viewModel)
        {
            if (viewModel == null || playniteApi.ApplicationInfo.Mode != ApplicationMode.Desktop)
            {
                return;
            }

            playniteApi.MainView.UIDispatcher.Invoke(() =>
            {
                var window = playniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowMinimizeButton = false,
                    ShowMaximizeButton = true
                });
                window.Title = PclsResources.Format(
                    "LOCPLSGameDetailsWindowTitle",
                    "{0} — Personal Cloud Library",
                    viewModel.Title);
                window.Content = new CloudGameDetailsView
                {
                    DataContext = viewModel
                };
                window.Owner = playniteApi.Dialogs.GetCurrentAppWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.Width = 720;
                window.Height = 620;
                window.MinWidth = 560;
                window.MinHeight = 460;
                window.Show();
            });
        }
    }
}
