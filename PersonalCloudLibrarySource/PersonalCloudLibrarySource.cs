using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;

namespace PersonalCloudLibrarySource
{
    public class PersonalCloudLibrarySource : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly RcloneManifestReader rcloneManifestReader = new RcloneManifestReader();
        private readonly RcloneFileCopier rcloneFileCopier = new RcloneFileCopier();
        private readonly LocalFileCopier localFileCopier = new LocalFileCopier();
        private readonly ManifestGenerationService manifestGenerationService = new ManifestGenerationService();
        private readonly IPlayniteAPI playniteApi;

        private PersonalCloudLibrarySourceSettingsViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("61993828-67a8-4468-93a2-293442e36328");

        public override string Name => ResolveLibraryDisplayName(settings?.Settings);

        public override LibraryClient Client { get; } = new PersonalCloudLibrarySourceClient();

        public PersonalCloudLibrarySource(IPlayniteAPI api) : base(api)
        {
            playniteApi = api;
            settings = new PersonalCloudLibrarySourceSettingsViewModel(this);

            Properties = new LibraryPluginProperties
            {
                HasSettings = true
            };
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var importedGames = new List<GameMetadata>();
            var diagnostics = new List<string>();
            PersonalCloudLibrarySourceSettings pluginSettings = null;

            try
            {
                pluginSettings = settings.Settings;

                if (pluginSettings == null || !pluginSettings.Enabled)
                {
                    logger.Info("Personal Cloud Library Source is disabled. No games imported.");
                    return importedGames;
                }

                var providerType = GetProviderType(pluginSettings);
                diagnostics.Add($"provider={providerType}");
                diagnostics.Add($"libraryDisplayName={ResolveLibraryDisplayName(pluginSettings)}");
                diagnostics.Add($"manifestPath={ResolveManifestDescription(pluginSettings)}");

                var json = LoadManifestJson(pluginSettings);
                var manifest = ParseManifest(json);
                diagnostics.Add($"itemCount={manifest.Items.Count}");
                importedGames = ConvertManifestItemsToGameMetadata(manifest, pluginSettings, diagnostics);
                diagnostics.Add($"returnedGameCount={importedGames.Count}");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Personal Cloud Library Source failed to import the manifest.");
                diagnostics.Add($"importError={ex.Message}");
            }
            finally
            {
                WriteImportDiagnostics(pluginSettings, diagnostics);
            }

            logger.Info($"Personal Cloud Library Source imported {importedGames.Count} manifest entries.");
            return importedGames;
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            var installActions = new List<InstallController>();

            try
            {
                var pluginSettings = settings.Settings;
                if (args?.Game == null)
                {
                    logger.Info("Personal Cloud Library Source install action not returned: no game context.");
                    return installActions;
                }

                if (pluginSettings == null || !pluginSettings.Enabled)
                {
                    logger.Info($"Personal Cloud Library Source install action not returned for {args.Game.GameId}: plugin disabled.");
                    return installActions;
                }

                if (!pluginSettings.AllowDownloads)
                {
                    logger.Info($"Personal Cloud Library Source install action not returned for {args.Game.GameId}: downloads disabled.");
                    return installActions;
                }

                if (args.Game.PluginId != Id)
                {
                    logger.Info($"Personal Cloud Library Source install action not returned for {args.Game.GameId}: game belongs to another plugin.");
                    return installActions;
                }

                var json = LoadManifestJson(pluginSettings);
                var manifest = ParseManifest(json);

                foreach (var item in manifest.Items)
                {
                    if (item == null ||
                        !string.Equals(item.Id, args.Game.GameId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var sourcePath = GetItemSourcePath(item);
                    if (string.IsNullOrWhiteSpace(sourcePath))
                    {
                        logger.Warn($"Personal Cloud Library Source item {item.Id} has no sourcePath or legacy remotePath and cannot be downloaded.");
                        return installActions;
                    }

                    var launchPath = ResolveLaunchPath(item, pluginSettings);
                    var installDirectory = ResolveInstallDirectory(item, pluginSettings, launchPath);
                    var launchFileExists = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
                    var installDirectoryExists = !string.IsNullOrWhiteSpace(installDirectory) && Directory.Exists(installDirectory);
                    if (launchFileExists || (IsDirectoryItem(item) && installDirectoryExists))
                    {
                        logger.Info($"Personal Cloud Library Source install action not returned for {item.Id}: launch file already exists.");
                        return installActions;
                    }

                    if (!CanResolveSourcePath(pluginSettings, sourcePath))
                    {
                        logger.Info($"Personal Cloud Library Source install action not returned for {item.Id}: provider cannot resolve sourcePath.");
                        return installActions;
                    }

                    installActions.Add(new RcloneInstallController(
                        args.Game,
                        item,
                        pluginSettings,
                        rcloneFileCopier,
                        localFileCopier));
                    logger.Info($"Personal Cloud Library Source install action returned for {item.Id}.");
                    return installActions;
                }

                logger.Info($"Personal Cloud Library Source install action not returned for {args.Game.GameId}: manifest item was not found.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Personal Cloud Library Source failed to prepare rclone install action.");
            }

            return installActions;
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            var uninstallActions = new List<UninstallController>();

            try
            {
                var pluginSettings = settings.Settings;
                if (args?.Game == null)
                {
                    logger.Info("Personal Cloud Library Source uninstall action not returned: no game context.");
                    return uninstallActions;
                }

                if (pluginSettings == null || !pluginSettings.Enabled)
                {
                    logger.Info($"Personal Cloud Library Source uninstall action not returned for {args.Game.GameId}: plugin disabled.");
                    return uninstallActions;
                }

                if (args.Game.PluginId != Id)
                {
                    logger.Info($"Personal Cloud Library Source uninstall action not returned for {args.Game.GameId}: game belongs to another plugin.");
                    return uninstallActions;
                }

                var json = LoadManifestJson(pluginSettings);
                var manifest = ParseManifest(json);

                foreach (var item in manifest.Items)
                {
                    if (item == null ||
                        !string.Equals(item.Id, args.Game.GameId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var launchPath = ResolveLaunchPath(item, pluginSettings);
                    var installDirectory = ResolveInstallDirectory(item, pluginSettings, launchPath);
                    var launchExists = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
                    var installDirectoryExists = !string.IsNullOrWhiteSpace(installDirectory) && Directory.Exists(installDirectory);
                    string refusalReason;
                    var targetPath = ResolveSafeUninstallTarget(
                        pluginSettings,
                        ResolveUninstallTargetPath(item, pluginSettings, launchPath, installDirectory),
                        out refusalReason);
                    var insideCache = IsPathInsideCacheFolder(targetPath, pluginSettings.LocalCacheFolder);

                    logger.Info(
                        $"Personal Cloud Library Source uninstall action check: gameId={item.Id}; title={item.Title}; launchPath={launchPath}; launchExists={launchExists}; installDirectory={installDirectory}; installDirectoryExists={installDirectoryExists}; behavior={pluginSettings.UninstallBehavior}; targetPath={targetPath}; insideCache={insideCache}; refusalReason={refusalReason}");

                    if (!launchExists && !installDirectoryExists)
                    {
                        logger.Info($"Personal Cloud Library Source uninstall action not returned for {item.Id}: cached file/folder is missing.");
                        return uninstallActions;
                    }

                    if (!string.IsNullOrWhiteSpace(refusalReason))
                    {
                        logger.Warn($"Personal Cloud Library Source uninstall action not returned for {item.Id}: {refusalReason}");
                        return uninstallActions;
                    }

                    if (!File.Exists(targetPath) && !Directory.Exists(targetPath))
                    {
                        logger.Info($"Personal Cloud Library Source uninstall action not returned for {item.Id}: uninstall target does not exist.");
                        return uninstallActions;
                    }

                    uninstallActions.Add(new PersonalCloudLibraryUninstallController(playniteApi, args.Game, item, pluginSettings));
                    logger.Info($"Personal Cloud Library Source uninstall action returned for {item.Id}.");
                    return uninstallActions;
                }

                logger.Info($"Personal Cloud Library Source uninstall action not returned for {args.Game.GameId}: manifest item was not found.");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Personal Cloud Library Source failed to prepare uninstall action.");
            }

            return uninstallActions;
        }

        public static string GetProviderType(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            if (pluginSettings == null)
            {
                return PersonalCloudLibrarySourceSettings.LocalFileProviderType;
            }

            return string.IsNullOrWhiteSpace(pluginSettings.SourceProviderType)
                ? PersonalCloudLibrarySourceSettings.LocalFileProviderType
                : pluginSettings.SourceProviderType;
        }

        public static string ResolveLibraryDisplayName(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            var displayName = pluginSettings?.LibraryDisplayName;
            return string.IsNullOrWhiteSpace(displayName)
                ? "Personal Cloud Library Source"
                : displayName.Trim();
        }

        private string LoadManifestJson(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            var sourceProviderType = GetProviderType(pluginSettings);

            if (string.Equals(
                sourceProviderType,
                PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                logger.Info("Personal Cloud Library Source loading manifest using rclone.");
                return rcloneManifestReader.ReadManifestJson(pluginSettings);
            }

            if (string.Equals(
                sourceProviderType,
                PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                var localFolderManifestPath = ResolveLocalFolderManifestPath(pluginSettings);
                if (string.IsNullOrWhiteSpace(localFolderManifestPath))
                {
                    throw new InvalidOperationException("Personal Cloud Library Source local folder manifest path could not be resolved.");
                }

                if (!File.Exists(localFolderManifestPath))
                {
                    throw new FileNotFoundException("Personal Cloud Library Source local folder manifest was not found.", localFolderManifestPath);
                }

                return File.ReadAllText(localFolderManifestPath);
            }

            if (!string.Equals(
                sourceProviderType,
                PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Source provider type must be LocalFile, LocalFolder, or RcloneRemote.");
            }

            if (string.IsNullOrWhiteSpace(pluginSettings.LocalManifestPath))
            {
                throw new InvalidOperationException("Personal Cloud Library Source local manifest path is empty.");
            }

            if (!File.Exists(pluginSettings.LocalManifestPath))
            {
                throw new FileNotFoundException("Personal Cloud Library Source manifest was not found.", pluginSettings.LocalManifestPath);
            }

            return File.ReadAllText(pluginSettings.LocalManifestPath);
        }

        public ManifestValidationSummary ValidateManifest(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            var json = LoadManifestJson(pluginSettings);
            var manifest = ParseManifest(json);
            var summary = new ManifestValidationSummary();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in manifest.Items)
            {
                if (item == null)
                {
                    summary.Warnings++;
                    continue;
                }

                summary.ItemsFound++;

                if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Title))
                {
                    summary.Warnings++;
                    continue;
                }

                if (!ids.Add(item.Id))
                {
                    summary.Warnings++;
                }

                var launchPath = ResolveLaunchPath(item, pluginSettings);
                var launchFileExists = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
                if (launchFileExists)
                {
                    summary.CachedInstalled++;
                    continue;
                }

                var sourcePath = GetItemSourcePath(item);
                if (HasRcloneContentRootPathDoublingRisk(pluginSettings, sourcePath))
                {
                    summary.Warnings++;
                }

                if (pluginSettings.AllowDownloads &&
                    !string.IsNullOrWhiteSpace(sourcePath) &&
                    CanResolveSourcePath(pluginSettings, sourcePath))
                {
                    summary.DownloadEligible++;
                }
            }

            return summary;
        }

        private static PersonalCloudLibraryManifest ParseManifest(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Personal Cloud Library Source manifest JSON was empty.");
            }

            json = json.TrimStart('\uFEFF', '\u00EF', '\u00BB', '\u00BF');
            var manifest = Serialization.FromJson<PersonalCloudLibraryManifest>(json);
            if (manifest == null || manifest.Items == null)
            {
                throw new InvalidOperationException("Personal Cloud Library Source manifest was empty or invalid.");
            }

            return manifest;
        }

        private static List<GameMetadata> ConvertManifestItemsToGameMetadata(
            PersonalCloudLibraryManifest manifest,
            PersonalCloudLibrarySourceSettings pluginSettings,
            List<string> diagnostics)
        {
            var importedGames = new List<GameMetadata>();
            var importedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in manifest.Items)
            {
                if (item == null)
                {
                    diagnostics.Add("item=<null>; skipReason=null item");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Title))
                {
                    logger.Warn("Skipped manifest item because it was missing an id or title.");
                    diagnostics.Add($"itemId={item.Id}; title={item.Title}; skipReason=missing id or title");
                    continue;
                }

                if (!importedIds.Add(item.Id))
                {
                    logger.Warn($"Skipped duplicate manifest item id: {item.Id}");
                    diagnostics.Add($"itemId={item.Id}; title={item.Title}; skipReason=duplicate id");
                    continue;
                }

                var launchPath = ResolveLaunchPath(item, pluginSettings);
                var installDirectory = ResolveInstallDirectory(item, pluginSettings, launchPath);
                var launchFileExists = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
                var isInstalled = pluginSettings.TreatMissingFilesAsUninstalled ? launchFileExists : true;
                var sourcePath = GetItemSourcePath(item);
                var cachePath = ResolveDownloadDestinationFilePath(item, pluginSettings, launchPath);
                var downloadEligible = !launchFileExists &&
                    pluginSettings.AllowDownloads &&
                    !string.IsNullOrWhiteSpace(sourcePath) &&
                    CanResolveSourcePath(pluginSettings, sourcePath);
                var playActionCount = launchFileExists ? 1 : 0;
                var playActionName = launchFileExists ? "Play" : string.Empty;
                var playActionPath = launchFileExists ? launchPath : string.Empty;

                var game = new GameMetadata
                {
                    GameId = item.Id,
                    Name = item.Title,
                    IsInstalled = isInstalled,
                    InstallDirectory = installDirectory
                };

                logger.Info(
                    $"Personal Cloud Library Source item import: id={item.Id}; title={item.Title}; launchPath={launchPath}; fileExists={launchFileExists}; sourcePathPresent={!string.IsNullOrWhiteSpace(sourcePath)}; isInstalled={isInstalled}; downloadEligible={downloadEligible}; playActionCount={playActionCount}; playActionName={playActionName}; playActionPath={playActionPath}");
                diagnostics.Add(
                    $"itemId={item.Id}; title={item.Title}; sourcePath={sourcePath}; cachePath={cachePath}; localExists={launchFileExists}; isInstalled={isInstalled}; downloadEligible={downloadEligible}; playActionCount={playActionCount}; playActionName={playActionName}; playActionPath={playActionPath}; skipReason=");

                if (!string.IsNullOrWhiteSpace(item.Notes))
                {
                    game.Description = item.Notes;
                }

                if (launchFileExists)
                {
                    game.GameActions = new List<GameAction>
                    {
                        new GameAction
                        {
                            Name = "Play",
                            Type = GameActionType.File,
                            Path = launchPath,
                            WorkingDir = installDirectory,
                            IsPlayAction = true
                        }
                    };
                }

                importedGames.Add(game);
            }

            return importedGames;
        }

        public static string ResolveLaunchPath(PersonalCloudLibraryItem item, PersonalCloudLibrarySourceSettings pluginSettings)
        {
            if (!string.IsNullOrWhiteSpace(item.CachePath))
            {
                if (Path.IsPathRooted(item.CachePath))
                {
                    return item.CachePath;
                }

                if (!string.IsNullOrWhiteSpace(pluginSettings.LocalCacheFolder))
                {
                    return Path.Combine(pluginSettings.LocalCacheFolder, item.CachePath);
                }

                return item.CachePath;
            }

            if (!string.IsNullOrWhiteSpace(item.LocalPath))
            {
                if (Path.IsPathRooted(item.LocalPath))
                {
                    return item.LocalPath;
                }

                if (!string.IsNullOrWhiteSpace(pluginSettings.LocalCacheFolder))
                {
                    return Path.Combine(pluginSettings.LocalCacheFolder, item.LocalPath);
                }

                return item.LocalPath;
            }

            if (!string.IsNullOrWhiteSpace(item.InstallDirectory) &&
                !string.IsNullOrWhiteSpace(item.LaunchFile))
            {
                if (Path.IsPathRooted(item.InstallDirectory))
                {
                    return Path.Combine(item.InstallDirectory, item.LaunchFile);
                }

                if (!string.IsNullOrWhiteSpace(pluginSettings.LocalCacheFolder))
                {
                    return Path.Combine(pluginSettings.LocalCacheFolder, item.InstallDirectory, item.LaunchFile);
                }

                return Path.Combine(item.InstallDirectory, item.LaunchFile);
            }

            return string.Empty;
        }

        public static string GetItemSourceType(PersonalCloudLibraryItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.SourceType))
            {
                return "file";
            }

            return item.SourceType.Trim().Equals("directory", StringComparison.OrdinalIgnoreCase)
                ? "directory"
                : "file";
        }

        public static bool IsDirectoryItem(PersonalCloudLibraryItem item)
        {
            return string.Equals(GetItemSourceType(item), "directory", StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveInstallDirectory(
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings pluginSettings,
            string launchPath)
        {
            if (!string.IsNullOrWhiteSpace(item.InstallDirectory))
            {
                if (Path.IsPathRooted(item.InstallDirectory))
                {
                    return item.InstallDirectory;
                }

                if (!string.IsNullOrWhiteSpace(pluginSettings.LocalCacheFolder))
                {
                    return Path.Combine(pluginSettings.LocalCacheFolder, item.InstallDirectory);
                }

                return item.InstallDirectory;
            }

            if (!string.IsNullOrWhiteSpace(launchPath))
            {
                return Path.GetDirectoryName(launchPath);
            }

            if (!string.IsNullOrWhiteSpace(pluginSettings.LocalCacheFolder))
            {
                return pluginSettings.LocalCacheFolder;
            }

            return string.Empty;
        }

        public static string ResolveDownloadDestinationFilePath(
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings pluginSettings,
            string launchPath)
        {
            if (!string.IsNullOrWhiteSpace(item.CachePath))
            {
                if (Path.IsPathRooted(item.CachePath))
                {
                    return item.CachePath;
                }

                if (!string.IsNullOrWhiteSpace(pluginSettings.LocalCacheFolder))
                {
                    return Path.Combine(pluginSettings.LocalCacheFolder, item.CachePath);
                }

                return item.CachePath;
            }

            var installDirectory = ResolveInstallDirectory(item, pluginSettings, launchPath);

            if (!string.IsNullOrWhiteSpace(installDirectory) &&
                !string.IsNullOrWhiteSpace(item.LaunchFile))
            {
                return Path.Combine(installDirectory, item.LaunchFile);
            }

            if (!string.IsNullOrWhiteSpace(launchPath))
            {
                return launchPath;
            }

            var remoteFileName = Path.GetFileName((GetItemSourcePath(item) ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(remoteFileName))
            {
                remoteFileName = item.Id;
            }

            if (!string.IsNullOrWhiteSpace(pluginSettings.LocalCacheFolder) &&
                !string.IsNullOrWhiteSpace(item.Id))
            {
                return Path.Combine(pluginSettings.LocalCacheFolder, item.Id, remoteFileName);
            }

            return string.Empty;
        }

        public static string ResolveDownloadDestinationFolder(
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings pluginSettings,
            string launchPath)
        {
            if (!string.IsNullOrWhiteSpace(item.InstallDirectory))
            {
                if (Path.IsPathRooted(item.InstallDirectory))
                {
                    return item.InstallDirectory;
                }

                if (!string.IsNullOrWhiteSpace(pluginSettings.LocalCacheFolder))
                {
                    return Path.Combine(pluginSettings.LocalCacheFolder, item.InstallDirectory);
                }
            }

            if (!string.IsNullOrWhiteSpace(launchPath))
            {
                var launchDirectory = Path.GetDirectoryName(launchPath);
                if (!string.IsNullOrWhiteSpace(launchDirectory))
                {
                    return launchDirectory;
                }
            }

            if (!string.IsNullOrWhiteSpace(pluginSettings.LocalCacheFolder) &&
                !string.IsNullOrWhiteSpace(item.Id))
            {
                return Path.Combine(pluginSettings.LocalCacheFolder, item.Id);
            }

            return string.Empty;
        }

        public static string ResolveUninstallTargetPath(
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings pluginSettings,
            string launchPath,
            string installDirectory)
        {
            var behavior = string.IsNullOrWhiteSpace(pluginSettings.UninstallBehavior)
                ? PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior
                : pluginSettings.UninstallBehavior;

            if (string.Equals(behavior, PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior, StringComparison.OrdinalIgnoreCase))
            {
                return launchPath;
            }

            if (string.Equals(behavior, PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior, StringComparison.OrdinalIgnoreCase))
            {
                return installDirectory;
            }

            if (string.Equals(behavior, PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior, StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(installDirectory) ? installDirectory : launchPath;
            }

            return string.Empty;
        }

        public static bool IsPathInsideLocalCache(PersonalCloudLibrarySourceSettings pluginSettings, string candidatePath)
        {
            if (pluginSettings == null)
            {
                return false;
            }

            return IsPathInsideCacheFolder(candidatePath, pluginSettings.LocalCacheFolder);
        }

        public static bool IsPathInsideCacheFolder(string candidatePath, string localCacheFolder)
        {
            if (string.IsNullOrWhiteSpace(localCacheFolder) ||
                string.IsNullOrWhiteSpace(candidatePath))
            {
                return false;
            }

            var cacheRoot = NormalizeFullPath(localCacheFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = NormalizeFullPath(candidatePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return candidate.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveSafeUninstallTarget(
            PersonalCloudLibrarySourceSettings pluginSettings,
            string targetPath,
            out string refusalReason)
        {
            refusalReason = string.Empty;

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                refusalReason = "uninstall target path is empty";
                return string.Empty;
            }

            string normalizedTarget;
            try
            {
                normalizedTarget = NormalizeFullPath(targetPath);
            }
            catch (Exception ex)
            {
                refusalReason = "uninstall target path could not be normalized: " + ex.Message;
                return string.Empty;
            }

            if (IsDriveRoot(normalizedTarget))
            {
                refusalReason = "uninstall target is a drive root";
                return string.Empty;
            }

            var normalizedCache = string.Empty;
            if (pluginSettings != null && !string.IsNullOrWhiteSpace(pluginSettings.LocalCacheFolder))
            {
                try
                {
                    normalizedCache = NormalizeFullPath(pluginSettings.LocalCacheFolder)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                catch (Exception ex)
                {
                    refusalReason = "LocalCacheFolder could not be normalized: " + ex.Message;
                    return string.Empty;
                }
            }

            if (!string.IsNullOrWhiteSpace(normalizedCache) &&
                string.Equals(
                    normalizedTarget.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    normalizedCache,
                    StringComparison.OrdinalIgnoreCase))
            {
                refusalReason = "uninstall target is LocalCacheFolder itself";
                return string.Empty;
            }

            var insideCache = IsPathInsideCacheFolder(normalizedTarget, pluginSettings?.LocalCacheFolder);
            if (pluginSettings == null || (!pluginSettings.AllowUninstallOutsideCacheFolder && !insideCache))
            {
                refusalReason = "uninstall target is outside LocalCacheFolder";
                return string.Empty;
            }

            return normalizedTarget;
        }

        public static string NormalizeFullPath(string path)
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsDriveRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var root = Path.GetPathRoot(path);
            return !string.IsNullOrWhiteSpace(root) &&
                string.Equals(
                    path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
        }

        public static string GetItemSourcePath(PersonalCloudLibraryItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.SourcePath))
            {
                return item.SourcePath;
            }

            return item.RemotePath;
        }

        public static string ResolveLocalFolderSourcePath(
            PersonalCloudLibrarySourceSettings pluginSettings,
            string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(sourcePath))
            {
                return sourcePath;
            }

            if (!string.IsNullOrWhiteSpace(pluginSettings.LocalLibraryRoot))
            {
                return Path.Combine(pluginSettings.LocalLibraryRoot, sourcePath);
            }

            if (!string.IsNullOrWhiteSpace(pluginSettings.LocalManifestPath))
            {
                var manifestFolder = Path.GetDirectoryName(pluginSettings.LocalManifestPath);
                if (!string.IsNullOrWhiteSpace(manifestFolder))
                {
                    return Path.Combine(manifestFolder, sourcePath);
                }
            }

            return string.Empty;
        }

        public static string ResolveRcloneSourcePath(
            PersonalCloudLibrarySourceSettings pluginSettings,
            string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(pluginSettings.RcloneContentRoot))
            {
                return sourcePath;
            }

            return CombineRemotePath(pluginSettings.RcloneContentRoot, sourcePath);
        }

        public static bool HasRcloneContentRootPathDoublingRisk(
            PersonalCloudLibrarySourceSettings pluginSettings,
            string sourcePath)
        {
            if (pluginSettings == null ||
                string.IsNullOrWhiteSpace(sourcePath) ||
                string.IsNullOrWhiteSpace(pluginSettings.RcloneContentRoot))
            {
                return false;
            }

            if (!string.Equals(
                GetProviderType(pluginSettings),
                PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var normalizedRoot = NormalizeRemotePath(pluginSettings.RcloneContentRoot);
            var normalizedSource = NormalizeRemotePath(sourcePath);

            return !string.IsNullOrWhiteSpace(normalizedRoot) &&
                normalizedSource.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeRemotePath(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .Replace('\\', '/')
                .Trim('/');
        }

        private static bool CanResolveSourcePath(PersonalCloudLibrarySourceSettings pluginSettings, string sourcePath)
        {
            var providerType = GetProviderType(pluginSettings);

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(pluginSettings.RcloneRemoteName) &&
                    !string.IsNullOrWhiteSpace(ResolveRcloneSourcePath(pluginSettings, sourcePath));
            }

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFileProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(ResolveLocalFolderSourcePath(pluginSettings, sourcePath));
            }

            return false;
        }

        private static string ResolveLocalFolderManifestPath(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            if (!string.IsNullOrWhiteSpace(pluginSettings.LocalManifestPath))
            {
                return pluginSettings.LocalManifestPath;
            }

            if (string.IsNullOrWhiteSpace(pluginSettings.ManifestRelativePath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(pluginSettings.ManifestRelativePath))
            {
                return pluginSettings.ManifestRelativePath;
            }

            if (string.IsNullOrWhiteSpace(pluginSettings.LocalLibraryRoot))
            {
                return string.Empty;
            }

            return Path.Combine(pluginSettings.LocalLibraryRoot, pluginSettings.ManifestRelativePath);
        }

        private static string CombineRemotePath(string root, string relativePath)
        {
            return root.TrimEnd('/', '\\') + "/" + relativePath.TrimStart('/', '\\');
        }

        private static string ResolveManifestDescription(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            var providerType = GetProviderType(pluginSettings);

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return $"{pluginSettings.RcloneRemoteName}:{pluginSettings.RcloneManifestPath}";
            }

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return ResolveLocalFolderManifestPath(pluginSettings);
            }

            return pluginSettings.LocalManifestPath;
        }

        private void WriteImportDiagnostics(PersonalCloudLibrarySourceSettings pluginSettings, List<string> diagnostics)
        {
            if (pluginSettings == null || !pluginSettings.EnableDiagnostics)
            {
                return;
            }

            try
            {
                var diagnosticsDirectory = ResolveDiagnosticsDirectory();
                Directory.CreateDirectory(diagnosticsDirectory);
                var diagnosticsPath = Path.Combine(diagnosticsDirectory, "last-import-diagnostics.txt");
                File.WriteAllLines(diagnosticsPath, diagnostics);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Personal Cloud Library Source could not write import diagnostics.");
            }
        }

        public string GetDiagnosticsDirectory()
        {
            return ResolveDiagnosticsDirectory();
        }

        public string GetGeneratedManifestDirectory()
        {
            return Path.Combine(GetPluginDataDirectory(), "manifests");
        }

        public string GetDefaultGeneratedManifestPath()
        {
            return Path.Combine(GetGeneratedManifestDirectory(), "personal-cloud-library.generated.json");
        }

        public string GetDefaultGeneratedReportPath()
        {
            return Path.Combine(GetGeneratedManifestDirectory(), "personal-cloud-library.generated.report.txt");
        }

        public string GetDefaultLocalCacheFolder()
        {
            return Path.Combine(GetPluginDataDirectory(), "cache");
        }

        public string DescribeManifestPath(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            return ResolveManifestDescription(pluginSettings);
        }

        public ManifestGenerationReport GenerateManifestFromFolder(string sourceRoot)
        {
            var outputPath = GetDefaultGeneratedManifestPath();
            var reportPath = GetDefaultGeneratedReportPath();

            return manifestGenerationService.Generate(new ManifestGenerationOptions
            {
                SourceRoot = sourceRoot,
                OutputPath = outputPath,
                ReportPath = reportPath
            });
        }

        public string GetPluginDataDirectory()
        {
            try
            {
                var pluginUserDataPath = GetPluginUserDataPath();
                if (!string.IsNullOrWhiteSpace(pluginUserDataPath))
                {
                    return pluginUserDataPath;
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Personal Cloud Library Source could not resolve the Playnite plugin user data path.");
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PersonalCloudLibrarySource");
        }

        private string ResolveDiagnosticsDirectory()
        {
            try
            {
                return Path.Combine(GetPluginDataDirectory(), "diagnostics");
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Personal Cloud Library Source could not resolve the Playnite plugin user data path.");
            }

            var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localApplicationData, "PersonalCloudLibrarySource", "diagnostics");
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new PersonalCloudLibrarySourceSettingsView
            {
                DataContext = settings
            };
        }
    }

    public class PersonalCloudLibraryManifest
    {
        public int Version { get; set; }
        public string GeneratedBy { get; set; }
        public string GeneratedAt { get; set; }
        public string SourceMode { get; set; }
        public int ItemCount { get; set; }
        public List<PersonalCloudLibraryItem> Items { get; set; } = new List<PersonalCloudLibraryItem>();
    }

    public class ManifestValidationSummary
    {
        public int ItemsFound { get; set; }
        public int DownloadEligible { get; set; }
        public int CachedInstalled { get; set; }
        public int Warnings { get; set; }
    }

    public class PersonalCloudLibraryItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Platform { get; set; }
        public string LocalPath { get; set; }
        public string InstallDirectory { get; set; }
        public string LaunchFile { get; set; }
        public string SourcePath { get; set; }
        public string CachePath { get; set; }
        public string RemotePath { get; set; }
        public string SourceType { get; set; }
        public string PackageRole { get; set; }
        public string Notes { get; set; }
    }
}
