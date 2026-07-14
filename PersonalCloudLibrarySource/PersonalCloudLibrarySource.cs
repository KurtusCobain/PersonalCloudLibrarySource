using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource : LibraryPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly RcloneManifestReader rcloneManifestReader = new RcloneManifestReader();
        private readonly RcloneFileCopier rcloneFileCopier = new RcloneFileCopier();
        private readonly LocalFileCopier localFileCopier = new LocalFileCopier();
        private readonly ManifestGenerationService manifestGenerationService = new ManifestGenerationService();
        private readonly LibraryVerificationService libraryVerificationService = new LibraryVerificationService();
        private readonly SafeFileWriteService safeFileWriteService = new SafeFileWriteService();
        private readonly ManifestLoader manifestLoader = new ManifestLoader();
        private readonly ImportOutcomeService importOutcomeService = new ImportOutcomeService();
        private readonly ImportNotificationService importNotificationService;
        private readonly object validatedManifestItemsSync = new object();
        private IReadOnlyDictionary<string, PersonalCloudLibraryItem> validatedManifestItems =
            new Dictionary<string, PersonalCloudLibraryItem>(StringComparer.OrdinalIgnoreCase);
        private readonly IPlayniteAPI playniteApi;

        private PersonalCloudLibrarySourceSettingsV3ViewModel settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("61993828-67a8-4468-93a2-293442e36328");

        public override string Name => ResolveLibraryDisplayName(settings?.Settings);

        public override LibraryClient Client { get; }

        public PersonalCloudLibrarySource(IPlayniteAPI api) : base(api)
        {
            playniteApi = api;
            importNotificationService = new ImportNotificationService(
                new PlayniteImportNotificationSink(api.Notifications),
                new PlayniteImportUiDispatcher(api),
                ResourceProvider.GetString,
                () => playniteApi.MainView.OpenPluginSettings(Id));
            settings = new PersonalCloudLibrarySourceSettingsV3ViewModel(this);
            InitializeDashboardNavigation();
            Client = new PersonalCloudLibrarySourceClient(navigationService.OpenDashboard);

            Properties = new LibraryPluginProperties
            {
                HasSettings = true
            };
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var pluginSettings = settings.GetRuntimeSettingsSnapshot();
            if (pluginSettings == null || !pluginSettings.Enabled)
            {
                logger.Info("Personal Cloud Library Source is disabled. No games imported.");
                return new List<GameMetadata>();
            }

            var outcome = importOutcomeService.Import(pluginSettings, rcloneManifestReader.ReadManifestJson);
            PublishImportOutcome(outcome, pluginSettings);
            if (!outcome.Succeeded)
            {
                logger.Error("Personal Cloud Library Source failed to import the manifest: " + outcome.Error);
            }
            else
            {
                logger.Info($"Personal Cloud Library Source imported {outcome.Games.Count} manifest entries.");
            }

            return ImportExecutionPolicy.Complete(outcome);
        }

        private void PublishImportOutcome(
            ImportOutcome outcome,
            PersonalCloudLibrarySourceSettings pluginSettings)
        {
            var snapshot = outcome.Succeeded
                ? outcome.ValidItems
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                    .ToDictionary(item => item.Id, item => item, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, PersonalCloudLibraryItem>(StringComparer.OrdinalIgnoreCase);
            lock (validatedManifestItemsSync)
            {
                validatedManifestItems = snapshot;
            }

            var diagnostics = new List<string>
            {
                "provider=" + GetProviderType(pluginSettings),
                "libraryDisplayName=" + ResolveLibraryDisplayName(pluginSettings),
                "manifestPath=" + ResolveManifestDescription(pluginSettings),
                "importSucceeded=" + outcome.Succeeded
            };
            diagnostics.AddRange(outcome.Diagnostics);
            var diagnosticsPath = WriteImportDiagnostics(pluginSettings, diagnostics, !outcome.Succeeded);
            VerificationDashboardStateService.LatestReport =
                ImportDashboardStateService.CreateReport(outcome, diagnosticsPath);

            try
            {
                if (outcome.Succeeded)
                {
                    importNotificationService.Clear();
                }
                else
                {
                    importNotificationService.ShowFailure(outcome);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Personal Cloud Library Source could not update its import notification.");
            }

            RefreshDashboardStateOnUiThread();
        }

        private void RefreshDashboardStateOnUiThread()
        {
            if (playniteApi?.MainView?.UIDispatcher == null)
            {
                RefreshDashboardState();
                return;
            }

            playniteApi.MainView.UIDispatcher.BeginInvoke(new Action(RefreshDashboardState));
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            var installActions = new List<InstallController>();

            try
            {
                var pluginSettings = settings.GetRuntimeSettingsSnapshot();
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

                var manifest = LoadValidatedManifest(pluginSettings);

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
                    var itemState = new LibraryItemStateResolver().Resolve(
                        item,
                        launchPath,
                        installDirectory,
                        pluginSettings.TreatMissingFilesAsUninstalled);
                    if (itemState.IsCached)
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
                        playniteApi,
                        args.Game,
                        item,
                        pluginSettings,
                        rcloneFileCopier,
                        localFileCopier,
                        GetTransferManager(),
                        GetTransferExecutor()));
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
                var pluginSettings = settings.GetRuntimeSettingsSnapshot();
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

                var manifest = LoadValidatedManifest(pluginSettings);

                foreach (var item in manifest.Items)
                {
                    if (item == null ||
                        !string.Equals(item.Id, args.Game.GameId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var launchPath = ResolveLaunchPath(item, pluginSettings);
                    var installDirectory = ResolveInstallDirectory(item, pluginSettings, launchPath);
                    var itemState = new LibraryItemStateResolver().Resolve(
                        item,
                        launchPath,
                        installDirectory,
                        pluginSettings.TreatMissingFilesAsUninstalled);
                    var launchExists = itemState.HasPlayAction;
                    var installDirectoryExists = itemState.IsCached && !launchExists;
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
            var result = manifestLoader.Load(pluginSettings, rcloneManifestReader.ReadManifestJson);
            if (!result.Succeeded) throw new InvalidOperationException(result.Error);
            return result.Json;
        }

        private PersonalCloudLibraryManifest LoadValidatedManifest(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            return LoadParsedManifest(pluginSettings).CreateValidatedManifest();
        }

        private ManifestParseResult LoadParsedManifest(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            try
            {
                var result = new ManifestParserValidator().Parse(LoadManifestJson(pluginSettings));
                if (!result.Succeeded) throw new InvalidOperationException(result.Error);
                var snapshot = result.ValidItems
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                    .ToDictionary(item => item.Id, item => item, StringComparer.OrdinalIgnoreCase);
                lock (validatedManifestItemsSync)
                {
                    validatedManifestItems = snapshot;
                }
                return result;
            }
            catch
            {
                lock (validatedManifestItemsSync)
                {
                    validatedManifestItems = new Dictionary<string, PersonalCloudLibraryItem>(StringComparer.OrdinalIgnoreCase);
                }
                throw;
            }
        }

        private IReadOnlyDictionary<string, PersonalCloudLibraryItem> GetValidatedManifestItemsSnapshot()
        {
            lock (validatedManifestItemsSync)
            {
                return validatedManifestItems.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        public ManifestValidationSummary ValidateManifest(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            var parseResult = LoadParsedManifest(pluginSettings);
            var summary = new ManifestValidationSummary();
            summary.ItemsFound = parseResult.Manifest.Items.Count;
            summary.Warnings = parseResult.Issues.Count;

            foreach (var item in parseResult.ValidItems)
            {
                var launchPath = ResolveLaunchPath(item, pluginSettings);
                var installDirectory = ResolveInstallDirectory(item, pluginSettings, launchPath);
                var itemState = new LibraryItemStateResolver().Resolve(
                    item,
                    launchPath,
                    installDirectory,
                    pluginSettings.TreatMissingFilesAsUninstalled);
                if (itemState.IsCached)
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
            var result = new ManifestParserValidator().Parse(json);
            if (!result.Succeeded) throw new InvalidOperationException(result.Error);
            return result.CreateValidatedManifest();
        }

        private static List<GameMetadata> ConvertManifestItemsToGameMetadata(
            PersonalCloudLibraryManifest manifest,
            PersonalCloudLibrarySourceSettings pluginSettings,
            List<string> diagnostics)
        {
            return new ManifestItemMapper().Map(manifest.Items, pluginSettings, diagnostics);
        }

        public static string ResolveLaunchPath(PersonalCloudLibraryItem item, PersonalCloudLibrarySourceSettings pluginSettings)
        {
            return new CachePathResolver().Resolve(item, pluginSettings).LaunchPath;
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
            return new CachePathResolver().Resolve(item, pluginSettings).InstallDirectory;
        }

        public static string ResolveDownloadDestinationFilePath(
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings pluginSettings,
            string launchPath)
        {
            return new CachePathResolver().Resolve(item, pluginSettings).DestinationFile;
        }

        public static string ResolveDownloadDestinationFolder(
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings pluginSettings,
            string launchPath)
        {
            return new CachePathResolver().Resolve(item, pluginSettings).DestinationDirectory;
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
            return PathBoundary.IsContained(localCacheFolder, candidatePath) &&
                !string.Equals(PathBoundary.Normalize(localCacheFolder), PathBoundary.Normalize(candidatePath), StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveSafeUninstallTarget(
            PersonalCloudLibrarySourceSettings pluginSettings,
            string targetPath,
            out string refusalReason)
        {
            var result = new SafeCacheDeletionPolicy().Authorize(
                pluginSettings?.LocalCacheFolder,
                targetPath,
                pluginSettings?.AllowUninstallOutsideCacheFolder == true);
            refusalReason = result.Reason;
            return result.Allowed ? result.TargetPath : string.Empty;
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
            var result = new SourcePathResolver().ResolveLocal(pluginSettings, sourcePath);
            return result.Succeeded ? result.Path : string.Empty;
        }

        public static string ResolveRcloneSourcePath(
            PersonalCloudLibrarySourceSettings pluginSettings,
            string sourcePath)
        {
            return new SourcePathResolver().ResolveRclone(pluginSettings, sourcePath);
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

        public static bool CanResolveSourcePath(PersonalCloudLibrarySourceSettings pluginSettings, string sourcePath)
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

        private string ResolveManifestDescription(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            var providerType = GetProviderType(pluginSettings);

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return $"{pluginSettings.RcloneRemoteName}:{pluginSettings.RcloneManifestPath}";
            }

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
            {
                var resolution = manifestLoader.ResolveLocalManifestPath(pluginSettings);
                return resolution.Succeeded ? resolution.Path : string.Empty;
            }

            return pluginSettings.LocalManifestPath;
        }

        private string WriteImportDiagnostics(
            PersonalCloudLibrarySourceSettings pluginSettings,
            IReadOnlyList<string> diagnostics,
            bool force)
        {
            if (pluginSettings == null || (!force && !pluginSettings.EnableDiagnostics))
            {
                return string.Empty;
            }

            try
            {
                var diagnosticsDirectory = ResolveDiagnosticsDirectory();
                var diagnosticsPath = Path.Combine(diagnosticsDirectory, "last-import-diagnostics.txt");
                safeFileWriteService.WriteAllLines(diagnosticsPath, diagnostics);
                return diagnosticsPath;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Personal Cloud Library Source could not write import diagnostics.");
                return string.Empty;
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

        public string GetReportsDirectory()
        {
            return Path.Combine(GetPluginDataDirectory(), "reports");
        }

        public string GetBackupsDirectory()
        {
            return Path.Combine(GetPluginDataDirectory(), "backups");
        }

        public string GetLatestVerificationReportPath()
        {
            return Path.Combine(GetReportsDirectory(), "latest-verification-report.txt");
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
                ReportPath = reportPath,
                BackupDirectory = GetBackupsDirectory()
            });
        }

        public LibraryVerificationReport GenerateVerificationReport(
            PersonalCloudLibrarySourceSettings pluginSettings,
            IEnumerable<string> configurationErrors = null)
        {
            var reportPath = GetLatestVerificationReportPath();
            PersonalCloudLibraryManifest manifest = null;
            Exception manifestLoadException = null;

            try
            {
                manifest = LoadParsedManifest(pluginSettings).Manifest;
            }
            catch (Exception ex)
            {
                manifestLoadException = ex;
            }

            var report = libraryVerificationService.BuildReport(
                pluginSettings,
                DescribeManifestPath(pluginSettings),
                reportPath,
                manifest,
                manifestLoadException,
                configurationErrors,
                playniteApi?.Database?.Games,
                Id);

            safeFileWriteService.WriteAllLines(
                reportPath,
                libraryVerificationService.BuildReportLines(report),
                GetBackupsDirectory(),
                createBackup: true);

            return report;
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
