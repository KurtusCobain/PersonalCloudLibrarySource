using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource
    {
        private readonly LibraryStatusService dashboardLibraryStatusService = new LibraryStatusService();
        private readonly DashboardActivityService dashboardActivityService = new DashboardActivityService();
        private DashboardStateStore dashboardStateStore;
        private CloudLibraryDashboardWindowService dashboardWindowService;
        private SetupWizardWindowService setupWizardWindowService;
        private PluginNavigationService navigationService;
        private readonly SetupStateService setupStateService = new SetupStateService(new SetupLaunchPolicyService());
        private IStartupUiDispatcher startupUiDispatcher;
        private StartupNotificationService startupNotificationService;
        private StartupActionService startupActionService;
        private TopPanelItem dashboardTopPanelItem;
        private CloudLibrarySidebarItem dashboardSidebarItem;

        private void InitializeDashboardNavigation()
        {
            dashboardStateStore = new DashboardStateStore();
            dashboardWindowService = new CloudLibraryDashboardWindowService(playniteApi, CreateDashboardView);
            setupWizardWindowService = new SetupWizardWindowService(
                playniteApi,
                settings,
                GetDefaultLocalCacheFolder,
                PrepareSetupWizardCompletion,
                SetupWizardSaved,
                settings.PersistSetupDismissed);
            navigationService = new PluginNavigationService(
                dashboardWindowService.OpenDashboard,
                () => playniteApi.MainView.OpenPluginSettings(Id),
                VerifyLibraryFromDashboard,
                settings.OpenCacheFolder,
                settings.OpenLatestVerificationReport,
                GenerateManifestFromDashboard,
                settings.ShowUpdateLibraryInstructions,
                OpenSourceLocationFromDashboard,
                setupWizardWindowService.OpenWizard);

            var notificationDispatcher = new PlayniteImportUiDispatcher(playniteApi);
            startupUiDispatcher = new StartupUiDispatcher(
                new PlayniteStartupUiPostTarget(playniteApi),
                ObserveStartupUiException);
            startupNotificationService = new StartupNotificationService(
                new PlayniteImportNotificationSink(playniteApi.Notifications),
                notificationDispatcher,
                ResourceProvider.GetString,
                setupWizardWindowService.OpenWizard,
                () => playniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop);
            startupActionService = new StartupActionService(
                new DelegatingStartupActionSink(
                    setupWizardWindowService.OpenWizard,
                    startupNotificationService.ShowSetupReminder,
                    GenerateStartupManifest,
                    RefreshStartupStatus,
                    dashboardWindowService.OpenDashboard,
                    ReportStartupFailure,
                    startupUiDispatcher),
                ObserveStartupBackgroundException);

            settings.Settings.PropertyChanged += DashboardSettings_PropertyChanged;
            settings.SettingsCommitted += Settings_SettingsCommitted;
            RefreshDashboardState();
        }

        private CloudLibraryDashboardView CreateDashboardView()
        {
            RefreshDashboardState();
            var view = new CloudLibraryDashboardView();
            view.SetViewModelFactory(CreateDashboardViewModel);
            return view;
        }

        private CloudLibraryDashboardViewModel CreateDashboardViewModel()
        {
            var queue = GetTransferQueue();
            return new CloudLibraryDashboardViewModel(
                dashboardStateStore,
                navigationService,
                GetTransferManager(),
                dashboardActivityService,
                new DashboardTransferActions(queue, settings.GetRuntimeSettingsSnapshot),
                startupUiDispatcher);
        }

        private void RefreshDashboardState()
        {
            if (dashboardStateStore == null)
            {
                return;
            }

            var pluginSettings = settings?.Settings;
            var pluginGames = playniteApi?.Database?.Games?.
                Where(game => game.PluginId == Id)
                .ToList() ?? new List<Playnite.SDK.Models.Game>();

            var importedCount = pluginGames.Count;
            var stateResolver = new LibraryItemStateResolver();
            var manifestItems = GetValidatedManifestItemsSnapshot();
            var treatMissingAsUninstalled = pluginSettings?.TreatMissingFilesAsUninstalled ?? true;
            var cachedCount = pluginGames.Count(game =>
            {
                PersonalCloudLibraryItem item;
                if (!string.IsNullOrWhiteSpace(game.GameId) && manifestItems.TryGetValue(game.GameId, out item))
                {
                    var paths = new CachePathResolver().Resolve(item, pluginSettings);
                    return stateResolver.Resolve(item, paths.LaunchPath, paths.InstallDirectory, treatMissingAsUninstalled).IsCached;
                }
                return stateResolver.ResolveGame(game, treatMissingAsUninstalled).IsCached;
            });
            var manifestCount = pluginSettings == null
                ? importedCount
                : Math.Max(pluginSettings.LastManifestItemCount, importedCount);

            dashboardStateStore.Current = dashboardLibraryStatusService.BuildState(
                pluginSettings,
                new LibraryStatusContext
                {
                    SourceAvailable = IsConfiguredSourceAvailable(pluginSettings),
                    ManifestItemCount = manifestCount,
                    ImportedGameCount = importedCount,
                    CachedGameCount = cachedCount,
                    WarningCount = 0,
                    ActiveTransferCount = GetActiveTransferCount(),
                    FailedTransferCount = GetFailedTransferCount(),
                    SourceDescription = ResolveDashboardSourceDescription(pluginSettings),
                    ManifestDescription = DescribeManifestPath(pluginSettings),
                    CachePath = pluginSettings?.LocalCacheFolder ?? string.Empty
                });
        }

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            if (dashboardTopPanelItem == null)
            {
                dashboardTopPanelItem = new TopPanelItem
                {
                    Icon = CreateToolbarIcon(),
                    Title = BuildToolbarTitle(),
                    Visible = settings?.Settings?.ShowTopPanelButton ?? true,
                    Activated = navigationService.OpenDashboard
                };
            }

            yield return dashboardTopPanelItem;
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            if (playniteApi.ApplicationInfo.Mode != ApplicationMode.Desktop)
            {
                yield break;
            }

            if (dashboardSidebarItem == null)
            {
                dashboardSidebarItem = new CloudLibrarySidebarItem(CreateDashboardView, GetNavigationIconPath())
                {
                    Visible = settings?.Settings?.ShowSidebarDashboard ?? true
                };
            }

            yield return dashboardSidebarItem;
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            var menuSection = "@" + GetDashboardResource("LOCPLSMenuSection", "Personal Cloud Library");
            var iconPath = GetNavigationIconPath();

            yield return CreateMainMenuItem(menuSection, "LOCPLSOpenDashboard", "Open Dashboard", navigationService.OpenDashboard, iconPath);
            yield return CreateMainMenuItem(menuSection, "LOCPLSRunSetupWizard", "Run Setup Wizard", navigationService.RunSetupWizard, iconPath);
            yield return CreateMainMenuItem(menuSection, "LOCPLSUpdateLibraryHelp", "How to Update Library", navigationService.ShowUpdateLibraryInstructions, iconPath);

            if (string.Equals(
                GetProviderType(settings.Settings),
                PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                yield return CreateMainMenuItem(menuSection, "LOCPLSGenerateManifest", "Generate or Refresh Manifest", navigationService.GenerateManifest, iconPath);
            }

            yield return CreateMainMenuItem(menuSection, "LOCPLSVerifyLibrary", "Verify Library", navigationService.VerifyLibrary, iconPath);

            if (CanOpenSourceLocation(settings.Settings))
            {
                yield return CreateMainMenuItem(menuSection, "LOCPLSOpenSourceLocation", "Open Source Location", navigationService.OpenSourceLocation, iconPath);
            }

            yield return CreateMainMenuItem(menuSection, "LOCPLSOpenCacheFolder", "Open Cache Folder", navigationService.OpenCacheFolder, iconPath);
            yield return CreateMainMenuItem(menuSection, "LOCPLSOpenLatestReport", "Open Latest Report", navigationService.OpenLatestReport, iconPath);
            yield return CreateMainMenuItem(menuSection, "LOCPLSOpenSettings", "Settings", navigationService.OpenSettings, iconPath);
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            var snapshot = settings?.GetRuntimeSettingsSnapshot();
            if (snapshot == null)
            {
                return;
            }

            var setupValid = setupStateService.IsValid(snapshot);
            var setupAction = setupStateService.Evaluate(snapshot, setupValid);
            if (setupAction == SetupLaunchAction.OpenWizard &&
                playniteApi.ApplicationInfo.Mode != ApplicationMode.Desktop)
            {
                setupAction = snapshot.ShowSetupReminders
                    ? SetupLaunchAction.ShowReminder
                    : SetupLaunchAction.None;
            }

            startupActionService.Start(new StartupActionContext
            {
                PluginEnabled = snapshot.Enabled,
                SetupValid = setupValid,
                SetupAction = setupAction,
                GenerateManifest = snapshot.AutoGenerateManifestOnApplicationStart,
                ManifestGenerationEligible = IsStartupManifestGenerationEligible(snapshot),
                RefreshStatus = snapshot.AutoRefreshOnApplicationStart,
                OpenDashboard = snapshot.OpenDashboardAtStartup &&
                    playniteApi.ApplicationInfo.Mode == ApplicationMode.Desktop
            });
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            if (!startupActionService.Stop(TimeSpan.FromSeconds(10)))
            {
                logger.Warn("Personal Cloud Library Source startup work did not stop within the shutdown timeout.");
            }

            if (settings?.Settings != null)
            {
                settings.Settings.PropertyChanged -= DashboardSettings_PropertyChanged;
                settings.SettingsCommitted -= Settings_SettingsCommitted;
            }

            DisposeTransferManager();
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            RefreshDashboardState();
            UpdateNavigationItemState();
        }

        private void DashboardSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            SynchronizeTransferManagerSettings();
            RefreshDashboardState();
            UpdateNavigationItemState();
        }

        private void Settings_SettingsCommitted(object sender, EventArgs e)
        {
            SynchronizeTransferManagerSettings();
            RefreshDashboardState();
            UpdateNavigationItemState();
        }

        private void UpdateNavigationItemState()
        {
            if (dashboardTopPanelItem != null)
            {
                dashboardTopPanelItem.Visible = settings.Settings.ShowTopPanelButton;
                dashboardTopPanelItem.Title = BuildToolbarTitle();
            }

            if (dashboardSidebarItem != null)
            {
                dashboardSidebarItem.Visible = settings.Settings.ShowSidebarDashboard;
            }

            UpdateSidebarTransferProgress();
        }

        private string BuildToolbarTitle()
        {
            return GetDashboardResource("LOCPLSDashboardTitle", "Personal Cloud Library") + " — " +
                   (dashboardStateStore?.Current?.StatusText ?? "Needs setup");
        }

        private void VerifyLibraryFromDashboard()
        {
            settings.VerifySetup();
            RefreshDashboardState();
            UpdateNavigationItemState();
        }

        private void GenerateManifestFromDashboard()
        {
            settings.GenerateManifestFromFolder();
            RefreshDashboardState();
            UpdateNavigationItemState();
        }

        private void PrepareSetupWizardCompletion()
        {
            var pluginSettings = settings.Settings;
            if (string.Equals(
                    GetProviderType(pluginSettings),
                    PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                    StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(pluginSettings.LocalManifestPath))
            {
                var report = GenerateManifestFromFolder(pluginSettings.LocalLibraryRoot);
                pluginSettings.LocalManifestPath = report.OutputPath;
                pluginSettings.ManifestRelativePath = string.Empty;
                pluginSettings.LastGeneratedManifestPath = report.OutputPath;
                pluginSettings.LastGeneratedReportPath = report.ReportPath;
                pluginSettings.LastManifestGeneratedAt = report.Manifest.GeneratedAt;
                pluginSettings.LastManifestItemCount = report.ItemCount;
            }

            settings.MarkSetupCompleted();
        }

        private static bool IsStartupManifestGenerationEligible(PersonalCloudLibrarySourceSettingsV3 snapshot)
        {
            return snapshot != null &&
                   string.Equals(
                       GetProviderType(snapshot),
                       PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                       StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(snapshot.LocalLibraryRoot) &&
                   Directory.Exists(snapshot.LocalLibraryRoot);
        }

        private void GenerateStartupManifest(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = settings.GetRuntimeSettingsSnapshot();
            if (!IsStartupManifestGenerationEligible(snapshot))
            {
                return;
            }

            var report = GenerateManifestFromFolder(snapshot.LocalLibraryRoot, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            startupUiDispatcher.Post(
                () => settings.PersistGeneratedManifestState(report),
                cancellationToken);
        }

        private void RefreshStartupStatus()
        {
            RefreshDashboardState();
            UpdateNavigationItemState();
        }

        private void ReportStartupFailure(Exception exception)
        {
            logger.Error(exception, "Personal Cloud Library Source startup action failed.");
            try
            {
                startupNotificationService.ShowFailure(exception);
            }
            catch (Exception notificationException)
            {
                logger.Warn(notificationException, "Personal Cloud Library Source could not publish its startup failure notification.");
            }
        }

        private void ObserveStartupUiException(Exception exception)
        {
            logger.Error(exception, "Personal Cloud Library Source startup UI callback failed.");
            ReportStartupFailure(exception);
        }

        private void ObserveStartupBackgroundException(Exception exception)
        {
            logger.Error(exception, "Personal Cloud Library Source startup task fault was observed.");
        }

        private void SetupWizardSaved()
        {
            RefreshDashboardState();
            UpdateNavigationItemState();
            dashboardWindowService.OpenDashboard();
            playniteApi.Dialogs.ShowMessage(
                "Setup was saved successfully. Run Update Game Library in Playnite to import or refresh the catalog.",
                GetDashboardResource("LOCPLSSetupWizardTitle", "Personal Cloud Library Setup"));
        }

        private void OpenSourceLocationFromDashboard()
        {
            var pluginSettings = settings.Settings;
            var providerType = GetProviderType(pluginSettings);

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
            {
                if (!OpenExplorerFolder(pluginSettings.LocalLibraryRoot))
                {
                    ShowSourceUnavailableMessage();
                }
                return;
            }

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFileProviderType, StringComparison.OrdinalIgnoreCase))
            {
                if (!OpenExplorerFile(pluginSettings.LocalManifestPath))
                {
                    ShowSourceUnavailableMessage();
                }
                return;
            }

            playniteApi.Dialogs.ShowMessage(
                "This cloud source is accessed through rclone. Open the dashboard or plugin settings to review the configured remote and content root.",
                GetDashboardResource("LOCPLSDashboardTitle", "Personal Cloud Library"));
        }

        private void ShowSourceUnavailableMessage()
        {
            playniteApi.Dialogs.ShowMessage(
                "The configured source location is missing or unavailable.",
                GetDashboardResource("LOCPLSDashboardTitle", "Personal Cloud Library"));
        }

        private bool IsConfiguredSourceAvailable(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            if (!dashboardLibraryStatusService.IsSetupComplete(pluginSettings))
            {
                return false;
            }

            var providerType = GetProviderType(pluginSettings);
            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFileProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return File.Exists(pluginSettings.LocalManifestPath);
            }

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
            {
                var manifestResolution = manifestLoader.ResolveLocalManifestPath(pluginSettings);
                return Directory.Exists(pluginSettings.LocalLibraryRoot) &&
                       manifestResolution.Succeeded &&
                       File.Exists(manifestResolution.Path);
            }

            return true;
        }

        private static string ResolveDashboardSourceDescription(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            if (pluginSettings == null)
            {
                return string.Empty;
            }

            var providerType = GetProviderType(pluginSettings);
            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return pluginSettings.LocalLibraryRoot;
            }

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, StringComparison.OrdinalIgnoreCase))
            {
                var remote = pluginSettings.RcloneRemoteName;
                var root = pluginSettings.RcloneContentRoot;
                return string.IsNullOrWhiteSpace(root) ? remote + ":" : remote + ":" + root;
            }

            return pluginSettings.LocalManifestPath;
        }

        private static bool CanOpenSourceLocation(PersonalCloudLibrarySourceSettings pluginSettings)
        {
            var providerType = GetProviderType(pluginSettings);
            return string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFileProviderType, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase);
        }

        private static MainMenuItem CreateMainMenuItem(
            string menuSection,
            string resourceKey,
            string fallback,
            Action action,
            string iconPath)
        {
            return new MainMenuItem
            {
                MenuSection = menuSection,
                Description = GetDashboardResource(resourceKey, fallback),
                Icon = iconPath,
                Action = _ => action()
            };
        }

        private static string GetDashboardResource(string key, string fallback)
        {
            return ResourceProvider.GetString(key) ?? fallback;
        }

        private static object CreateToolbarIcon()
        {
            var iconPath = GetNavigationIconPath();
            if (!File.Exists(iconPath))
            {
                return new TextBlock { Text = "☁", FontSize = 18 };
            }

            return new Image
            {
                Source = new BitmapImage(new Uri(iconPath, UriKind.Absolute)),
                Width = 20,
                Height = 20,
                Stretch = Stretch.Uniform
            };
        }

        private static string GetNavigationIconPath()
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon.png");
        }

        private static bool OpenExplorerFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return false;
            }

            Process.Start("explorer.exe", path);
            return true;
        }

        private static bool OpenExplorerFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return false;
            }

            Process.Start("explorer.exe", "/select,\"" + path + "\"");
            return true;
        }
    }
}
