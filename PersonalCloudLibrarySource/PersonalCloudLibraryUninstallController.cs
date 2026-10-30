using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public class PersonalCloudLibraryUninstallController : UninstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly PersonalCloudLibraryItem item;
        private readonly PersonalCloudLibrarySourceSettings settings;
        private readonly SafeCacheDeletionExecutor deletionExecutor;
        private readonly GameWorkflowNotificationService workflowNotifications;
        private readonly bool hasPlayniteHost;

        public PersonalCloudLibraryUninstallController(
            IPlayniteAPI playniteApi,
            Game game,
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings,
            GameWorkflowNotificationService workflowNotifications = null,
            SafeCacheDeletionExecutor deletionExecutor = null) : base(game)
        {
            hasPlayniteHost = playniteApi != null;
            this.item = item;
            this.settings = settings;
            this.workflowNotifications = workflowNotifications ?? CreateNotifications(playniteApi);
            this.deletionExecutor = deletionExecutor ?? new SafeCacheDeletionExecutor();
            Name = PclsResources.Get("LOCPLSUninstallControllerName", "Remove cached copy");
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            var launchPath = PersonalCloudLibrarySource.ResolveLaunchPath(item, settings);
            var installDirectory = PersonalCloudLibrarySource.ResolveInstallDirectory(item, settings, launchPath);
            var behavior = NormalizeUninstallBehavior(settings.UninstallBehavior);

            var requestedTargetPath = ResolveTargetPath(behavior, launchPath, installDirectory);
            string refusalReason;
            var targetPath = PersonalCloudLibrarySource.ResolveSafeUninstallTarget(settings, requestedTargetPath, out refusalReason);
            if (string.IsNullOrWhiteSpace(refusalReason))
                refusalReason = GetLocalSourceProtectionRefusal(targetPath);
            var insideCache = PersonalCloudLibrarySource.IsPathInsideCacheFolder(targetPath, settings.LocalCacheFolder);

            logger.Info(
                $"Personal Cloud Library Source uninstall requested: gameId={Game.GameId}; title={item.Title}; launchPath={launchPath}; installDirectory={installDirectory}; behavior={behavior}; requestedTargetPath={requestedTargetPath}; targetPath={targetPath}; insideCache={insideCache}; refusalReason={refusalReason}");

            if (!string.IsNullOrWhiteSpace(refusalReason))
            {
                logger.Warn($"Personal Cloud Library Source refused uninstall for {Game.GameId}: {refusalReason}");
                workflowNotifications.Failure("uninstall", Game.GameId, PclsResources.Format(
                    "LOCPLSUninstallRefusedNotification",
                    "Remove cached copy was refused.{0}{0}Item: {1}{0}Reason: {2}{0}{0}Next: review {3} and uninstall safety settings.",
                    Environment.NewLine,
                    item.Title,
                    refusalReason,
                    nameof(PersonalCloudLibrarySourceSettings.LocalCacheFolder)));
                // Playnite has no uninstallation-cancel event; its exception path
                // resets IsUninstalling and disposes this controller.
                throw new InvalidOperationException("Cached-copy deletion refused: " + refusalReason);
            }

            try
            {
                var deletion = deletionExecutor.Delete(
                    settings.LocalCacheFolder,
                    targetPath,
                    settings.AllowUninstallOutsideCacheFolder);
                if (!deletion.Allowed)
                {
                    throw new InvalidOperationException("Cached-copy deletion refused: " + deletion.Reason);
                }

                var postDeletionState = new LibraryItemStateApplicator().Reconcile(
                    Game,
                    item,
                    launchPath,
                    installDirectory,
                    settings.TreatMissingFilesAsUninstalled);
                // A completed removal must notify the host even when the
                // optional missing-file status preference remains enabled.
                if (hasPlayniteHost)
                    InvokeOnUninstalled();
                logger.Info($"Personal Cloud Library Source uninstall succeeded for {Game.GameId}: deleted {targetPath}.");
                workflowNotifications.Success("uninstall", Game.GameId, PclsResources.Format(
                    "LOCPLSUninstallCompletedNotification",
                    "Remove cached copy completed.{0}{0}Item: {1}{0}Result: the requested cached target was removed safely.{0}Cached state: {2}{0}{0}Next: run Update Game Library if you want Playnite to refresh the installed state immediately.",
                    Environment.NewLine,
                    item.Title,
                    postDeletionState.IsCached
                        ? PclsResources.Get("LOCPLSUninstallOtherContentRemains", "other cached content remains.")
                        : PclsResources.Get("LOCPLSUninstallNoContentRemains", "no cached content remains.")));
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Personal Cloud Library Source uninstall failed for {Game.GameId}: targetPath={targetPath}");
                workflowNotifications.Failure("uninstall", Game.GameId, PclsResources.Format(
                    "LOCPLSUninstallFailedNotification",
                    "Remove cached copy failed.{0}{0}Item: {1}{0}Reason: {2}",
                    Environment.NewLine,
                    item.Title,
                    ex.Message));
                throw;
            }
        }

        // Even the explicit outside-cache option must never authorize deletion
        // of the source item, an ancestor of it, or the managed local library root.
        private string GetLocalSourceProtectionRefusal(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                return "uninstall target could not be verified";

            var provider = PersonalCloudLibrarySource.GetProviderType(settings);
            if (string.Equals(provider, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var itemSource = PersonalCloudLibrarySource.GetItemSourcePath(item);
            var resolved = new SourcePathResolver().ResolveLocal(settings, itemSource);
            if (!resolved.Succeeded)
            {
                return settings.AllowUninstallOutsideCacheFolder &&
                       !PathBoundary.IsContained(settings.LocalCacheFolder, targetPath)
                    ? "cannot verify the original local source path for outside-cache uninstall"
                    : string.Empty;
            }

            if (PathBoundary.IsContained(resolved.Path, targetPath) ||
                PathBoundary.IsContained(targetPath, resolved.Path))
                return "uninstall target overlaps the original source item";

            if (string.Equals(provider, PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                StringComparison.OrdinalIgnoreCase) &&
                PathBoundary.IsContained(settings.LocalLibraryRoot, targetPath))
                return "uninstall target is within the original library source folder";

            return string.Empty;
        }

        private static string ResolveTargetPath(string behavior, string launchPath, string installDirectory)
        {
            if (string.Equals(behavior, PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior, StringComparison.OrdinalIgnoreCase))
            {
                return launchPath;
            }

            if (string.Equals(behavior, PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior, StringComparison.OrdinalIgnoreCase))
            {
                return installDirectory;
            }

            return string.Empty;
        }

        private static string NormalizeUninstallBehavior(string behavior)
        {
            return string.IsNullOrWhiteSpace(behavior) ||
                   string.Equals(behavior, PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior,
                       StringComparison.OrdinalIgnoreCase)
                ? PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior
                : behavior;
        }

        private static GameWorkflowNotificationService CreateNotifications(IPlayniteAPI playniteApi)
        {
            var sink = new PlayniteGameWorkflowNotificationSink(playniteApi?.Notifications);
            return playniteApi == null
                ? new GameWorkflowNotificationService(sink)
                : new GameWorkflowNotificationService(
                    sink,
                    new PlayniteImportUiDispatcher(playniteApi),
                    ex => logger.Warn(ex, "Personal Cloud Library Source could not publish an uninstall notification."));
        }
    }
}
