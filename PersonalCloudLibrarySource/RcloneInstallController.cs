using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public class RcloneInstallController : InstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly IPlayniteAPI playniteApi;
        private readonly PersonalCloudLibraryItem item;
        private readonly PersonalCloudLibrarySourceSettings settings;
        private readonly RcloneFileCopier rcloneFileCopier;
        private readonly LocalFileCopier localFileCopier;
        private readonly CloudTransferManager transferManager;
        private readonly CloudTransferExecutor transferExecutor;

        public RcloneInstallController(
            IPlayniteAPI playniteApi,
            Game game,
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings,
            RcloneFileCopier rcloneFileCopier,
            LocalFileCopier localFileCopier,
            CloudTransferManager transferManager = null,
            CloudTransferExecutor transferExecutor = null) : base(game)
        {
            this.playniteApi = playniteApi;
            this.item = item;
            this.settings = settings;
            this.rcloneFileCopier = rcloneFileCopier;
            this.localFileCopier = localFileCopier;
            this.transferManager = transferManager;
            this.transferExecutor = transferExecutor;
            Name = "Download to local cache";
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
                var result = sourceType == "directory"
                    ? rcloneFileCopier.CopyRemoteDirectoryToLocalPath(settings, rcloneSourcePath, destinationFolderPath)
                    : rcloneFileCopier.CopyRemoteFileToLocalPath(settings, rcloneSourcePath, destinationFilePath);
                succeeded = result.Succeeded;
                message = result.Message;
                exception = result.Exception;
            }
            else
            {
                var localSourcePath = PersonalCloudLibrarySource.ResolveLocalFolderSourcePath(settings, sourcePath);
                if (transferManager != null && transferExecutor != null)
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
                    ShowSummary(
                        "Download to local cache was cancelled." + System.Environment.NewLine + System.Environment.NewLine +
                        "Item: " + item.Title + System.Environment.NewLine +
                        "Partial files were removed and the game remains uninstalled.");
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

                ShowSummary(
                    "Download to local cache failed." + System.Environment.NewLine + System.Environment.NewLine +
                    "Item: " + item.Title + System.Environment.NewLine +
                    "Source type: " + sourceType + System.Environment.NewLine +
                    "Result: " + (string.IsNullOrWhiteSpace(message) ? "Unknown failure." : message) + System.Environment.NewLine +
                    System.Environment.NewLine +
                    "Next: review the manifest source path and cache settings, then try again.");
                return;
            }

            var expectedLaunchFileExists = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
            logger.Info($"Personal Cloud Library Source download result for {item.Id}: expected launch file exists={expectedLaunchFileExists}.");

            if (!expectedLaunchFileExists)
            {
                logger.Warn($"Personal Cloud Library Source downloaded item {item.Id}, but the expected launch file was not found.");
                ShowSummary(
                    "Download to local cache finished with warnings." + System.Environment.NewLine + System.Environment.NewLine +
                    "Item: " + item.Title + System.Environment.NewLine +
                    "Source type: " + sourceType + System.Environment.NewLine +
                    "Result: files were copied, but the expected launch file was not found." + System.Environment.NewLine +
                    System.Environment.NewLine +
                    "Next: review launchFile, cachePath, and installDirectory in the manifest.");
                return;
            }

            Game.IsInstalled = true;
            Game.InstallDirectory = installDirectory;

            InvokeOnInstalled(new GameInstalledEventArgs(new GameInstallationData
            {
                InstallDirectory = installDirectory
            }));

            logger.Info($"Personal Cloud Library Source downloaded item {item.Id} to local cache.");
            ShowSummary(
                "Download to local cache completed." + System.Environment.NewLine + System.Environment.NewLine +
                "Item: " + item.Title + System.Environment.NewLine +
                "Source type: " + sourceType + System.Environment.NewLine +
                "Installed state: cached locally and launch-ready." + System.Environment.NewLine +
                System.Environment.NewLine +
                "Next: launch the item from Playnite or run Update Game Library if you want Playnite to refresh its view.");
        }

        private void ShowSummary(string message)
        {
            playniteApi?.Dialogs?.ShowMessage(message, "Personal Cloud Library Source");
        }
    }
}
