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
                result.CanInstall = true;
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
            else if (!File.Exists(result.UninstallTarget) && !Directory.Exists(result.UninstallTarget))
            {
                result.UninstallRefusalReason = "The authorized cache target is unavailable.";
            }
            else
            {
                result.CanUninstall = true;
            }

            return result;
        }
    }
}
