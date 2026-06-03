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

        public RcloneInstallController(
            Game game,
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings,
            RcloneFileCopier rcloneFileCopier,
            LocalFileCopier localFileCopier) : base(game)
        {
            this.item = item;
            this.settings = settings;
            this.rcloneFileCopier = rcloneFileCopier;
            this.localFileCopier = localFileCopier;
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
                var result = sourceType == "directory"
                    ? localFileCopier.CopyDirectoryToLocalPath(localSourcePath, destinationFolderPath)
                    : localFileCopier.CopyFileToLocalPath(localSourcePath, destinationFilePath);
                succeeded = result.Succeeded;
                message = result.Message;
                exception = result.Exception;
            }

            if (!succeeded)
            {
                if (exception != null)
                {
                    logger.Error(exception, $"Personal Cloud Library Source failed to download item {item.Id}: {message}");
                }
                else
                {
                    logger.Error($"Personal Cloud Library Source failed to download item {item.Id}: {message}");
                }

                return;
            }

            var expectedLaunchFileExists = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
            logger.Info($"Personal Cloud Library Source download result for {item.Id}: expected launch file exists={expectedLaunchFileExists}.");

            if (!expectedLaunchFileExists)
            {
                logger.Warn($"Personal Cloud Library Source downloaded item {item.Id}, but the expected launch file was not found.");
                return;
            }

            Game.IsInstalled = true;
            Game.InstallDirectory = installDirectory;

            InvokeOnInstalled(new GameInstalledEventArgs(new GameInstallationData
            {
                InstallDirectory = installDirectory
            }));

            logger.Info($"Personal Cloud Library Source downloaded item {item.Id} to local cache.");
        }
    }
}
