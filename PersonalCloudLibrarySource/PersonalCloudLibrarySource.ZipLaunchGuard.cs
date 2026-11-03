using Playnite.SDK.Events;
using System;
using System.IO;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public partial class PersonalCloudLibrarySource
    {
        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (args?.Game == null || args.Game.PluginId != Id)
                return;

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
                var decision = gate.Evaluate(Id, args.Game, args.SourceAction,
                    item, snapshot, receipts, approvals);
                if (decision.Applies && decision.Allowed)
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
