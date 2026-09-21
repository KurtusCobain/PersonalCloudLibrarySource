using System;
using System.Windows;
using System.Windows.Controls;

namespace PersonalCloudLibrarySource
{
    public partial class CloudLibraryDashboardView : UserControl
    {
        private DashboardViewModelLifetime viewModelLifetime;

        public CloudLibraryDashboardView()
        {
            InitializeComponent();
            Loaded += CloudLibraryDashboardView_Loaded;
            Unloaded += CloudLibraryDashboardView_Unloaded;
        }

        public void SetViewModelFactory(Func<IDisposable> factory)
        {
            ReleaseViewModel();
            viewModelLifetime = new DashboardViewModelLifetime(factory);
            if (IsLoaded)
            {
                DataContext = viewModelLifetime.Activate();
            }
        }

        public void ReleaseViewModel()
        {
            DataContext = null;
            viewModelLifetime?.Deactivate();
        }

        private void CloudLibraryDashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            if (viewModelLifetime != null)
            {
                DataContext = viewModelLifetime.Activate();
            }
        }

        private void CloudLibraryDashboardView_Unloaded(object sender, RoutedEventArgs e)
        {
            ReleaseViewModel();
        }
    }
}
