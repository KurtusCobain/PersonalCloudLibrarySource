using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;

namespace PersonalCloudLibrarySource
{
    // Used by the opt-in multi-source LibraryPlugin methods. This factory is an
    // explicit fail-closed bridge from frozen source routes to Playnite actions.
    // Do not construct actions from the globally selected source settings.
    public sealed class MultiSourceControllerFactory
    {
        public bool TryPrepareInstall(IPlayniteAPI api, Game game, Guid expectedPluginId,
            MultiSourceRuntimePlan plan, TransferQueueService queue,
            CacheOwnershipReceiptStore receipts, out InstallController controller,
            Func<bool> planStillCurrent = null)
        {
            controller = null;
            SourceScopedGameAction route;
            if (!TryResolveOwnedGame(game, expectedPluginId, plan, out route) ||
                queue == null || receipts == null || (api != null && planStillCurrent == null))
                return false;

            var item = route.CreateItemSnapshot();
            var sourceSettings = route.CreateSettingsSnapshot();
            if (!sourceSettings.Enabled || !sourceSettings.AllowDownloads ||
                !new GameWorkflowPolicyService().Evaluate(item, sourceSettings).CanInstall)
                return false;

            // Keep the existing single-source receipt key for legacy games.
            // New-profile games must use their distinct namespaced GameId.
            var explicitReceiptKey = string.Equals(
                route.OwnershipReceiptKey, route.OriginalItemId,
                StringComparison.Ordinal) ? null : route.OwnershipReceiptKey;

            controller = new RcloneInstallController(api, game, item,
                sourceSettings, queue, ownershipReceipts: receipts,
                ownershipReceiptKey: explicitReceiptKey,
                executionAuthorized: BindExecutionGuard(game, expectedPluginId, route.GameId, planStillCurrent));
            return true;
        }

        public bool TryPrepareUninstall(IPlayniteAPI api, Game game, Guid expectedPluginId,
            MultiSourceRuntimePlan plan, out UninstallController controller,
            CacheOwnershipReceiptStore receipts = null, Func<bool> planStillCurrent = null)
        {
            controller = null;
            SourceScopedGameAction route;
            if (receipts == null || (api != null && planStillCurrent == null) ||
                !TryResolveOwnedGame(game, expectedPluginId, plan, out route))
                return false;

            var item = route.CreateItemSnapshot();
            var sourceSettings = route.CreateSettingsSnapshot();

            // Multi-source routing cannot resurrect the legacy uninstall for
            // an extracted ZIP. Check both the source manifest ID and scoped
            // game identity: historic cache records may use either namespace.
            var maintenance = new ZipManagedMaintenanceService();
            if (maintenance.HasManagedFootprint(
                    sourceSettings.LocalCacheFolder, item.Id, receipts) ||
                maintenance.HasManagedFootprint(
                    sourceSettings.LocalCacheFolder, route.GameId, receipts))
                return false;

            // Multi-source mode never inherits external-cache deletion opt-ins,
            // and recursive folder deletion remains disabled by existing policy.
            if (!sourceSettings.Enabled ||
                sourceSettings.AllowUninstallOutsideCacheFolder ||
                !new GameWorkflowPolicyService().Evaluate(item, sourceSettings).CanUninstall)
                return false;

            controller = new PersonalCloudLibraryUninstallController(
                api, game, item, sourceSettings, ownershipReceipts: receipts,
                executionAuthorized: BindExecutionGuard(game, expectedPluginId, route.GameId, planStillCurrent));
            return true;
        }

        private static Func<bool> BindExecutionGuard(Game game, Guid pluginId,
            string gameId, Func<bool> planStillCurrent)
        {
            return () => game.PluginId == pluginId &&
                string.Equals(game.GameId, gameId, StringComparison.Ordinal) &&
                (planStillCurrent == null || planStillCurrent());
        }

        private static bool TryResolveOwnedGame(Game game, Guid expectedPluginId,
            MultiSourceRuntimePlan plan, out SourceScopedGameAction route)
        {
            route = null;
            return game != null &&
                expectedPluginId != Guid.Empty &&
                game.PluginId == expectedPluginId &&
                plan != null && plan.CanPublish &&
                plan.TryResolveAction(game.GameId, out route) &&
                string.Equals(route.GameId, game.GameId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(route.ProfileId);
        }
    }
}
