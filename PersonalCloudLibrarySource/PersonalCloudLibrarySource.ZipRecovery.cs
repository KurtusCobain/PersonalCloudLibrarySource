using Playnite.SDK.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource
    {
        // The user must explicitly choose each SAME-VOLUME recovery move.
        // This feature never erases a directory, receipt, source ZIP or save.
        private async void ManageZipRecoveryAsync(GameCommandTarget selected)
        {
            var title = PclsResources.Get("LOCPLSZipRecoveryTitle",
                "Manage ZIP Recovery");
            try
            {
                if (selected?.Game == null || selected.Game.PluginId != Id)
                    return;
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
                if (activeZipExtractions.ContainsKey(selected.Game.Id) ||
                    GetTransferManager().GetActiveJobForGame(selected.Game.Id) != null)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipRecoveryBusy",
                            "Finish or cancel active transfers before moving a managed ZIP."),
                        title);
                    return;
                }

                var gameId = target.Item.Id;
                var root = snapshot.LocalCacheFolder;
                var receipts = new CacheOwnershipReceiptStore(Path.Combine(
                    GetPluginDataDirectory(), "ownership-receipts"));
                var approvals = new ZipLauncherApprovalService(Path.Combine(
                    GetPluginDataDirectory(), "zip-launcher-approvals"));
                var recovery = new ZipManagedRecoveryService(Path.Combine(
                    GetPluginDataDirectory(), "zip-recovery-journals"));
                var state = recovery.Inspect(root, gameId);
                if (state.State == ZipRecoveryState.Ambiguous)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipRecoveryAmbiguous",
                            "ZIP recovery journal or folder locations are inconsistent. No files were changed. Preserve both folders and the journal for manual recovery."),
                        title);
                    return;
                }
                if (state.State == ZipRecoveryState.Restored)
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSZipRecoveryAlreadyRestored",
                            "This ZIP recovery was already restored and finalized. The original files remain in place; no further recovery move is available for this journal."),
                        title);
                    return;
                }

                if (state.State == ZipRecoveryState.NoJournal)
                {
                    var maintenance = await Task.Run(() =>
                        new ZipManagedMaintenanceService().Assess(
                            root, gameId, receipts, approvals));
                    if (maintenance.Status != ZipManagedMaintenanceStatus.CleanNeedsManualRemoval)
                    {
                        playniteApi.Dialogs.ShowMessage(
                            PclsResources.Get("LOCPLSZipRecoveryNotClean",
                                "A managed ZIP can be moved to recovery only when its original inventory is unchanged and its launcher approval has been revoked. Any saves or modified content must be preserved and reviewed manually."),
                            title);
                        return;
                    }
                }

                var prompt = state.State == ZipRecoveryState.NoJournal
                    ? PclsResources.Format("LOCPLSZipRecoveryQuarantineConfirm",
                        "Move this verified, unapproved ZIP extraction to a reversible recovery folder?{0}{0}From: {1}{0}To: {2}{0}{0}Yes moves the entire folder without deleting files. No keeps it. Revoke launcher approval first and refresh Playnite afterward.",
                        Environment.NewLine, state.OriginalDirectory,
                        state.RecoveryDirectory)
                    : state.State == ZipRecoveryState.Quarantined
                        ? PclsResources.Format("LOCPLSZipRecoveryRestoreConfirm",
                            "Restore this ZIP from its recovery folder?{0}{0}From: {1}{0}To: {2}{0}{0}Yes moves the intact verified folder back without deleting files. No keeps it in recovery. Launcher approval is NOT recreated.",
                            Environment.NewLine, state.RecoveryDirectory,
                            state.OriginalDirectory)
                        : PclsResources.Get("LOCPLSZipRecoveryResumeConfirm",
                            "A ZIP recovery journal exists and the original directory is present. Retry the prepared reversible move? Yes retries after full verification; No leaves all files unchanged. No automatic deletion is performed.");

                if (playniteApi.Dialogs.ShowMessage(prompt, title,
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;

                // Revalidate config and the manifest following user input.
                // Never relocate a folder bound to an obsolete cache identity.
                var updated = settings.GetRuntimeSettingsSnapshot();
                if (updated == null || !updated.Enabled ||
                    !string.Equals(PathBoundary.Normalize(updated.LocalCacheFolder),
                        PathBoundary.Normalize(root), StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetProviderType(snapshot),
                        GetProviderType(updated), StringComparison.OrdinalIgnoreCase))
                    return;
                var latestManifest = LoadValidatedManifest(updated);
                var current = gameCommandService.ResolveTargets(
                    new[] { selected.Game }, latestManifest.Items, updated, Id).Single();
                if (current.Item == null ||
                    !string.Equals(current.Item.Id, gameId, StringComparison.Ordinal))
                    return;
                if (activeZipExtractions.ContainsKey(selected.Game.Id) ||
                    GetTransferManager().GetActiveJobForGame(selected.Game.Id) != null)
                    return;

                string reason = string.Empty;
                var result = await Task.Run(() =>
                {
                    if (state.State == ZipRecoveryState.NoJournal)
                        return recovery.TryQuarantine(root, gameId,
                            receipts, approvals, true, CancellationToken.None, out reason);
                    if (state.State == ZipRecoveryState.Quarantined)
                        return recovery.TryRestore(root, gameId,
                            receipts, approvals, true, CancellationToken.None, out reason);
                    return recovery.TryResumePrepared(root, gameId,
                        receipts, approvals, true, CancellationToken.None, out reason);
                });
                playniteApi.Dialogs.ShowMessage(
                    result
                        ? PclsResources.Get("LOCPLSZipRecoverySuccess",
                            "Managed ZIP recovery move completed without deleting files. Ownership records and launcher approval state were not changed. Update Game Library to refresh Playnite.")
                        : PclsResources.Get("LOCPLSZipRecoveryFailed",
                            "Managed ZIP recovery was not completed. Preserve the journal, original ZIP, and both cache folders; review their status before retrying. No recursive deletion was attempted."),
                    title);
                if (!result)
                    logger.Warn("PCLS managed ZIP recovery declined or failed: " + reason);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PCLS managed ZIP recovery failed.");
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Get("LOCPLSZipRecoveryFailed",
                        "Managed ZIP recovery was not completed. Preserve the journal, original ZIP, and both cache folders; review their status before retrying. No recursive deletion was attempted."),
                    title);
            }
        }
    }
}
