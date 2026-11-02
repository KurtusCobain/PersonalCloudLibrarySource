using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;

namespace PersonalCloudLibrarySource
{
    // Not wired to the live LibraryPlugin methods yet. This factory is an
    // explicit fail-closed bridge from frozen source routes to Playnite actions.
    // Do not construct actions from the globally selected source settings.
    public sealed class MultiSourceControllerFactory
    {
        public bool TryPrepareInstall(IPlayniteAPI api, Game game, Guid expectedPluginId,
            MultiSourceRuntimePlan plan, TransferQueueService queue,
            CacheOwnershipReceiptStore receipts, out InstallController controller)
        {
            controller = null;
            SourceScopedGameAction route;
            if (!TryResolveOwnedGame(game, expectedPluginId, plan, out route) ||
                queue == null || receipts == null)
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
                ownershipReceiptKey: explicitReceiptKey);
            return true;
        }

        public bool TryPrepareUninstall(IPlayniteAPI api, Game game, Guid expectedPluginId,
            MultiSourceRuntimePlan plan, out UninstallController controller)
        {
            controller = null;
            SourceScopedGameAction route;
            if (!TryResolveOwnedGame(game, expectedPluginId, plan, out route))
                return false;

            var item = route.CreateItemSnapshot();
            var sourceSettings = route.CreateSettingsSnapshot();

            // Multi-source mode never inherits external-cache deletion opt-ins,
            // and recursive folder deletion remains disabled by existing policy.
            if (!sourceSettings.Enabled ||
                sourceSettings.AllowUninstallOutsideCacheFolder ||
                !new GameWorkflowPolicyService().Evaluate(item, sourceSettings).CanUninstall)
                return false;

            controller = new PersonalCloudLibraryUninstallController(
                api, game, item, sourceSettings);
            return true;
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
