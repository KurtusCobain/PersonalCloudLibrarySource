using Playnite.SDK.Events;
using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource
    {
        private bool IsTransferRecoveryClearForLaunch(Guid gameId, string launchPath, string installDirectory)
        {
            if (zipHostStopping || transferPresentationDisposed) return false;
            try
            {
                var manager = transferManager;
                if (manager != null)
                    foreach (var job in manager.GetReservationSnapshot())
                        if (TransferRecoveryJournal.ConflictsWithLaunch(job.GameId, job.Destination, job.Id,
                            gameId, launchPath, installDirectory))
                            return false;
                return GetTransferRecoveryJournal().IsClearForGameLaunch(gameId, launchPath, installDirectory);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PCLS could not establish transfer recovery state for launch.");
                return false;
            }
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (args?.Game == null || args.Game.PluginId != Id)
                return;

            // Once irreversible cleanup begins, Playnite must not start this
            // game while file-level deletion is in progress. This complements
            // the IsRunning/IsLaunching check performed before cleanup.
            if (activeZipCleanups.ContainsKey(args.Game.Id) ||
                activeZipRecoveries.ContainsKey(args.Game.Id) ||
                activeZipExtractions.ContainsKey(args.Game.Id) ||
                !IsTransferRecoveryClearForLaunch(args.Game.Id, args.SourceAction?.Path, args.Game.InstallDirectory))
            {
                args.CancelStartup = true;
                logger.Warn("PCLS prevented game startup during package work or unresolved transfer recovery.");
                try
                {
                    playniteApi.Dialogs.ShowMessage(
                        PclsResources.Get("LOCPLSTransferRecoveryLaunchBusy",
                            "This game cannot start while its transfer or ZIP operation is active, or recovery evidence cannot be verified. Preserve files and review transfer recovery."),
                        PclsResources.Get("LOCPLSTransferRecoveryLaunchTitle",
                            "Library Launch Deferred"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "PCLS could not display cleanup launch refusal.");
                }
                return;
            }

            var gate = new ZipManagedLaunchGate();
            if (!gate.IsManagedAction(Id, args.Game, args.SourceAction))
                return;

            // Managed ZIP launch paths must fail CLOSED. This event runs before
            // Playnite dispatches the generic file action; no process has yet
            // been launched by the selected action.
            try
            {
                var snapshot = settings.GetRuntimeSettingsSnapshot();
                PersonalCloudLibraryItem item;
                var currentItems = GetValidatedManifestItemsSnapshot();
                if (!currentItems.TryGetValue(args.Game.GameId, out item))
                    item = null;

                var receipts = new CacheOwnershipReceiptStore(Path.Combine(
                    GetPluginDataDirectory(), "ownership-receipts"));
                var approvals = new ZipLauncherApprovalService(Path.Combine(
                    GetPluginDataDirectory(), "zip-launcher-approvals"));
                var baseline = new ZipMutableDataBaselineStore(Path.Combine(
                    GetPluginDataDirectory(), "zip-mutable-data-baselines"));
                ZipManagedLaunchDecision decision;
                // Enforce a real bounded synchronous verification window.
                // A timeout denies this launch; it never skips integrity
                // checks or permits execution on a cache-only shortcut.
                using (var verification = new CancellationTokenSource())
                {
                    verification.CancelAfter(TimeSpan.FromSeconds(5));
                    decision = gate.Evaluate(Id, args.Game, args.SourceAction,
                        item, snapshot, receipts, approvals,
                        verification.Token, baseline);
                }
                if (decision.Applies && decision.Allowed &&
                    IsTransferRecoveryClearForLaunch(args.Game.Id, args.SourceAction?.Path, args.Game.InstallDirectory))
                    return;

                args.CancelStartup = true;
                logger.Warn("PCLS blocked managed ZIP launcher: " + decision.Reason);
            }
            catch (Exception ex)
            {
                args.CancelStartup = true;
                logger.Warn(ex, "PCLS could not verify managed ZIP launcher at startup.");
            }

            // Never silently permit a stale/revoked launch. No attempt is made
            // to relaunch, repair or modify the extracted game from this event.
            try
            {
                playniteApi.Dialogs.ShowMessage(
                    PclsResources.Get("LOCPLSZipLaunchBlocked",
                        "PCLS blocked this ZIP launcher because its approval, current source, or extracted-file inventory could not be verified. No game was started. Review Managed ZIP Maintenance and Update Game Library before trying again."),
                    PclsResources.Get("LOCPLSZipLaunchBlockedTitle",
                        "Managed ZIP Launch Blocked"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "PCLS could not show its managed ZIP launch-block notice.");
            }
        }
    }
}
