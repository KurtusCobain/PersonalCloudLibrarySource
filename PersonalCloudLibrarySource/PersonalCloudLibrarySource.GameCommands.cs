using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource
    {
        private readonly GameCommandPolicyService gameCommandPolicyService = new GameCommandPolicyService();
        private readonly GameCommandService gameCommandService = new GameCommandService();
        private CloudGameDetailsWindowService cloudGameDetailsWindowService;

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var menuItems = new List<GameMenuItem>();
            var games = args?.Games?.Where(game => game != null).ToList();
            if (games == null || games.Count == 0 || games.Any(game => game.PluginId != Id))
            {
                return menuItems;
            }

            try
            {
                var pluginSettings = settings.GetRuntimeSettingsSnapshot();
                var manifest = LoadValidatedManifest(pluginSettings);
                var targets = gameCommandService.ResolveTargets(games, manifest.Items, pluginSettings, Id).ToList();
                var manager = GetTransferManager();
                foreach (var target in targets)
                {
                    target.PolicyContext.HasActiveTransfer = manager.GetActiveJobForGame(target.Game.Id) != null;
                    target.PolicyContext.HasRetryableTransfer = manager.GetLatestRetryableJobForGame(target.Game.Id) != null;
                }

                var availability = gameCommandPolicyService.Evaluate(targets.Select(target => target.PolicyContext));
                if (!availability.ShowPluginMenu)
                {
                    return menuItems;
                }

                var section = GetDashboardResource("LOCPLSMenuSection", "Personal Cloud Library");
                if (availability.IsSingleSelection)
                {
                    AddSingleGameMenuItems(menuItems, section, targets[0], availability);
                }
                else
                {
                    AddMultiGameMenuItems(menuItems, section, targets, availability);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Personal Cloud Library Source could not build game context actions.");
            }

            return menuItems;
        }

        private void AddSingleGameMenuItems(
            ICollection<GameMenuItem> menuItems,
            string section,
            GameCommandTarget target,
            GameCommandAvailability availability)
        {
            if (availability.CanViewDetails)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameViewDetails",
                    "View Cloud Library Details",
                    () => OpenGameDetails(target)));
            }

            if (availability.CanInstallSelected)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameInstall",
                    "Install to This Computer",
                    () => playniteApi.InstallGame(target.Game.Id)));
            }

            if (availability.CanCancelTransfer)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameCancelTransfer",
                    "Cancel Active Transfer",
                    () => CancelActiveTransfer(target)));
            }

            if (availability.CanRetryTransfer)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameRetryTransfer",
                    "Retry Last Transfer",
                    () => RetryLastTransfer(target)));
            }

            if (availability.CanOpenCachedFolder)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameOpenCachedFolder",
                    "Open Cached Folder",
                    () => OpenCachedLocation(target)));
            }

            if (availability.CanOpenSourceLocation)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameOpenSource",
                    "Open Source Location",
                    () => OpenGameSourceLocation(target)));
            }

            if (availability.CanVerifySelected)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameVerifyEntry",
                    "Verify This Entry",
                    () => VerifyGameTarget(target)));
            }

            if (availability.CanCopySourcePaths)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameCopySourcePath",
                    "Copy Source Path",
                    () => CopyText(target.SourceDisplayPath, "Source path copied.")));
            }

            if (availability.CanCopyCachePath)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameCopyCachePath",
                    "Copy Local Cache Path",
                    () => CopyText(target.CacheDisplayPath, "Cache path copied.")));
            }

            if (availability.CanRemoveSelectedCachedCopies)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameRemoveCachedCopy",
                    "Remove Cached Copy",
                    () => playniteApi.UninstallGame(target.Game.Id)));
            }
        }

        private void AddMultiGameMenuItems(
            ICollection<GameMenuItem> menuItems,
            string section,
            IReadOnlyList<GameCommandTarget> targets,
            GameCommandAvailability availability)
        {
            if (availability.CanInstallSelected)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameInstallSelected",
                    "Install Selected Games",
                    () =>
                    {
                        foreach (var target in targets.Where(value => value.PolicyContext.CanInstall))
                        {
                            playniteApi.InstallGame(target.Game.Id);
                        }
                    }));
            }

            if (availability.CanVerifySelected)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameVerifySelected",
                    "Verify Selected Entries",
                    () => VerifyGameTargets(targets)));
            }

            if (availability.CanCopySourcePaths)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameCopySourcePaths",
                    "Copy Source Paths",
                    () => CopyText(
                        string.Join(Environment.NewLine, targets.Select(target => target.SourceDisplayPath)),
                        "Source paths copied.")));
            }

            if (availability.CanRemoveSelectedCachedCopies)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameRemoveSelectedCachedCopies",
                    "Remove Selected Cached Copies",
                    () => RemoveSelectedCachedCopies(targets)));
            }

            menuItems.Add(CreateGameMenuItem(
                section,
                "LOCPLSOpenDashboard",
                "Open Dashboard",
                navigationService.OpenDashboard));
        }

        private void CancelActiveTransfer(GameCommandTarget target)
        {
            var active = target?.Game == null
                ? null
                : GetTransferManager().GetActiveJobForGame(target.Game.Id);
            if (active == null)
            {
                return;
            }

            GetTransferQueue().Cancel(active.Id);
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
            if (previous == null)
            {
                return;
            }

            var settingsSnapshot = settings.GetRuntimeSettingsSnapshot();
            GetTransferQueue().Retry(previous.Id, settingsSnapshot);
        }

        private GameMenuItem CreateGameMenuItem(
            string section,
            string resourceKey,
            string fallback,
            Action action)
        {
            return new GameMenuItem
            {
                MenuSection = section,
                Description = GetDashboardResource(resourceKey, fallback),
                Icon = GetNavigationIconPath(),
                Action = _ => action()
            };
        }

        private void OpenGameDetails(GameCommandTarget target)
        {
            if (cloudGameDetailsWindowService == null)
            {
                cloudGameDetailsWindowService = new CloudGameDetailsWindowService(playniteApi);
            }

            cloudGameDetailsWindowService.Open(new CloudGameDetailsViewModel(
                target,
                () => playniteApi.InstallGame(target.Game.Id),
                () => playniteApi.UninstallGame(target.Game.Id),
                () => OpenCachedLocation(target),
                () => OpenGameSourceLocation(target),
                () => VerifyGameTarget(target),
                () => CopyText(target.SourceDisplayPath, "Source path copied."),
                () => CopyText(target.CacheDisplayPath, "Cache path copied.")));
        }

        private void OpenCachedLocation(GameCommandTarget target)
        {
            var path = target?.CacheDisplayPath;
            if (File.Exists(path))
            {
                OpenExplorerFile(path);
                return;
            }

            if (Directory.Exists(path))
            {
                OpenExplorerFolder(path);
                return;
            }

            playniteApi.Dialogs.ShowMessage(
                "The cached file or folder is missing.",
                GetDashboardResource("LOCPLSDashboardTitle", "Personal Cloud Library"));
        }

        private void OpenGameSourceLocation(GameCommandTarget target)
        {
            var path = target?.ResolvedLocalSourcePath;
            if (File.Exists(path))
            {
                OpenExplorerFile(path);
                return;
            }

            if (Directory.Exists(path))
            {
                OpenExplorerFolder(path);
                return;
            }

            playniteApi.Dialogs.ShowMessage(
                "The source file or folder is unavailable. Cloud source paths can still be copied from the details view.",
                GetDashboardResource("LOCPLSDashboardTitle", "Personal Cloud Library"));
        }

        private void VerifyGameTarget(GameCommandTarget target)
        {
            VerifyGameTargets(new[] { target });
        }

        private void VerifyGameTargets(IEnumerable<GameCommandTarget> sourceTargets)
        {
            var targets = sourceTargets?.Where(target => target != null).ToList() ?? new List<GameCommandTarget>();
            var lines = new List<string>();
            var warningCount = 0;

            foreach (var target in targets)
            {
                var title = target.Item?.Title ?? target.Game?.Name ?? "Unknown game";
                var warnings = new List<string>();
                if (target.Item == null)
                {
                    warnings.Add("manifest entry missing");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(target.SourceDisplayPath))
                    {
                        warnings.Add("source path missing");
                    }

                    if (target.PolicyContext.HasCachedPath && !target.PolicyContext.CanRemoveCachedCopy)
                    {
                        warnings.Add(string.IsNullOrWhiteSpace(target.UninstallRefusalReason)
                            ? "cached path is not safely removable"
                            : target.UninstallRefusalReason);
                    }
                }

                warningCount += warnings.Count;
                lines.Add(warnings.Count == 0
                    ? title + ": ready"
                    : title + ": " + string.Join(", ", warnings));
            }

            playniteApi.Dialogs.ShowMessage(
                "Entries checked: " + targets.Count + Environment.NewLine +
                "Warnings: " + warningCount + Environment.NewLine + Environment.NewLine +
                string.Join(Environment.NewLine, lines),
                GetDashboardResource("LOCPLSGameVerifyEntry", "Verify Cloud Library Entry"));
        }

        private void RemoveSelectedCachedCopies(IReadOnlyList<GameCommandTarget> targets)
        {
            var result = playniteApi.Dialogs.ShowMessage(
                "Remove the managed cached copies for " + targets.Count + " selected games?" + Environment.NewLine +
                "Source files and manifest entries will not be removed.",
                GetDashboardResource("LOCPLSGameRemoveSelectedCachedCopies", "Remove Selected Cached Copies"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            foreach (var target in targets)
            {
                playniteApi.UninstallGame(target.Game.Id);
            }
        }

        private void CopyText(string value, string confirmation)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            try
            {
                playniteApi.MainView.UIDispatcher.Invoke(() => Clipboard.SetText(value));
                playniteApi.Notifications.Add(new Playnite.SDK.NotificationMessage(
                    Guid.NewGuid().ToString(),
                    confirmation,
                    Playnite.SDK.NotificationType.Info));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Personal Cloud Library Source could not copy a path to the clipboard.");
                playniteApi.Dialogs.ShowErrorMessage(
                    "The path could not be copied: " + ex.Message,
                    GetDashboardResource("LOCPLSDashboardTitle", "Personal Cloud Library"));
            }
        }
    }
}
