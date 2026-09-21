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

        public PersonalCloudLibraryUninstallController(
            IPlayniteAPI playniteApi,
            Game game,
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings,
            GameWorkflowNotificationService workflowNotifications = null,
            SafeCacheDeletionExecutor deletionExecutor = null) : base(game)
        {
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

            if (string.Equals(behavior, PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior, StringComparison.OrdinalIgnoreCase))
            {
                behavior = ChooseUninstallBehavior();
                if (string.IsNullOrWhiteSpace(behavior))
                {
                    workflowNotifications.Failure(
                        "uninstall",
                        Game.GameId,
                        PclsResources.Get(
                            "LOCPLSUninstallBehaviorRequired",
                            "Remove cached copy needs a deterministic uninstall behavior. Open plugin settings in Desktop mode, choose file-only or install-folder removal, then try again."));
                    return;
                }
            }

            var requestedTargetPath = ResolveTargetPath(behavior, launchPath, installDirectory);
            string refusalReason;
            var targetPath = PersonalCloudLibrarySource.ResolveSafeUninstallTarget(settings, requestedTargetPath, out refusalReason);
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
                return;
            }

            try
            {
                var deletion = deletionExecutor.Delete(
                    settings.LocalCacheFolder,
                    targetPath,
                    settings.AllowUninstallOutsideCacheFolder);
                if (!deletion.Allowed)
                {
                    logger.Info($"Personal Cloud Library Source uninstall skipped for {Game.GameId}: {deletion.Reason}.");
                    workflowNotifications.Warning("uninstall", Game.GameId, PclsResources.Format(
                        "LOCPLSUninstallSkippedNotification",
                        "Remove cached copy was skipped.{0}{0}Item: {1}{0}Reason: {2}",
                        Environment.NewLine,
                        item.Title,
                        deletion.Reason));
                    return;
                }

                var postDeletionState = new LibraryItemStateApplicator().Reconcile(
                    Game,
                    item,
                    launchPath,
                    installDirectory,
                    settings.TreatMissingFilesAsUninstalled);
                if (!postDeletionState.IsInstalled)
                {
                    InvokeOnUninstalled();
                }
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
            }
        }

        private string ChooseUninstallBehavior()
        {
            logger.Warn("Personal Cloud Library Source AskEachTime uninstall is unavailable in a standard controller. Select a deterministic uninstall behavior in plugin settings.");
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
            return string.IsNullOrWhiteSpace(behavior)
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
