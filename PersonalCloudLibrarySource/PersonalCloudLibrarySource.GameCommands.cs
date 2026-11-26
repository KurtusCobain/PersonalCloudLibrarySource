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
                // Experimental multi-source game IDs are bound to profiles,
                // not the legacy manifest. Until ZIP approval/recovery records
                // are source-scoped, do not offer any legacy game commands
                // (especially destructive cleanup) under multi-source mode.
                if (pluginSettings == null || !pluginSettings.Enabled)
                    return menuItems;
                if (pluginSettings.EnableMultiSourceProfiles)
                {
                    menuItems.Add(CreateGameMenuItem(
                        GetDashboardResource("LOCPLSMenuSection", "Personal Cloud Library"),
                        "LOCPLSOpenDashboard", "Open Dashboard",
                        navigationService.OpenDashboard));
                    return menuItems;
                }
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

            // ZIP inspection is a read-only action. It never turns an
            // archive into an installed game or skips an extraction prompt.
            if (new ZipGameReviewTargetPolicy().Resolve(
                    target, settings.GetRuntimeSettingsSnapshot(), Id).Available)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameReviewZip",
                    "Review ZIP Package Contents",
                    () => ReviewZipGameTarget(target)));
                if (settings.GetRuntimeSettingsSnapshot().AllowDownloads &&
                    !activeZipExtractions.ContainsKey(target.Game.Id))
                {
                    menuItems.Add(CreateGameMenuItem(
                        section,
                        "LOCPLSZipExtractTitle",
                        "Extract ZIP to Separate Cache Folder",
                        () => ExtractZipGameTargetAsync(target)));
                }
            }

            if (activeZipExtractions.ContainsKey(target.Game.Id))
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSZipExtractCancelMenu",
                    "Cancel ZIP Extraction",
                    () => CancelZipExtraction(target.Game.Id)));
            }

            if (target.Item != null &&
                new ZipExtractedLauncherReviewService().HasRecordedExtraction(
                    settings.GetRuntimeSettingsSnapshot().LocalCacheFolder,
                    target.Item.Id, new CacheOwnershipReceiptStore(Path.Combine(
                        GetPluginDataDirectory(), "ownership-receipts"))))
            {
                menuItems.Add(CreateGameMenuItem(
                    section, "LOCPLSZipLauncherReviewTitle",
                    "Review Extracted ZIP Launchers",
                    () => ReviewExtractedZipLauncherAsync(target)));
                menuItems.Add(CreateGameMenuItem(
                    section, "LOCPLSZipApproveTitle",
                    "Approve Extracted ZIP Launcher",
                    () => ApproveExtractedZipLauncherAsync(target)));
            }

            // Show the diagnostic for a managed ZIP folder or ownership
            // record, even if it was modified or the receipt is missing.
            var menuSnapshot = settings.GetRuntimeSettingsSnapshot();
            if (target.Item != null && menuSnapshot != null &&
                new ZipManagedMaintenanceService().HasManagedFootprint(
                    menuSnapshot.LocalCacheFolder, target.Item.Id,
                    new CacheOwnershipReceiptStore(Path.Combine(
                        GetPluginDataDirectory(), "ownership-receipts"))))
            {
                menuItems.Add(CreateGameMenuItem(
                    section, "LOCPLSZipMaintenanceTitle",
                    "Review Managed ZIP Maintenance",
                    () => ReviewManagedZipMaintenanceAsync(target)));
            }

            // A managed ZIP can be reversibly moved to its own recovery
            // folder, or explicitly restored, without recursive deletion.
            // The action rechecks the live manifest and ownership on click.
            if (target.Item != null && menuSnapshot != null &&
                (new ZipManagedMaintenanceService().HasManagedFootprint(
                    menuSnapshot.LocalCacheFolder, target.Item.Id,
                    new CacheOwnershipReceiptStore(Path.Combine(
                        GetPluginDataDirectory(), "ownership-receipts"))) ||
                 new ZipManagedRecoveryService(Path.Combine(
                     GetPluginDataDirectory(), "zip-recovery-journals"))
                    .HasJournalFootprint(menuSnapshot.LocalCacheFolder,
                        target.Item.Id)))
            {
                menuItems.Add(CreateGameMenuItem(
                    section, "LOCPLSZipRecoveryTitle",
                    "Manage ZIP Recovery",
                    () => ManageZipRecoveryAsync(target)));
            }

            // Permanent cleanup is intentionally separate from reversible
            // quarantine and requires TWO explicit confirmations. It appears
            // only for a quarantined or partially cleaned managed ZIP.
            if (PermanentZipCleanupHostEnabled &&
                target.Item != null && menuSnapshot != null &&
                !activeZipCleanups.ContainsKey(target.Game.Id))
            {
                var purgeState = new ZipManagedRecoveryService(Path.Combine(
                    GetPluginDataDirectory(), "zip-recovery-journals"))
                    .Inspect(menuSnapshot.LocalCacheFolder, target.Item.Id).State;
                if (purgeState == ZipRecoveryState.Quarantined ||
                    purgeState == ZipRecoveryState.CleanupPending)
                {
                    menuItems.Add(CreateGameMenuItem(
                        section, "LOCPLSZipPermanentCleanupTitle",
                        "Permanently Remove Quarantined ZIP Files",
                        () => PermanentlyCleanupZipAsync(target)));
                }
            }

            if (activeZipCleanups.ContainsKey(target.Game.Id))
            {
                menuItems.Add(CreateGameMenuItem(
                    section, "LOCPLSZipPermanentCleanupCancel",
                    "Cancel Permanent ZIP Cleanup",
                    () => CancelZipCleanup(target.Game.Id)));
            }

            // Revoke remains available for a recorded approval even when the
            // manifest source changed or the extracted files no longer match.
            // It changes only plugin approval metadata, not game files.
            var currentSettings = settings.GetRuntimeSettingsSnapshot();
            if (target.Item != null && currentSettings != null &&
                new ZipLauncherApprovalService(Path.Combine(
                    GetPluginDataDirectory(), "zip-launcher-approvals"))
                    .HasApproval(target.Item.Id, currentSettings.LocalCacheFolder))
            {
                menuItems.Add(CreateGameMenuItem(
                    section, "LOCPLSZipRevokeTitle",
                    "Revoke ZIP Launcher Approval",
                    () => RevokeZipLauncherApproval(target)));
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
                    () => CopyText(target.SourceDisplayPath, PclsResources.Get("LOCPLSSourcePathCopied", "Source path copied."))));
            }

            if (availability.CanCopyCachePath)
            {
                menuItems.Add(CreateGameMenuItem(
                    section,
                    "LOCPLSGameCopyCachePath",
                    "Copy Local Cache Path",
                    () => CopyText(target.CacheDisplayPath, PclsResources.Get("LOCPLSCachePathCopied", "Cache path copied."))));
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
                        PclsResources.Get("LOCPLSSourcePathsCopied", "Source paths copied."))));
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

        private bool ConfirmExternalSourceAccess(string path)
        {
            var approved = false;
            if (transferPresentationDisposed) return false;
            new PlayniteImportUiDispatcher(playniteApi).Invoke(() =>
            {
                if (transferPresentationDisposed) return;
                approved = playniteApi.Dialogs.ShowMessage(
                    PclsResources.Format("LOCPLSExternalSourceConfirmation",
                        "This manifest references a source outside the configured library location:{0}{0}{1}{0}{0}Only continue if you recognize and trust this path. Accessing a network share may authenticate to that server. Allow access for this operation only?",
                        Environment.NewLine, path),
                    PclsResources.Get("LOCPLSExternalSourceTitle", "Confirm external library source"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            });
            return approved && !transferPresentationDisposed;
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

            try
            {
                RetryHostedTransfer(previous.Id);
            }
            catch (TransferRetryRefusedException ex)
            {
                playniteApi.Dialogs.ShowMessage(ex.Message);
            }
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
                () => CopyText(target.SourceDisplayPath, PclsResources.Get("LOCPLSSourcePathCopied", "Source path copied.")),
                () => CopyText(target.CacheDisplayPath, PclsResources.Get("LOCPLSCachePathCopied", "Cache path copied."))));
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
                PclsResources.Get("LOCPLSCachedPathMissing", "The cached file or folder is missing."),
                GetDashboardResource("LOCPLSDashboardTitle", "Personal Cloud Library"));
        }

        private void OpenGameSourceLocation(GameCommandTarget target)
        {
            try
            {
                if (target?.Game == null || target.Game.PluginId != Id || transferPresentationDisposed) return;
                var current = settings.GetRuntimeSettingsSnapshot();
                if (current == null || !current.Enabled || current.EnableMultiSourceProfiles) return;
                var manifest = LoadValidatedManifest(current);
                var refreshed = gameCommandService.ResolveTargets(
                    new[] { target.Game }, manifest.Items, current, Id).Single();
                var path = refreshed.ResolvedLocalSourcePath;
                if (refreshed.RequiresExternalSourceConsent && !ConfirmExternalSourceAccess(path))
                    return;
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
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PCLS could not open the selected source location.");
            }
            if (!transferPresentationDisposed)
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Get("LOCPLSGameSourceUnavailable", "The source file or folder is unavailable. Cloud source paths can still be copied from the details view."),
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
                var title = target.Item?.Title ?? target.Game?.Name ?? PclsResources.Get("LOCPLSGameUnknown", "Unknown game");
                var warnings = new List<string>();
                if (target.Item == null)
                {
                    warnings.Add(PclsResources.Get("LOCPLSVerifyManifestMissing", "manifest entry missing"));
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(target.SourceDisplayPath))
                    {
                        warnings.Add(PclsResources.Get("LOCPLSVerifySourcePathMissing", "source path missing"));
                    }

                    if (target.PolicyContext.HasCachedPath && !target.PolicyContext.CanRemoveCachedCopy)
                    {
                        warnings.Add(string.IsNullOrWhiteSpace(target.UninstallRefusalReason)
                            ? PclsResources.Get("LOCPLSVerifyCacheUnsafe", "cached path is not safely removable")
                            : target.UninstallRefusalReason);
                    }
                }

                warningCount += warnings.Count;
                lines.Add(warnings.Count == 0
                    ? title + PclsResources.Get("LOCPLSVerifyReadySuffix", ": ready")
                    : title + ": " + string.Join(", ", warnings));
            }

            playniteApi.Dialogs.ShowMessage(
                PclsResources.Format(
                    "LOCPLSVerifyEntriesResult",
                    "Entries checked: {0}{2}Warnings: {1}{2}{2}{3}",
                    targets.Count,
                    warningCount,
                    Environment.NewLine,
                    string.Join(Environment.NewLine, lines)),
                GetDashboardResource("LOCPLSGameVerifyEntry", "Verify Cloud Library Entry"));
        }

        private void RemoveSelectedCachedCopies(IReadOnlyList<GameCommandTarget> targets)
        {
            var result = playniteApi.Dialogs.ShowMessage(
                PclsResources.Format(
                    "LOCPLSRemoveSelectedConfirmation",
                    "Remove the managed cached copies for {0} selected games?{1}Source files and manifest entries will not be removed.",
                    targets.Count,
                    Environment.NewLine),
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
                    PclsResources.Format("LOCPLSCopyPathFailed", "The path could not be copied: {0}", ex.Message),
                    GetDashboardResource("LOCPLSDashboardTitle", "Personal Cloud Library"));
            }
        }
    }
}
