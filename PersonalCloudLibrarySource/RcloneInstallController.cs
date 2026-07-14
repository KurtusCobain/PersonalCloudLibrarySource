using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public class RcloneInstallController : InstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly PersonalCloudLibraryItem item;
        private readonly PersonalCloudLibrarySourceSettings settings;
        private readonly RcloneFileCopier rcloneFileCopier;
        private readonly LocalFileCopier localFileCopier;
        private readonly CloudTransferManager transferManager;
        private readonly CloudTransferExecutor transferExecutor;
        private readonly TransferQueueService transferQueue;
        private readonly GameWorkflowNotificationService workflowNotifications;

        public RcloneInstallController(
            IPlayniteAPI playniteApi,
            Game game,
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings,
            RcloneFileCopier rcloneFileCopier,
            LocalFileCopier localFileCopier,
            CloudTransferManager transferManager = null,
            CloudTransferExecutor transferExecutor = null,
            TransferQueueService transferQueue = null,
            GameWorkflowNotificationService workflowNotifications = null) : base(game)
        {
            this.item = item;
            this.settings = settings;
            this.rcloneFileCopier = rcloneFileCopier;
            this.localFileCopier = localFileCopier;
            this.transferManager = transferManager;
            this.transferExecutor = transferExecutor;
            this.transferQueue = transferQueue;
            this.workflowNotifications = workflowNotifications ?? CreateNotifications(playniteApi);
            Name = PclsResources.Get("LOCPLSInstallControllerName", "Download to local cache");
        }

        public override void Install(InstallActionArgs args)
        {
            var sourceType = PersonalCloudLibrarySource.GetItemSourceType(item);
            var launchPath = PersonalCloudLibrarySource.ResolveLaunchPath(item, settings);
            var installDirectory = PersonalCloudLibrarySource.ResolveInstallDirectory(item, settings, launchPath);
            var destinationFilePath = PersonalCloudLibrarySource.ResolveDownloadDestinationFilePath(item, settings, launchPath);
            var destinationFolderPath = PersonalCloudLibrarySource.ResolveDownloadDestinationFolder(item, settings, launchPath);
            var sourcePath = PersonalCloudLibrarySource.GetItemSourcePath(item);

            logger.Info($"Personal Cloud Library Source downloading item {item.Id} as {sourceType}.");
            var providerType = PersonalCloudLibrarySource.GetProviderType(settings);
            var succeeded = false;
            var cancelled = false;
            string message = null;
            System.Exception exception = null;

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, System.StringComparison.OrdinalIgnoreCase))
            {
                var rcloneSourcePath = PersonalCloudLibrarySource.ResolveRcloneSourcePath(settings, sourcePath);
                if (transferQueue != null)
                {
                    var destinationPath = sourceType == "directory"
                        ? destinationFolderPath
                        : destinationFilePath;
                    var job = transferQueue.EnqueueRclone(
                        Game.Id,
                        string.IsNullOrWhiteSpace(item.Title) ? Game.Name : item.Title,
                        rcloneSourcePath,
                        destinationPath,
                        providerType,
                        sourceType == "directory",
                        settings);
                    var result = transferQueue.GetCompletion(job.Id).GetAwaiter().GetResult();
                    succeeded = result.Succeeded;
                    cancelled = result.Cancelled;
                    message = result.Message;
                    exception = result.Exception;
                }
                else if (transferManager != null && transferExecutor != null)
                {
                    var destinationPath = sourceType == "directory"
                        ? destinationFolderPath
                        : destinationFilePath;
                    var job = transferManager.Enqueue(
                        Game.Id,
                        string.IsNullOrWhiteSpace(item.Title) ? Game.Name : item.Title,
                        rcloneSourcePath,
                        destinationPath,
                        providerType,
                        sourceType == "directory");
                    var result = transferExecutor.ExecuteRclone(job.Id, settings);
                    succeeded = result.Succeeded;
                    cancelled = result.Cancelled;
                    message = result.Message;
                    exception = result.Exception;
                }
                else
                {
                    var result = sourceType == "directory"
                        ? rcloneFileCopier.CopyRemoteDirectoryToLocalPath(settings, rcloneSourcePath, destinationFolderPath)
                        : rcloneFileCopier.CopyRemoteFileToLocalPath(settings, rcloneSourcePath, destinationFilePath);
                    succeeded = result.Succeeded;
                    message = result.Message;
                    exception = result.Exception;
                }
            }
            else
            {
                var localSourcePath = PersonalCloudLibrarySource.ResolveLocalFolderSourcePath(settings, sourcePath);
                if (transferQueue != null)
                {
                    var destinationPath = sourceType == "directory"
                        ? destinationFolderPath
                        : destinationFilePath;
                    var job = transferQueue.EnqueueLocal(
                        Game.Id,
                        string.IsNullOrWhiteSpace(item.Title) ? Game.Name : item.Title,
                        localSourcePath,
                        destinationPath,
                        providerType,
                        sourceType == "directory");
                    var result = transferQueue.GetCompletion(job.Id).GetAwaiter().GetResult();
                    succeeded = result.Succeeded;
                    cancelled = result.Cancelled;
                    message = result.Message;
                    exception = result.Exception;
                }
                else if (transferManager != null && transferExecutor != null)
                {
                    var destinationPath = sourceType == "directory"
                        ? destinationFolderPath
                        : destinationFilePath;
                    var job = transferManager.Enqueue(
                        Game.Id,
                        string.IsNullOrWhiteSpace(item.Title) ? Game.Name : item.Title,
                        localSourcePath,
                        destinationPath,
                        providerType,
                        sourceType == "directory");
                    var result = transferExecutor.ExecuteLocal(job.Id, sourceType == "directory");
                    succeeded = result.Succeeded;
                    cancelled = result.Cancelled;
                    message = result.Message;
                    exception = result.Exception;
                }
                else
                {
                    var result = sourceType == "directory"
                        ? localFileCopier.CopyDirectoryToLocalPath(localSourcePath, destinationFolderPath)
                        : localFileCopier.CopyFileToLocalPath(localSourcePath, destinationFilePath);
                    succeeded = result.Succeeded;
                    message = result.Message;
                    exception = result.Exception;
                }
            }

            if (!succeeded)
            {
                if (cancelled)
                {
                    logger.Info($"Personal Cloud Library Source transfer cancelled for item {item.Id}.");
                    workflowNotifications.Warning("install", Game.GameId, PclsResources.Format(
                        "LOCPLSInstallCancelledNotification",
                        "Download to local cache was cancelled.{0}{0}Item: {1}{0}Partial files were removed and the game remains uninstalled.",
                        System.Environment.NewLine,
                        item.Title));
                    return;
                }

                if (exception != null)
                {
                    logger.Error(exception, $"Personal Cloud Library Source failed to download item {item.Id}: {message}");
                }
                else
                {
                    logger.Error($"Personal Cloud Library Source failed to download item {item.Id}: {message}");
                }

                workflowNotifications.Failure("install", Game.GameId, PclsResources.Format(
                    "LOCPLSInstallFailedNotification",
                    "Download to local cache failed.{0}{0}Item: {1}{0}Source type: {2}{0}Result: {3}{0}{0}Next: review the manifest source path and cache settings, then try again.",
                    System.Environment.NewLine,
                    item.Title,
                    sourceType,
                    string.IsNullOrWhiteSpace(message)
                        ? PclsResources.Get("LOCPLSUnknownFailure", "Unknown failure.")
                        : message));
                return;
            }

            var itemState = new LibraryItemStateResolver().Resolve(
                item,
                launchPath,
                installDirectory,
                settings.TreatMissingFilesAsUninstalled);
            logger.Info($"Personal Cloud Library Source download result for {item.Id}: cached={itemState.IsCached}; play action available={itemState.HasPlayAction}.");

            if (!itemState.IsCached)
            {
                logger.Warn($"Personal Cloud Library Source downloaded item {item.Id}, but the expected launch file was not found.");
                workflowNotifications.Warning("install", Game.GameId, PclsResources.Format(
                    "LOCPLSInstallWarningNotification",
                    "Download to local cache finished with warnings.{0}{0}Item: {1}{0}Source type: {2}{0}Result: files were copied, but the expected launch file was not found.{0}{0}Next: review {3}, {4}, and {5} in the manifest.",
                    System.Environment.NewLine,
                    item.Title,
                    sourceType,
                    PclsResources.LaunchFileIdentifier,
                    PclsResources.CachePathIdentifier,
                    PclsResources.InstallDirectoryIdentifier));
                return;
            }

            new LibraryItemStateApplicator().Apply(Game, itemState);

            InvokeOnInstalled(new GameInstalledEventArgs(new GameInstallationData
            {
                InstallDirectory = installDirectory
            }));

            logger.Info($"Personal Cloud Library Source downloaded item {item.Id} to local cache.");
            workflowNotifications.Success("install", Game.GameId, PclsResources.Format(
                "LOCPLSInstallCompletedNotification",
                "Download to local cache completed.{0}{0}Item: {1}{0}Source type: {2}{0}Installed state: cached locally{3}{0}{0}Next: launch the item from Playnite or run Update Game Library if you want Playnite to refresh its view.",
                System.Environment.NewLine,
                item.Title,
                sourceType,
                itemState.HasPlayAction
                    ? PclsResources.Get("LOCPLSInstallLaunchReadySuffix", " and launch-ready.")
                    : "."));
        }

        private static GameWorkflowNotificationService CreateNotifications(IPlayniteAPI playniteApi)
        {
            var sink = new PlayniteGameWorkflowNotificationSink(playniteApi?.Notifications);
            return playniteApi == null
                ? new GameWorkflowNotificationService(sink)
                : new GameWorkflowNotificationService(
                    sink,
                    new PlayniteImportUiDispatcher(playniteApi),
                    ex => logger.Warn(ex, "Personal Cloud Library Source could not publish an install notification."));
        }

    }
}
