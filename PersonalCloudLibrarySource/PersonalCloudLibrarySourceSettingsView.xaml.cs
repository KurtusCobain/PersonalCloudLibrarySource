using System.Windows;
using System.Windows.Controls;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySourceSettingsView : UserControl
    {
        public PersonalCloudLibrarySourceSettingsView()
        {
            InitializeComponent();
        }

        private PersonalCloudLibrarySourceSettingsViewModel ViewModel => DataContext as PersonalCloudLibrarySourceSettingsViewModel;

        private void BrowseLocalManifestPath_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.BrowseLocalManifestPath();
        }

        private void BrowseLocalLibraryRoot_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.BrowseLocalLibraryRoot();
        }

        private void BrowseLocalCacheFolder_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.BrowseLocalCacheFolder();
        }

        private void BrowseRcloneExecutablePath_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.BrowseRcloneExecutablePath();
        }

        private void GenerateManifestFromFolder_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.GenerateManifestFromFolder();
        }

        private void OpenGeneratedManifest_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.OpenGeneratedManifest();
        }

        private void OpenGeneratedReport_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.OpenGeneratedReport();
        }

        private void VerifySetup_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.VerifySetup();
        }

        private void TestRcloneConnection_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.TestRcloneConnection();
        }

        private void TestManifestLoad_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.TestManifestLoad();
        }

        private void OpenCacheFolder_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.OpenCacheFolder();
        }

        private void OpenDiagnosticsFolder_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.OpenDiagnosticsFolder();
        }

        private void CreateSampleManifest_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.CreateSampleManifest();
        }

        private void ShowUpdateLibraryInstructions_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ShowUpdateLibraryInstructions();
        }
    }
}
