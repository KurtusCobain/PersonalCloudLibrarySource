using Playnite.SDK;
using System;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudLibraryDashboardWindowService
    {
        private readonly IPlayniteAPI playniteApi;
        private readonly Func<CloudLibraryDashboardView> viewFactory;
        private Window dashboardWindow;

        public CloudLibraryDashboardWindowService(
            IPlayniteAPI playniteApi,
            Func<CloudLibraryDashboardView> viewFactory)
        {
            this.playniteApi = playniteApi ?? throw new ArgumentNullException(nameof(playniteApi));
            this.viewFactory = viewFactory ?? throw new ArgumentNullException(nameof(viewFactory));
        }

        public void OpenDashboard()
        {
            if (playniteApi.ApplicationInfo.Mode != ApplicationMode.Desktop)
            {
                return;
            }

            playniteApi.MainView.UIDispatcher.Invoke(OpenDashboardOnUiThread);
        }

        private void OpenDashboardOnUiThread()
        {
            if (dashboardWindow != null)
            {
                if (dashboardWindow.WindowState == WindowState.Minimized)
                {
                    dashboardWindow.WindowState = WindowState.Normal;
                }

                if (!dashboardWindow.IsVisible)
                {
                    dashboardWindow.Show();
                }

                dashboardWindow.Activate();
                return;
            }

            dashboardWindow = playniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = true,
                ShowMaximizeButton = true
            });
            dashboardWindow.Title = GetLocalizedString("LOCPLSDashboardTitle", "Personal Cloud Library");
            dashboardWindow.Content = viewFactory();
            dashboardWindow.Owner = playniteApi.Dialogs.GetCurrentAppWindow();
            dashboardWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dashboardWindow.Width = 760;
            dashboardWindow.Height = 620;
            dashboardWindow.MinWidth = 560;
            dashboardWindow.MinHeight = 460;
            dashboardWindow.Closed += DashboardWindow_Closed;
            dashboardWindow.Show();
        }

        private void DashboardWindow_Closed(object sender, EventArgs e)
        {
            if (dashboardWindow != null)
            {
                dashboardWindow.Closed -= DashboardWindow_Closed;
                dashboardWindow = null;
            }
        }

        private static string GetLocalizedString(string key, string fallback)
        {
            return ResourceProvider.GetString(key) ?? fallback;
        }
    }
}
