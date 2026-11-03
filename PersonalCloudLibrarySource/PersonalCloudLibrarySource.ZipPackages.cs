using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource
    {
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource>
            activeZipExtractions = new ConcurrentDictionary<Guid, CancellationTokenSource>();

        // This is deliberately a separate, user-initiated extraction operation.
        // It never calls Playnite.InstallGame, sets IsInstalled or launches code.
        private async void ExtractZipGameTargetAsync(GameCommandTarget selected)
        {
            var title = PclsResources.Get("LOCPLSZipExtractTitle",
                "Extract ZIP to Separate Cache Folder");
            CancellationTokenSource cancellation = null;
            var registered = false;
            try
            {
                if (selected?.Game == null || selected.Game.PluginId != Id)
                    return;
                var gameId = selected.Game.Id;
                if (activeZipExtractions.ContainsKey(gameId))
                    return;

                var currentSettings = settings.GetRuntimeSettingsSnapshot();
                if (currentSettings == null || !currentSettings.Enabled ||
                    !currentSettings.AllowDownloads)
                    return;

                var manifest = LoadValidatedManifest(currentSettings);
                var current = gameCommandService.ResolveTargets(
                    new[] { selected.Game }, manifest.Items, currentSettings, Id).Single();
                var candidate = new ZipGameReviewTargetPolicy()
                    .Resolve(current, currentSettings, Id);
                if (!candidate.Available)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipReviewUnavailable",
                            "A matching local ZIP is no longer available. No files were changed."),
                        title);
                    return;
                }

                // A large archive must never be hashed on Playnite's UI thread.
                var review = await Task.Run(() => new ZipPackageReviewService()
                    .Inspect(candidate.ArchivePath, current.Item.Title));
                if (!review.PassedPreflight)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Format("LOCPLSZipReviewRejected",
                            "ZIP review was refused: {0}{1}No files were changed.",
                            review.Reason, Environment.NewLine),
                        title);
                    return;
                }

                // Re-read settings and manifest following the async review.
                // User-facing approval is only for the same current ZIP source.
                currentSettings = settings.GetRuntimeSettingsSnapshot();
                if (currentSettings == null || !currentSettings.Enabled ||
                    !currentSettings.AllowDownloads)
                    return;
                manifest = LoadValidatedManifest(currentSettings);
                current = gameCommandService.ResolveTargets(
                    new[] { selected.Game }, manifest.Items, currentSettings, Id).Single();
                var refreshedSource = new ZipGameReviewTargetPolicy()
                    .Resolve(current, currentSettings, Id);
                if (!refreshedSource.Available ||
                    !string.Equals(candidate.ArchivePath, refreshedSource.ArchivePath,
                        StringComparison.OrdinalIgnoreCase))
                    return;

                var receipts = new CacheOwnershipReceiptStore(Path.Combine(
                    GetPluginDataDirectory(), "ownership-receipts"));
                var extractor = new ZipManagedExtractionService();
                var plan = extractor.Prepare(review, candidate.ArchivePath,
                    currentSettings.LocalCacheFolder, current.Item.Id, receipts);
                if (plan == null)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipExtractUnavailable",
                            "Extraction is unavailable: the destination is unsafe, already occupied or already recorded. No files were changed."),
                        title);
                    return;
                }

                // Yes = extract; No = keep ZIP. No dialog or policy heuristic
                // is allowed to turn an absent answer into extraction consent.
                var decision = playniteApi.Dialogs.ShowMessage(
                    PclsResources.Format("LOCPLSZipExtractConfirmation",
                        "Keep the current ZIP (No), or extract its verified contents to this new folder (Yes)?{0}{0}{1}{0}{0}No existing files will be overwritten. No executable or installer will run. Playnite installed state and game launch settings will NOT change. The result is a separate managed-cache extraction.",
                        Environment.NewLine, plan.Destination),
                    title, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (decision != MessageBoxResult.Yes)
                    return;

                cancellation = new CancellationTokenSource();
                if (!activeZipExtractions.TryAdd(gameId, cancellation))
                    return;
                registered = true;

                // The extraction, archive hash, inventory scan and receipt
                // issuance may be expensive; keep them off the UI thread.
                var receiptStore = receipts;
                var approvedPlan = plan;
                var token = cancellation.Token;
                var result = await Task.Run(() => extractor.ExecuteApproved(
                    approvedPlan, true, receiptStore, token));

                if (result.Cancelled)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipExtractCancelled",
                            "ZIP extraction was cancelled. Existing source and game files were preserved."),
                        title);
                }
                else if (!result.Succeeded)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipExtractFailed",
                            "ZIP extraction was refused or failed. Inspect the archive and cache settings. Existing files were not overwritten."),
                        title);
                }
                else if (!result.ReceiptRecorded)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Format("LOCPLSZipExtractUntracked",
                            "ZIP files were extracted to {0}, but an inventory receipt could not be saved. Preserve these files and inspect them manually. The game has NOT been marked installed.",
                            result.Destination),
                        title);
                }
                else
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Format("LOCPLSZipExtractComplete",
                            "ZIP contents extracted and recorded at:{0}{1}{0}{0}The ZIP source was preserved. Playnite installed state, launch settings and uninstall permissions were not changed. Configure a launcher explicitly before playing.",
                            Environment.NewLine, result.Destination),
                        title);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PCLS ZIP extraction flow failed.");
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Get("LOCPLSZipExtractFailed",
                        "ZIP extraction was refused or failed. Inspect the archive and cache settings. Existing files were not overwritten."),
                    title);
            }
            finally
            {
                if (registered && selected?.Game != null)
                {
                    CancellationTokenSource removed;
                    activeZipExtractions.TryRemove(selected.Game.Id, out removed);
                }
                cancellation?.Dispose();
            }
        }

        // Preview only; there is deliberately NO Play action, launcher execution
        // or installed-state update attached to the candidate ranking.
        private async void ReviewExtractedZipLauncherAsync(GameCommandTarget selected)
        {
            var title = PclsResources.Get("LOCPLSZipLauncherReviewTitle",
                "Review Extracted ZIP Launchers");
            try
            {
                if (selected?.Game == null || selected.Game.PluginId != Id)
                    return;
                var snapshot = settings.GetRuntimeSettingsSnapshot();
                if (snapshot == null || !snapshot.Enabled)
                    return;
                var manifest = LoadValidatedManifest(snapshot);
                var current = gameCommandService.ResolveTargets(
                    new[] { selected.Game }, manifest.Items, snapshot, Id).Single();
                if (current.Item == null)
                    return;
                var receipts = new CacheOwnershipReceiptStore(Path.Combine(
                    GetPluginDataDirectory(), "ownership-receipts"));
                var policy = new ZipExtractedLauncherReviewService();
                if (!policy.HasRecordedExtraction(snapshot.LocalCacheFolder,
                        current.Item.Id, receipts))
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipLauncherReviewUnavailable",
                            "No managed extracted ZIP and matching inventory receipt were found."),
                        title);
                    return;
                }

                var result = await Task.Run(() => policy.Inspect(
                    snapshot.LocalCacheFolder, current.Item.Id,
                    current.Item.Title, receipts));
                if (!result.InventoryVerified)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipLauncherReviewRefused",
                            "Extracted files or their ownership inventory have changed. No launcher is approved. Review the files manually."),
                        title);
                    return;
                }

                var top = result.Launchers.Candidates.Take(6)
                    .Select(candidate => "  " + candidate.RelativePath + " — " +
                        candidate.Kind).ToList();
                var preview = top.Count == 0
                    ? PclsResources.Get("LOCPLSZipLauncherReviewEmpty",
                        "No supported Windows launcher candidates were found.")
                    : string.Join(Environment.NewLine, top);
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Format("LOCPLSZipLauncherReviewCompleted",
                        "Verified extracted ZIP inventory:{0}{1}{0}{0}Candidate launchers (first six):{0}{2}{0}{0}These are only suggestions. No launcher or installer has been approved or executed. Playnite install state and game actions are unchanged.",
                        Environment.NewLine, result.ExtractedDirectory, preview),
                    title);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PCLS could not verify extracted ZIP launcher candidates.");
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Get("LOCPLSZipLauncherReviewFailed",
                        "Launcher review could not be completed. No files were changed."),
                    title);
            }
        }

        private void ApplyExplicitZipLauncherApprovals(
            IReadOnlyList<GameMetadata> games,
            IReadOnlyList<PersonalCloudLibraryItem> validItems,
            PersonalCloudLibrarySourceSettings runtimeSettings)
        {
            try
            {
                if (games == null || validItems == null || runtimeSettings == null ||
                    !runtimeSettings.Enabled)
                    return;
                var storePath = Path.Combine(GetPluginDataDirectory(),
                    "zip-launcher-approvals");
                if (!Directory.Exists(storePath)) return;
                var service = new ZipLauncherApprovalService(storePath);
                var approvedIds = service.ApprovedGameIds();
                if (approvedIds.Count == 0) return;
                var gameById = games.Where(g => g != null &&
                    !string.IsNullOrWhiteSpace(g.GameId))
                    .GroupBy(g => g.GameId, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
                var itemById = validItems.Where(i => i != null &&
                    !string.IsNullOrWhiteSpace(i.Id))
                    .GroupBy(i => i.Id, StringComparer.Ordinal)
                    .ToDictionary(i => i.Key, i => i.First(), StringComparer.Ordinal);
                var receiptStore = new CacheOwnershipReceiptStore(
                    Path.Combine(GetPluginDataDirectory(), "ownership-receipts"));
                var activated = 0;
                foreach (var id in approvedIds)
                {
                    GameMetadata metadata;
                    PersonalCloudLibraryItem item;
                    if (!gameById.TryGetValue(id, out metadata) ||
                        !itemById.TryGetValue(id, out item))
                        continue;
                    if (service.TryApply(item, runtimeSettings, metadata,
                        receiptStore))
                        activated++;
                }
                if (activated > 0)
                    logger.Info("PCLS activated " + activated +
                        " explicitly approved managed ZIP launcher(s) during import.");
            }
            catch (Exception ex)
            {
                // A malformed approval never converts a successful manifest
                // import into a destructive partial library result.
                logger.Warn(ex, "PCLS could not apply ZIP launcher approvals.");
            }
        }

        private async void ApproveExtractedZipLauncherAsync(GameCommandTarget selected)
        {
            var title = PclsResources.Get("LOCPLSZipApproveTitle",
                "Approve Extracted ZIP Launcher");
            try
            {
                if (selected?.Game == null || selected.Game.PluginId != Id)
                    return;
                var snapshot = settings.GetRuntimeSettingsSnapshot();
                if (snapshot == null || !snapshot.Enabled) return;
                var manifest = LoadValidatedManifest(snapshot);
                var current = gameCommandService.ResolveTargets(
                    new[] { selected.Game }, manifest.Items, snapshot, Id).Single();
                if (current.Item == null || !string.Equals(current.Item.Id,
                    selected.Game.GameId, StringComparison.Ordinal))
                    return;

                var receipts = new CacheOwnershipReceiptStore(Path.Combine(
                    GetPluginDataDirectory(), "ownership-receipts"));
                var reviewer = new ZipExtractedLauncherReviewService();
                var inspected = await Task.Run(() => reviewer.Inspect(
                    snapshot.LocalCacheFolder, current.Item.Id,
                    current.Item.Title, receipts));
                if (!inspected.InventoryVerified)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipLauncherReviewRefused",
                            "Extracted files or their ownership inventory have changed. No launcher is approved. Review the files manually."),
                        title);
                    return;
                }
                var candidates = inspected.Launchers.Candidates
                    .Where(c => c.Kind != LauncherCandidateKind.InstallerOrHelper &&
                        string.Equals(Path.GetExtension(c.RelativePath), ".exe",
                            StringComparison.OrdinalIgnoreCase))
                    .Take(6).ToList();
                if (candidates.Count == 0)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipApproveNoCandidates",
                            "No eligible game executables were found. Installers, helpers and scripts cannot be approved here."),
                        title);
                    return;
                }

                foreach (var choice in candidates)
                {
                    // Yes approves this exact path; No examines the next;
                    // Cancel quits with no writes. The dialog does NOT execute it.
                    var decision = playniteApi.Dialogs.ShowMessage(
                        PclsResources.Format("LOCPLSZipApproveChoice",
                            "Approve this extracted game executable?{0}{0}{1}{0}{0}Yes: approve this file; No: next candidate; Cancel: stop.{0}No executable will run. After approval, Update Game Library to enable Playnite's Play action, provided the source and extracted files still match.",
                            Environment.NewLine, choice.RelativePath),
                        title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (decision == MessageBoxResult.Cancel) return;
                    if (decision != MessageBoxResult.Yes) continue;

                    // Re-resolve after user interaction. Never persist an
                    // approval for a changed source or cache configuration.
                    var latest = settings.GetRuntimeSettingsSnapshot();
                    if (latest == null || !latest.Enabled ||
                        !string.Equals(PathBoundary.Normalize(snapshot.LocalCacheFolder),
                            PathBoundary.Normalize(latest.LocalCacheFolder),
                            StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(PersonalCloudLibrarySource.GetProviderType(snapshot),
                            PersonalCloudLibrarySource.GetProviderType(latest),
                            StringComparison.OrdinalIgnoreCase))
                        return;
                    var latestManifest = LoadValidatedManifest(latest);
                    var updated = gameCommandService.ResolveTargets(
                        new[] { selected.Game }, latestManifest.Items, latest, Id).Single();
                    if (updated.Item == null ||
                        !string.Equals(updated.Item.Id, current.Item.Id,
                            StringComparison.Ordinal) ||
                        !string.Equals(GetItemSourcePath(updated.Item),
                            GetItemSourcePath(current.Item), StringComparison.Ordinal) ||
                        !string.Equals(updated.Item.CachePath,
                            current.Item.CachePath, StringComparison.Ordinal))
                        return;

                    var service = new ZipLauncherApprovalService(Path.Combine(
                        GetPluginDataDirectory(), "zip-launcher-approvals"));
                    var success = await Task.Run(() =>
                    {
                        string reason;
                        return service.TryApprove(updated.Item, latest,
                            choice.RelativePath, receipts, true,
                            CancellationToken.None, out reason);
                    });
                    playniteApi.Dialogs.ShowMessage(success
                        ? PclsResources.Get("LOCPLSZipApproveSuccess",
                            "Launcher approval saved. Use Update Game Library to refresh Playnite's Play action. Files were not executed or modified.")
                        : PclsResources.Get("LOCPLSZipApproveFailed",
                            "Launcher approval was not saved. Source, inventory or approval state may have changed. No files were executed or modified."),
                        title);
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PCLS could not approve managed ZIP launcher.");
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Get("LOCPLSZipApproveFailed",
                        "Launcher approval was not saved. Source, inventory or approval state may have changed. No files were executed or modified."),
                    title);
            }
        }

        private void RevokeZipLauncherApproval(GameCommandTarget selected)
        {
            var title = PclsResources.Get("LOCPLSZipRevokeTitle",
                "Revoke ZIP Launcher Approval");
            try
            {
                if (selected?.Game == null || selected.Game.PluginId != Id)
                    return;
                // Re-resolve the currently validated manifest identity rather
                // than trusting a stale context-menu snapshot.
                var snapshot = settings.GetRuntimeSettingsSnapshot();
                if (snapshot == null || !snapshot.Enabled)
                    return;
                var manifest = LoadValidatedManifest(snapshot);
                var target = gameCommandService.ResolveTargets(
                    new[] { selected.Game }, manifest.Items, snapshot, Id).Single();
                if (target.Item == null ||
                    !string.Equals(target.Item.Id, selected.Game.GameId,
                        StringComparison.Ordinal))
                    return;

                var approvals = new ZipLauncherApprovalService(Path.Combine(
                    GetPluginDataDirectory(), "zip-launcher-approvals"));
                if (!approvals.HasApproval(target.Item.Id,
                    snapshot.LocalCacheFolder))
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipRevokeUnavailable",
                            "No valid ZIP launcher approval is recorded for this game."),
                        title);
                    return;
                }

                var confirmation = playniteApi.Dialogs.ShowMessage(
                    PclsResources.Get("LOCPLSZipRevokeConfirm",
                        "Revoke this ZIP launcher approval? Yes revokes it; No keeps it. The original ZIP, extracted game files, saves, and inventory receipt will remain untouched. Update Game Library afterward to refresh Playnite. An existing Play action may remain visible until it is refreshed; do not use it in the meantime."),
                    title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirmation != MessageBoxResult.Yes)
                    return;

                // A config change during the confirmation invalidates the
                // selected cache identity. Re-read before removing the record.
                var updatedSettings = settings.GetRuntimeSettingsSnapshot();
                if (updatedSettings == null || !updatedSettings.Enabled ||
                    !string.Equals(PathBoundary.Normalize(
                            updatedSettings.LocalCacheFolder),
                        PathBoundary.Normalize(snapshot.LocalCacheFolder),
                        StringComparison.OrdinalIgnoreCase))
                    return;
                var updatedManifest = LoadValidatedManifest(updatedSettings);
                var updated = gameCommandService.ResolveTargets(
                    new[] { selected.Game }, updatedManifest.Items,
                    updatedSettings, Id).Single();
                if (updated.Item == null ||
                    !string.Equals(updated.Item.Id, target.Item.Id,
                        StringComparison.Ordinal))
                    return;

                string reason;
                var success = approvals.TryRevoke(updated.Item.Id,
                    updatedSettings.LocalCacheFolder, true, out reason);
                playniteApi.Dialogs.ShowMessage(
                    success
                        ? PclsResources.Get("LOCPLSZipRevokeCompleted",
                            "ZIP launcher approval revoked. The original ZIP, extracted files, saves and receipt were preserved. Update Game Library to refresh Playnite's installed state and Play action. To select a different launcher, explicitly approve another candidate afterward.")
                        : PclsResources.Get("LOCPLSZipRevokeFailed",
                            "ZIP launcher approval could not be safely revoked. No game files were changed."),
                    title);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PCLS could not revoke ZIP launcher approval.");
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Get("LOCPLSZipRevokeFailed",
                        "ZIP launcher approval could not be safely revoked. No game files were changed."),
                    title);
            }
        }

        private async void ReviewManagedZipMaintenanceAsync(GameCommandTarget selected)
        {
            var title = PclsResources.Get("LOCPLSZipMaintenanceTitle",
                "Review Managed ZIP Maintenance");
            try
            {
                if (selected?.Game == null || selected.Game.PluginId != Id)
                    return;
                var snapshot = settings.GetRuntimeSettingsSnapshot();
                if (snapshot == null || !snapshot.Enabled)
                    return;
                var currentManifest = LoadValidatedManifest(snapshot);
                var target = gameCommandService.ResolveTargets(
                    new[] { selected.Game }, currentManifest.Items,
                    snapshot, Id).Single();
                if (target.Item == null ||
                    !string.Equals(target.Item.Id, selected.Game.GameId,
                        StringComparison.Ordinal))
                    return;

                var root = snapshot.LocalCacheFolder;
                var itemId = target.Item.Id;
                var receiptStore = new CacheOwnershipReceiptStore(Path.Combine(
                    GetPluginDataDirectory(), "ownership-receipts"));
                var approvals = new ZipLauncherApprovalService(Path.Combine(
                    GetPluginDataDirectory(), "zip-launcher-approvals"));
                var maintenance = new ZipManagedMaintenanceService();
                var report = await Task.Run(() => maintenance.Assess(
                    root, itemId, receiptStore, approvals));

                // A completed read-only review must not be presented as a
                // current safe-delete permit. Even Clean is informational.
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Format("LOCPLSZipMaintenanceCompleted",
                        "Managed ZIP maintenance review:{0}{0}Status: {1}{0}Extracted files recorded: {2}{0}Folders recorded: {3}{0}Inventory verified: {4}{0}{0}{5}{0}{0}No files were changed. Automatic repair and recursive removal are disabled. If files differ, preserve game saves and review them manually.",
                        Environment.NewLine,
                        report.Status.ToString(),
                        report.RecordedFileCount,
                        report.RecordedDirectoryCount,
                        report.HasVerifiedInventory
                            ? PclsResources.Get("LOCPLSZipMaintenanceYes", "Yes")
                            : PclsResources.Get("LOCPLSZipMaintenanceNo", "No"),
                        report.Reason),
                    title);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PCLS managed ZIP maintenance review failed.");
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Get("LOCPLSZipMaintenanceFailed",
                        "Managed ZIP maintenance could not be safely reviewed. No files were changed."),
                    title);
            }
        }

        private void CancelZipExtraction(Guid gameId)
        {
            CancellationTokenSource cancellation;
            if (!activeZipExtractions.TryGetValue(gameId, out cancellation))
                return;
            try { cancellation.Cancel(); }
            catch (ObjectDisposedException) { }
        }

        private void CancelAllZipExtractions()
        {
            foreach (var entry in activeZipExtractions)
                CancelZipExtraction(entry.Key);
        }
    }
}
