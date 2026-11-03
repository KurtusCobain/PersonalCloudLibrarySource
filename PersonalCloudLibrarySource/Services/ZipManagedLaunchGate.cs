using Playnite.SDK.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading;

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
            if (game == null || pluginId == Guid.Empty ||
                game.PluginId != pluginId || string.IsNullOrWhiteSpace(game.GameId))
                return false;
            try
            {
                var key = ZipManagedExtractionService.ReceiptKeyForGame(game.GameId);
                if (string.IsNullOrWhiteSpace(key))
                    return false;

                // Detect a stale managed install even when its File action has
                // been replaced with a relative path, script, or controller.
                // The original managed InstallDirectory still identifies it.
                if (HasManagedAncestor(game.InstallDirectory, key, true))
                    return true;
                return action != null &&
                    HasManagedAncestor(action.Path, key, false);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool HasManagedAncestor(string path,
            string identityKey, bool includeSelf)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                return false;
            var normalized = Path.GetFullPath(path);
            var current = includeSelf ? normalized : Path.GetDirectoryName(normalized);
            for (var i = 0; i < 128 && !string.IsNullOrWhiteSpace(current); i++)
            {
                if (string.Equals(Path.GetFileName(current), identityKey,
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

        public ZipManagedLaunchDecision Evaluate(Guid pluginId, Game game,
            GameAction selectedAction, PersonalCloudLibraryItem currentItem,
            PersonalCloudLibrarySourceSettings settings,
            CacheOwnershipReceiptStore receipts, ZipLauncherApprovalService approvals,
            CancellationToken cancellationToken = default(CancellationToken),
            ZipMutableDataBaselineStore mutableBaseline = null)
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
                    selectedAction == null ||
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
                    approved, receipts, cancellationToken, mutableBaseline) ||
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
