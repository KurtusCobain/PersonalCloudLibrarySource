from pathlib import Path


def replace_once(text, old, new, label):
    if new in text:
        return text
    if old not in text:
        raise SystemExit(f"Patch target missing for {label}: {old}")
    return text.replace(old, new, 1)


navigation_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.Navigation.cs")
navigation = navigation_path.read_text(encoding="utf-8-sig")
navigation = replace_once(
    navigation,
    """                    GetTransferManager(),
                    GetTransferExecutor())""",
    """                    GetTransferManager(),
                    GetTransferExecutor(),
                    settings.Settings)""",
    "dashboard rclone settings",
)
navigation_path.write_text(navigation, encoding="utf-8-sig")

commands_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.GameCommands.cs")
commands = commands_path.read_text(encoding="utf-8-sig")
commands = replace_once(
    commands,
    """            var previous = manager.GetLatestRetryableJobForGame(target.Game.Id);
            if (previous == null || string.Equals(
                previous.ProviderType,
                PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var retry = manager.Retry(previous.Id);
            Task.Run(() => GetTransferExecutor().ExecuteLocal(retry.Id, retry.IsDirectory));""",
    """            var previous = manager.GetLatestRetryableJobForGame(target.Game.Id);
            if (previous == null)
            {
                return;
            }

            var retry = manager.Retry(previous.Id);
            if (string.Equals(
                previous.ProviderType,
                PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                Task.Run(() => GetTransferExecutor().ExecuteRclone(retry.Id, settings.Settings));
                return;
            }

            Task.Run(() => GetTransferExecutor().ExecuteLocal(retry.Id, retry.IsDirectory));""",
    "game menu rclone retry",
)
commands_path.write_text(commands, encoding="utf-8-sig")

controller_path = Path("PersonalCloudLibrarySource/RcloneInstallController.cs")
controller = controller_path.read_text(encoding="utf-8-sig")
controller = replace_once(
    controller,
    """            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, System.StringComparison.OrdinalIgnoreCase))
            {
                var rcloneSourcePath = PersonalCloudLibrarySource.ResolveRcloneSourcePath(settings, sourcePath);
                var result = sourceType == \"directory\"
                    ? rcloneFileCopier.CopyRemoteDirectoryToLocalPath(settings, rcloneSourcePath, destinationFolderPath)
                    : rcloneFileCopier.CopyRemoteFileToLocalPath(settings, rcloneSourcePath, destinationFilePath);
                succeeded = result.Succeeded;
                message = result.Message;
                exception = result.Exception;
            }
            else""",
    """            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, System.StringComparison.OrdinalIgnoreCase))
            {
                var rcloneSourcePath = PersonalCloudLibrarySource.ResolveRcloneSourcePath(settings, sourcePath);
                if (transferManager != null && transferExecutor != null)
                {
                    var destinationPath = sourceType == \"directory\"
                        ? destinationFolderPath
                        : destinationFilePath;
                    var job = transferManager.Enqueue(
                        Game.Id,
                        string.IsNullOrWhiteSpace(item.Title) ? Game.Name : item.Title,
                        rcloneSourcePath,
                        destinationPath,
                        providerType,
                        sourceType == \"directory\");
                    var result = transferExecutor.ExecuteRclone(job.Id, settings);
                    succeeded = result.Succeeded;
                    cancelled = result.Cancelled;
                    message = result.Message;
                    exception = result.Exception;
                }
                else
                {
                    var result = sourceType == \"directory\"
                        ? rcloneFileCopier.CopyRemoteDirectoryToLocalPath(settings, rcloneSourcePath, destinationFolderPath)
                        : rcloneFileCopier.CopyRemoteFileToLocalPath(settings, rcloneSourcePath, destinationFilePath);
                    succeeded = result.Succeeded;
                    message = result.Message;
                    exception = result.Exception;
                }
            }
            else""",
    "live rclone queue integration",
)
controller_path.write_text(controller, encoding="utf-8-sig")
