using System;

namespace PersonalCloudLibrarySource
{
    public sealed class PluginNavigationService
    {
        private readonly Action openDashboard;
        private readonly Action openSettings;
        private readonly Action verifyLibrary;
        private readonly Action openCacheFolder;
        private readonly Action openLatestReport;
        private readonly Action generateManifest;
        private readonly Action showUpdateLibraryInstructions;
        private readonly Action openSourceLocation;

        public PluginNavigationService(
            Action openDashboard,
            Action openSettings,
            Action verifyLibrary,
            Action openCacheFolder,
            Action openLatestReport,
            Action generateManifest,
            Action showUpdateLibraryInstructions,
            Action openSourceLocation)
        {
            this.openDashboard = openDashboard ?? throw new ArgumentNullException(nameof(openDashboard));
            this.openSettings = openSettings ?? throw new ArgumentNullException(nameof(openSettings));
            this.verifyLibrary = verifyLibrary ?? throw new ArgumentNullException(nameof(verifyLibrary));
            this.openCacheFolder = openCacheFolder ?? throw new ArgumentNullException(nameof(openCacheFolder));
            this.openLatestReport = openLatestReport ?? throw new ArgumentNullException(nameof(openLatestReport));
            this.generateManifest = generateManifest ?? throw new ArgumentNullException(nameof(generateManifest));
            this.showUpdateLibraryInstructions = showUpdateLibraryInstructions ?? throw new ArgumentNullException(nameof(showUpdateLibraryInstructions));
            this.openSourceLocation = openSourceLocation ?? throw new ArgumentNullException(nameof(openSourceLocation));
        }

        public void OpenDashboard() => openDashboard();
        public void OpenSettings() => openSettings();
        public void VerifyLibrary() => verifyLibrary();
        public void OpenCacheFolder() => openCacheFolder();
        public void OpenLatestReport() => openLatestReport();
        public void GenerateManifest() => generateManifest();
        public void ShowUpdateLibraryInstructions() => showUpdateLibraryInstructions();
        public void OpenSourceLocation() => openSourceLocation();
    }
}
