using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    public sealed class GameCommandTarget
    {
        public Game Game { get; set; }
        public PersonalCloudLibraryItem Item { get; set; }
        public GameCommandContext PolicyContext { get; set; }
        public string SourceDisplayPath { get; set; }
        public string ResolvedLocalSourcePath { get; set; }
        public string LaunchPath { get; set; }
        public string InstallDirectory { get; set; }
        public string CacheDisplayPath { get; set; }
        public string SafeUninstallTarget { get; set; }
        public string UninstallRefusalReason { get; set; }
    }

    public sealed class GameCommandService
    {
        public IReadOnlyList<GameCommandTarget> ResolveTargets(
            IEnumerable<Game> selectedGames,
            IEnumerable<PersonalCloudLibraryItem> manifestItems,
            PersonalCloudLibrarySourceSettings settings,
            Guid pluginId)
        {
            var games = selectedGames?.Where(game => game != null).ToList() ?? new List<Game>();
            var items = (manifestItems ?? Enumerable.Empty<PersonalCloudLibraryItem>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            var results = new List<GameCommandTarget>();
            foreach (var game in games)
            {
                PersonalCloudLibraryItem item = null;
                if (!string.IsNullOrWhiteSpace(game.GameId))
                {
                    items.TryGetValue(game.GameId, out item);
                }

                results.Add(ResolveTarget(game, item, settings, pluginId));
            }

            return results;
        }

        private static GameCommandTarget ResolveTarget(
            Game game,
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings,
            Guid pluginId)
        {
            settings = settings ?? new PersonalCloudLibrarySourceSettings();
            var belongsToPlugin = game.PluginId == pluginId;
            var sourcePath = item == null
                ? string.Empty
                : PersonalCloudLibrarySource.GetItemSourcePath(item);
            var launchPath = item == null
                ? string.Empty
                : PersonalCloudLibrarySource.ResolveLaunchPath(item, settings);
            var installDirectory = item == null
                ? string.Empty
                : PersonalCloudLibrarySource.ResolveInstallDirectory(item, settings, launchPath);
            var itemState = new LibraryItemStateResolver().Resolve(
                item,
                launchPath,
                installDirectory,
                settings.TreatMissingFilesAsUninstalled);
            var cachedFileExists = itemState.HasPlayAction;
            var cachedDirectoryExists = itemState.IsCached && !cachedFileExists;
            var hasCachedPath = itemState.IsCached;
            var cacheDisplayPath = cachedFileExists
                ? launchPath
                : cachedDirectoryExists
                    ? installDirectory
                    : !string.IsNullOrWhiteSpace(launchPath)
                        ? launchPath
                        : installDirectory;

            var uninstallTarget = string.Empty;
            var uninstallRefusalReason = string.Empty;
            var canRemoveCachedCopy = false;
            if (item != null && hasCachedPath)
            {
                var requestedTarget = PersonalCloudLibrarySource.ResolveUninstallTargetPath(
                    item,
                    settings,
                    launchPath,
                    installDirectory);
                uninstallTarget = PersonalCloudLibrarySource.ResolveSafeUninstallTarget(
                    settings,
                    requestedTarget,
                    out uninstallRefusalReason);
                canRemoveCachedCopy = string.IsNullOrWhiteSpace(uninstallRefusalReason) &&
                    (File.Exists(uninstallTarget) || Directory.Exists(uninstallTarget));
            }

            var providerType = PersonalCloudLibrarySource.GetProviderType(settings);
            var resolvedLocalSourcePath = string.Empty;
            var sourceDisplayPath = sourcePath ?? string.Empty;
            var canOpenSourceLocation = false;
            if (item != null && !string.IsNullOrWhiteSpace(sourcePath))
            {
                if (string.Equals(
                    providerType,
                    PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                    StringComparison.OrdinalIgnoreCase))
                {
                    var remotePath = PersonalCloudLibrarySource.ResolveRcloneSourcePath(settings, sourcePath);
                    sourceDisplayPath = string.IsNullOrWhiteSpace(settings.RcloneRemoteName)
                        ? remotePath
                        : settings.RcloneRemoteName.TrimEnd(':') + ":" + remotePath;
                }
                else
                {
                    resolvedLocalSourcePath = PersonalCloudLibrarySource.ResolveLocalFolderSourcePath(settings, sourcePath);
                    sourceDisplayPath = resolvedLocalSourcePath;
                    canOpenSourceLocation = File.Exists(resolvedLocalSourcePath) || Directory.Exists(resolvedLocalSourcePath);
                }
            }

            var canInstall = belongsToPlugin &&
                item != null &&
                settings.Enabled &&
                settings.AllowDownloads &&
                !hasCachedPath &&
                !string.IsNullOrWhiteSpace(sourcePath) &&
                PersonalCloudLibrarySource.CanResolveSourcePath(settings, sourcePath);

            return new GameCommandTarget
            {
                Game = game,
                Item = item,
                SourceDisplayPath = sourceDisplayPath ?? string.Empty,
                ResolvedLocalSourcePath = resolvedLocalSourcePath ?? string.Empty,
                LaunchPath = launchPath ?? string.Empty,
                InstallDirectory = installDirectory ?? string.Empty,
                CacheDisplayPath = cacheDisplayPath ?? string.Empty,
                SafeUninstallTarget = uninstallTarget ?? string.Empty,
                UninstallRefusalReason = uninstallRefusalReason ?? string.Empty,
                PolicyContext = new GameCommandContext
                {
                    BelongsToPlugin = belongsToPlugin,
                    HasManifestItem = item != null,
                    IsInstalled = itemState.IsInstalled,
                    CanInstall = canInstall,
                    HasCachedPath = hasCachedPath,
                    CanRemoveCachedCopy = canRemoveCachedCopy,
                    HasSourcePath = item != null && !string.IsNullOrWhiteSpace(sourcePath),
                    CanOpenSourceLocation = canOpenSourceLocation
                }
            };
        }
    }
}
