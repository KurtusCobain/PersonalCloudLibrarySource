using System;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public sealed class GameWorkflowPolicyResult
    {
        public CachePathResolution Paths { get; set; } = new CachePathResolution();
        public LibraryItemState State { get; set; } = new LibraryItemState();
        public bool CanInstall { get; set; }
        public string InstallRefusalReason { get; set; } = string.Empty;
        public bool CanUninstall { get; set; }
        public string UninstallTarget { get; set; } = string.Empty;
        public string UninstallRefusalReason { get; set; } = string.Empty;
    }

    // Compare only execution-relevant fields, not display metadata. Exact
    // comparison is deliberately conservative for case-sensitive remotes.
    public static class LegacyActionConfigurationGuard
    {
        public static bool Matches(PersonalCloudLibrarySourceSettingsV3 expected,
            PersonalCloudLibrarySourceSettingsV3 current,
            PersonalCloudLibraryItem item, PersonalCloudLibraryItem latest)
        {
            return expected != null && current != null && item != null && latest != null &&
                expected.Enabled && current.Enabled &&
                !expected.EnableMultiSourceProfiles && !current.EnableMultiSourceProfiles &&
                expected.AllowDownloads == current.AllowDownloads &&
                expected.AllowUninstallOutsideCacheFolder == current.AllowUninstallOutsideCacheFolder &&
                expected.TreatMissingFilesAsUninstalled == current.TreatMissingFilesAsUninstalled &&
                expected.VerifyAfterTransfer == current.VerifyAfterTransfer &&
                expected.RemoveIncompleteTransferFiles == current.RemoveIncompleteTransferFiles &&
                expected.RcloneTimeoutSeconds == current.RcloneTimeoutSeconds &&
                Same(expected.SourceProviderType, current.SourceProviderType) &&
                Same(expected.LocalManifestPath, current.LocalManifestPath) &&
                Same(expected.LocalLibraryRoot, current.LocalLibraryRoot) &&
                Same(expected.ManifestRelativePath, current.ManifestRelativePath) &&
                Same(expected.LocalCacheFolder, current.LocalCacheFolder) &&
                Same(expected.RcloneExecutablePath, current.RcloneExecutablePath) &&
                Same(expected.RcloneRemoteName, current.RcloneRemoteName) &&
                Same(expected.RcloneManifestPath, current.RcloneManifestPath) &&
                Same(expected.RcloneContentRoot, current.RcloneContentRoot) &&
                Same(expected.UninstallBehavior, current.UninstallBehavior) &&
                Same(item.Id, latest.Id) &&
                Same(item.SourceType, latest.SourceType) &&
                Same(item.SourcePath, latest.SourcePath) &&
                Same(item.RemotePath, latest.RemotePath) &&
                Same(item.CachePath, latest.CachePath) &&
                Same(item.LocalPath, latest.LocalPath) &&
                Same(item.InstallDirectory, latest.InstallDirectory) &&
                Same(item.LaunchFile, latest.LaunchFile) &&
                Same(item.PackageRole, latest.PackageRole);
        }

        private static bool Same(string first, string second) =>
            string.Equals(first, second, StringComparison.Ordinal);
    }

    public sealed class GameWorkflowPolicyService
    {
        public GameWorkflowPolicyResult Evaluate(
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings)
        {
            var result = new GameWorkflowPolicyResult();
            if (item == null || settings == null)
            {
                result.InstallRefusalReason = "Game or settings are unavailable.";
                result.UninstallRefusalReason = result.InstallRefusalReason;
                return result;
            }

            result.Paths = new CachePathResolver().Resolve(item, settings);
            if (!result.Paths.Succeeded)
            {
                result.InstallRefusalReason = result.Paths.Error;
                result.UninstallRefusalReason = result.Paths.Error;
                return result;
            }
            result.State = new LibraryItemStateResolver().Resolve(
                item,
                result.Paths.LaunchPath,
                result.Paths.InstallDirectory,
                settings.TreatMissingFilesAsUninstalled);

            var sourcePath = PersonalCloudLibrarySource.GetItemSourcePath(item);
            if (!settings.Enabled)
            {
                result.InstallRefusalReason = "The library source is disabled.";
            }
            else if (!settings.AllowDownloads)
            {
                result.InstallRefusalReason = "Downloads are disabled.";
            }
            else if (result.State.IsCached)
            {
                result.InstallRefusalReason = "The game is already cached locally.";
            }
            else if (string.IsNullOrWhiteSpace(sourcePath) ||
                !PersonalCloudLibrarySource.CanResolveSourcePath(settings, sourcePath))
            {
                result.InstallRefusalReason = "The source path is unavailable or cannot be resolved.";
            }
            else if (!result.Paths.Succeeded)
            {
                result.InstallRefusalReason = result.Paths.Error;
            }
            else
            {
                // Externally stored entries may be catalogued or played, but installs
                // must write only inside the user-selected cache folder.
                var destination = string.Equals(
                    PersonalCloudLibrarySource.GetItemSourceType(item), "directory",
                    StringComparison.OrdinalIgnoreCase)
                    ? result.Paths.DestinationDirectory
                    : result.Paths.DestinationFile;
                if (string.IsNullOrWhiteSpace(destination) ||
                    !PathBoundary.IsContained(settings.LocalCacheFolder, destination) ||
                    string.Equals(
                        PathBoundary.Normalize(settings.LocalCacheFolder),
                        PathBoundary.Normalize(destination),
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.InstallRefusalReason = "Install destination must be inside LocalCacheFolder.";
                }
                else
                {
                    result.CanInstall = true;
                }
            }

            var requestedTarget = PersonalCloudLibrarySource.ResolveUninstallTargetPath(
                item,
                settings,
                result.Paths.LaunchPath,
                result.Paths.InstallDirectory);
            string refusalReason;
            result.UninstallTarget = PersonalCloudLibrarySource.ResolveSafeUninstallTarget(
                settings,
                requestedTarget,
                out refusalReason);
            if (!result.State.IsCached)
            {
                result.UninstallRefusalReason = "No cached content is available to remove.";
            }
            else if (!string.IsNullOrWhiteSpace(refusalReason))
            {
                result.UninstallRefusalReason = refusalReason;
            }
            else if (!PersonalCloudLibrarySource.IsDirectoryItem(item) &&
                !string.Equals(settings.UninstallBehavior,
                    PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.UninstallRefusalReason = "File-based items cannot authorize recursive folder removal.";
            }
            else if (PersonalCloudLibrarySource.IsDirectoryItem(item) &&
                !string.Equals(settings.UninstallBehavior,
                    PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.UninstallRefusalReason = "Directory cache ownership is unverified; recursive removal is disabled.";
            }
            else if (!settings.Enabled)
            {
                result.UninstallRefusalReason = "The library source is disabled.";
            }
            else if (!File.Exists(result.UninstallTarget) || Directory.Exists(result.UninstallTarget))
            {
                result.UninstallRefusalReason = "The authorized cache file is unavailable.";
            }
            else
            {
                result.CanUninstall = true;
            }

            return result;
        }
    }
}
