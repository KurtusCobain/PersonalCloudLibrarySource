from pathlib import Path


def replace_once(text, old, new, label):
    if new in text:
        return text
    if old not in text:
        raise SystemExit(f"Patch target missing for {label}: {old}")
    return text.replace(old, new, 1)


navigation_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.Navigation.cs")
navigation = navigation_path.read_text(encoding="utf-8-sig")
navigation = replace_once(
    navigation,
    "DataContext = new CloudLibraryDashboardViewModel(dashboardStateStore, navigationService)",
    "DataContext = new CloudLibraryDashboardViewModel(\n                    dashboardStateStore,\n                    navigationService,\n                    GetTransferManager(),\n                    GetTransferExecutor())",
    "dashboard transfer dependencies",
)
navigation_path.write_text(navigation, encoding="utf-8-sig")

controller_path = Path("PersonalCloudLibrarySource/RcloneInstallController.cs")
controller = controller_path.read_text(encoding="utf-8-sig")
controller = replace_once(
    controller,
    """                        destinationPath,
                        providerType);""",
    """                        destinationPath,
                        providerType,
                        sourceType == \"directory\");""",
    "directory transfer metadata",
)
controller_path.write_text(controller, encoding="utf-8-sig")

commands_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.GameCommands.cs")
commands = commands_path.read_text(encoding="utf-8-sig")
commands = replace_once(
    commands,
    "using System.Linq;\nusing System.Windows;",
    "using System.Linq;\nusing System.Threading.Tasks;\nusing System.Windows;",
    "game command task namespace",
)
commands = replace_once(
    commands,
    """                var targets = gameCommandService.ResolveTargets(games, manifest.Items, pluginSettings, Id).ToList();
                var availability = gameCommandPolicyService.Evaluate(targets.Select(target => target.PolicyContext));""",
    """                var targets = gameCommandService.ResolveTargets(games, manifest.Items, pluginSettings, Id).ToList();
                var manager = GetTransferManager();
                foreach (var target in targets)
                {
                    target.PolicyContext.HasActiveTransfer = manager.GetActiveJobForGame(target.Game.Id) != null;
                    target.PolicyContext.HasRetryableTransfer = manager.GetLatestRetryableJobForGame(target.Game.Id) != null;
                }

                var availability = gameCommandPolicyService.Evaluate(targets.Select(target => target.PolicyContext));""",
    "per-game transfer availability",
)
commands = replace_once(
    commands,
    """            if (availability.CanOpenCachedFolder)
            {""",
    """            if (availability.CanCancelTransfer)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    \"LOCPLSGameCancelTransfer\",
                    \"Cancel Active Transfer\",
                    () => CancelActiveTransfer(target)));
            }

            if (availability.CanRetryTransfer)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    \"LOCPLSGameRetryTransfer\",
                    \"Retry Last Transfer\",
                    () => RetryLastTransfer(target)));
            }

            if (availability.CanOpenCachedFolder)
            {""",
    "single-game cancel retry menu",
)
commands = replace_once(
    commands,
    """        private GameMenuItem CreateGameMenuItem(
""",
    """        private void CancelActiveTransfer(GameCommandTarget target)
        {
            var active = target?.Game == null
                ? null
                : GetTransferManager().GetActiveJobForGame(target.Game.Id);
            if (active == null)
            {
                return;
            }

            GetTransferManager().Cancel(active.Id);
        }

        private void RetryLastTransfer(GameCommandTarget target)
        {
            if (target?.Game == null)
            {
                return;
            }

            var manager = GetTransferManager();
            if (manager.GetActiveJobForGame(target.Game.Id) != null)
            {
                return;
            }

            var previous = manager.GetLatestRetryableJobForGame(target.Game.Id);
            if (previous == null || string.Equals(
                previous.ProviderType,
                PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var retry = manager.Retry(previous.Id);
            Task.Run(() => GetTransferExecutor().ExecuteLocal(retry.Id, retry.IsDirectory));
        }

        private GameMenuItem CreateGameMenuItem(
""",
    "game transfer commands",
)
commands_path.write_text(commands, encoding="utf-8-sig")

project_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.csproj")
project = project_path.read_text(encoding="utf-8-sig")
project = replace_once(
    project,
    "    <Compile Include=\"Dashboard\\CloudLibraryDashboardViewModel.cs\" />",
    "    <Compile Include=\"Dashboard\\CloudLibraryDashboardViewModel.cs\" />\n    <Compile Include=\"Dashboard\\CloudTransferQueueItemViewModel.cs\" />",
    "queue view model project registration",
)
project_path.write_text(project, encoding="utf-8-sig")

localization_path = Path("PersonalCloudLibrarySource/Localization/en_US.xaml")
localization = localization_path.read_text(encoding="utf-8-sig")
localization = replace_once(
    localization,
    """    <sys:String x:Key=\"LOCPLSFailedTransfers\">Failed transfers</sys:String>
    <sys:String x:Key=\"LOCPLSCacheHeading\">Cache</sys:String>""",
    """    <sys:String x:Key=\"LOCPLSFailedTransfers\">Failed transfers</sys:String>
    <sys:String x:Key=\"LOCPLSTransferCancel\">Cancel</sys:String>
    <sys:String x:Key=\"LOCPLSTransferRetry\">Retry</sys:String>
    <sys:String x:Key=\"LOCPLSCacheHeading\">Cache</sys:String>""",
    "dashboard transfer localization",
)
localization = replace_once(
    localization,
    """    <sys:String x:Key=\"LOCPLSGameInstall\">Install to This Computer</sys:String>
    <sys:String x:Key=\"LOCPLSGameOpenCachedFolder\">Open Cached Folder</sys:String>""",
    """    <sys:String x:Key=\"LOCPLSGameInstall\">Install to This Computer</sys:String>
    <sys:String x:Key=\"LOCPLSGameCancelTransfer\">Cancel Active Transfer</sys:String>
    <sys:String x:Key=\"LOCPLSGameRetryTransfer\">Retry Last Transfer</sys:String>
    <sys:String x:Key=\"LOCPLSGameOpenCachedFolder\">Open Cached Folder</sys:String>""",
    "game transfer localization",
)
localization_path.write_text(localization, encoding="utf-8-sig")
