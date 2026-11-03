using Playnite.SDK.Models;
using System;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    public sealed class ZipManagedLaunchDecision
    {
        public bool Applies { get; internal set; }
        public bool Allowed { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
    }

    // Runs in Playnite's OnGameStarting callback, BEFORE generic file startup.
    // The imported GameAction is not trusted: check the same exact source and
    // V2 extracted inventory again at the time the action is selected.
    public sealed class ZipManagedLaunchGate
    {
        // Only intercept a file action targeting this specific game's hashed
        // managed-extraction folder. Also detects stale paths after the cache
        // location changes, not only paths under the current configured root.
        public bool IsManagedAction(Guid pluginId, Game game, GameAction action)
        {
            if (game == null || action == null || pluginId == Guid.Empty ||
                game.PluginId != pluginId || string.IsNullOrWhiteSpace(game.GameId) ||
                string.IsNullOrWhiteSpace(action.Path))
                return false;
            try
            {
                var key = ZipManagedExtractionService.ReceiptKeyForGame(game.GameId);
                if (string.IsNullOrWhiteSpace(key) ||
                    !Path.IsPathRooted(action.Path))
                    return false;
                var path = Path.GetFullPath(action.Path);
                var current = Path.GetDirectoryName(path);
                for (var i = 0; i < 128 && !string.IsNullOrWhiteSpace(current); i++)
                {
                    if (string.Equals(Path.GetFileName(current), key,
                        StringComparison.OrdinalIgnoreCase))
                        return true;
                    var parent = Path.GetDirectoryName(current);
                    if (string.IsNullOrWhiteSpace(parent) ||
                        string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                        break;
                    current = parent;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public ZipManagedLaunchDecision Evaluate(Guid pluginId, Game game,
            GameAction selectedAction, PersonalCloudLibraryItem currentItem,
            PersonalCloudLibrarySourceSettings settings,
            CacheOwnershipReceiptStore receipts, ZipLauncherApprovalService approvals)
        {
            var decision = new ZipManagedLaunchDecision();
            if (!IsManagedAction(pluginId, game, selectedAction))
                return decision;
            decision.Applies = true;
            decision.Reason = "The managed ZIP launcher has no current verified approval.";
            try
            {
                if (settings == null || !settings.Enabled ||
                    currentItem == null || receipts == null || approvals == null ||
                    !string.Equals(currentItem.Id, game.GameId,
                        StringComparison.Ordinal) ||
                    selectedAction.Type != GameActionType.File ||
                    !selectedAction.IsPlayAction)
                    return decision;

                // This metadata object is throwaway. Never mutate the host's
                // game or its persisted custom Play actions from a launch gate.
                var approved = new GameMetadata
                {
                    GameId = currentItem.Id,
                    Name = currentItem.Title,
                    IsInstalled = false
                };
                if (!approvals.TryApply(currentItem, settings,
                    approved, receipts) ||
                    approved.GameActions == null ||
                    approved.GameActions.Count != 1)
                    return decision;

                var expected = approved.GameActions.Single();
                if (!string.Equals(PathBoundary.Normalize(selectedAction.Path),
                        PathBoundary.Normalize(expected.Path),
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(PathBoundary.Normalize(selectedAction.WorkingDir),
                        PathBoundary.Normalize(expected.WorkingDir),
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrWhiteSpace(selectedAction.Arguments))
                {
                    decision.Reason = "The selected Playnite action differs from the explicitly approved launcher.";
                    return decision;
                }
                decision.Allowed = true;
                decision.Reason = "Approved ZIP launcher and current extraction inventory verified.";
                return decision;
            }
            catch (Exception)
            {
                // No launch after an unexpected filesystem, receipt or JSON
                // error. Errors must never turn a guarded action into a pass.
                decision.Reason = "ZIP launcher could not be verified safely.";
                return decision;
            }
        }
    }
}
