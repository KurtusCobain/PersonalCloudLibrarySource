using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource
{
    public sealed class SetupDraft : ObservableObject
    {
        private SetupSourceKind? selectedSource;
        private string localManifestPath = string.Empty;
        private string localLibraryRoot = string.Empty;
        private string manifestRelativePath = string.Empty;
        private string rcloneExecutablePath = "rclone";
        private string rcloneRemoteName = string.Empty;
        private string rcloneManifestPath = string.Empty;
        private string rcloneContentRoot = string.Empty;
        private int rcloneTimeoutSeconds = 30;
        private string cachePath = string.Empty;
        private bool allowDownloads = true;
        private bool treatMissingFilesAsUninstalled = true;
        private string uninstallBehavior = PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior;

        public SetupSourceKind? SelectedSource
        {
            get => selectedSource;
            set => SetValue(ref selectedSource, value);
        }

        public string LocalManifestPath
        {
            get => localManifestPath;
            set => SetValue(ref localManifestPath, value ?? string.Empty);
        }

        public string LocalLibraryRoot
        {
            get => localLibraryRoot;
            set => SetValue(ref localLibraryRoot, value ?? string.Empty);
        }

        public string ManifestRelativePath
        {
            get => manifestRelativePath;
            set => SetValue(ref manifestRelativePath, value ?? string.Empty);
        }

        public string RcloneExecutablePath
        {
            get => rcloneExecutablePath;
            set => SetValue(ref rcloneExecutablePath, value ?? string.Empty);
        }

        public string RcloneRemoteName
        {
            get => rcloneRemoteName;
            set => SetValue(ref rcloneRemoteName, value ?? string.Empty);
        }

        public string RcloneManifestPath
        {
            get => rcloneManifestPath;
            set => SetValue(ref rcloneManifestPath, value ?? string.Empty);
        }

        public string RcloneContentRoot
        {
            get => rcloneContentRoot;
            set => SetValue(ref rcloneContentRoot, value ?? string.Empty);
        }

        public int RcloneTimeoutSeconds
        {
            get => rcloneTimeoutSeconds;
            set => SetValue(ref rcloneTimeoutSeconds, value);
        }

        public string CachePath
        {
            get => cachePath;
            set => SetValue(ref cachePath, value ?? string.Empty);
        }

        public bool AllowDownloads
        {
            get => allowDownloads;
            set => SetValue(ref allowDownloads, value);
        }

        public bool TreatMissingFilesAsUninstalled
        {
            get => treatMissingFilesAsUninstalled;
            set => SetValue(ref treatMissingFilesAsUninstalled, value);
        }

        public string UninstallBehavior
        {
            get => uninstallBehavior;
            set => SetValue(ref uninstallBehavior, value ?? PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior);
        }

        public static SetupDraft FromSettings(PersonalCloudLibrarySourceSettingsV3 settings)
        {
            settings = settings ?? new PersonalCloudLibrarySourceSettingsV3();

            return new SetupDraft
            {
                SelectedSource = InferSourceKind(settings.SourceProviderType),
                LocalManifestPath = settings.LocalManifestPath,
                LocalLibraryRoot = settings.LocalLibraryRoot,
                ManifestRelativePath = settings.ManifestRelativePath,
                RcloneExecutablePath = settings.RcloneExecutablePath,
                RcloneRemoteName = settings.RcloneRemoteName,
                RcloneManifestPath = settings.RcloneManifestPath,
                RcloneContentRoot = settings.RcloneContentRoot,
                RcloneTimeoutSeconds = settings.RcloneTimeoutSeconds,
                CachePath = settings.LocalCacheFolder,
                AllowDownloads = settings.AllowDownloads,
                TreatMissingFilesAsUninstalled = settings.TreatMissingFilesAsUninstalled,
                UninstallBehavior = settings.UninstallBehavior
            };
        }

        public void ApplyTo(PersonalCloudLibrarySourceSettingsV3 target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!SelectedSource.HasValue)
            {
                throw new InvalidOperationException("A source type must be selected before setup can be completed.");
            }

            target.Enabled = true;
            target.SettingsVersion = PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion;
            target.SourceProviderType = GetProviderType(SelectedSource.Value);
            target.LocalManifestPath = LocalManifestPath;
            target.LocalLibraryRoot = LocalLibraryRoot;
            target.ManifestRelativePath = ManifestRelativePath;
            target.RcloneExecutablePath = RcloneExecutablePath;
            target.RcloneRemoteName = RcloneRemoteName;
            target.RcloneManifestPath = RcloneManifestPath;
            target.RcloneContentRoot = RcloneContentRoot;
            target.RcloneTimeoutSeconds = RcloneTimeoutSeconds;
            target.LocalCacheFolder = CachePath;
            target.AllowDownloads = AllowDownloads;
            target.TreatMissingFilesAsUninstalled = TreatMissingFilesAsUninstalled;
            target.UninstallBehavior = UninstallBehavior;
        }

        private static SetupSourceKind InferSourceKind(string providerType)
        {
            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return SetupSourceKind.RcloneRemote;
            }

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return SetupSourceKind.LocalFolder;
            }

            return SetupSourceKind.ExistingManifest;
        }

        private static string GetProviderType(SetupSourceKind sourceKind)
        {
            switch (sourceKind)
            {
                case SetupSourceKind.LocalFolder:
                case SetupSourceKind.NetworkFolder:
                    return PersonalCloudLibrarySourceSettings.LocalFolderProviderType;
                case SetupSourceKind.RcloneRemote:
                    return PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType;
                default:
                    return PersonalCloudLibrarySourceSettings.LocalFileProviderType;
            }
        }
    }
}
