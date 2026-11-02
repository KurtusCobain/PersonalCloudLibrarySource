using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public class RcloneInstallController : InstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly PersonalCloudLibraryItem item;
        private readonly PersonalCloudLibrarySourceSettings settings;
        private readonly TransferQueueService transferQueue;
        private readonly GameWorkflowNotificationService workflowNotifications;
        private readonly IImportUiDispatcher uiDispatcher;
        private readonly CacheOwnershipReceiptStore ownershipReceipts;
        private readonly string ownershipReceiptKey;
        private readonly CancellationTokenSource inventoryCancellation = new CancellationTokenSource();
        private Guid activeJobId;
        private volatile bool disposed;
        private readonly bool hasPlayniteHost;

        public RcloneInstallController(
            IPlayniteAPI playniteApi,
            Game game,
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings,
            TransferQueueService transferQueue,
            GameWorkflowNotificationService workflowNotifications = null,
            CacheOwnershipReceiptStore ownershipReceipts = null,
            string ownershipReceiptKey = null) : base(game)
        {
            this.item = item;
            this.settings = settings;
            this.transferQueue = transferQueue ?? throw new ArgumentNullException(nameof(transferQueue));
            this.ownershipReceipts = ownershipReceipts;
            // Existing calls retain the original manifest item key. A new-profile
            // caller must supply its namespaced Playnite game ID explicitly.
            if (string.IsNullOrWhiteSpace(item?.Id))
                throw new ArgumentException("A valid source item is required.", nameof(item));
            if (ownershipReceiptKey != null &&
                !string.Equals(ownershipReceiptKey, game?.GameId, StringComparison.Ordinal))
                throw new ArgumentException("New-source receipt identity must match GameId.",
                    nameof(ownershipReceiptKey));
            this.ownershipReceiptKey = ownershipReceiptKey ?? item.Id;
            this.workflowNotifications = workflowNotifications ?? CreateNotifications(playniteApi);
            hasPlayniteHost = playniteApi != null;
            uiDispatcher = playniteApi == null
                ? (IImportUiDispatcher)new ImmediateGameWorkflowUiDispatcher()
                : new PlayniteImportUiDispatcher(playniteApi);
            Name = PclsResources.Get("LOCPLSInstallControllerName", "Download to local cache");
        }

        public override void Install(InstallActionArgs args)
        {
            if (disposed) return;
            try
            {
                var sourceType = PersonalCloudLibrarySource.GetItemSourceType(item);
                var launchPath = PersonalCloudLibrarySource.ResolveLaunchPath(item, settings);
                var installDirectory = PersonalCloudLibrarySource.ResolveInstallDirectory(item, settings, launchPath);
                var destinationFilePath = PersonalCloudLibrarySource.ResolveDownloadDestinationFilePath(item, settings, launchPath);
                var destinationFolderPath = PersonalCloudLibrarySource.ResolveDownloadDestinationFolder(item, settings, launchPath);
                var sourcePath = PersonalCloudLibrarySource.GetItemSourcePath(item);
                // Defense in depth: do not trust a manifest-supplied cache destination
                // even if an install controller was constructed outside the normal policy.
                var intendedDestination = string.Equals(sourceType, "directory", StringComparison.OrdinalIgnoreCase)
                    ? destinationFolderPath : destinationFilePath;
                if (string.IsNullOrWhiteSpace(intendedDestination) ||
                    !PathBoundary.IsContained(settings?.LocalCacheFolder, intendedDestination) ||
                    string.Equals(
                        PathBoundary.Normalize(settings.LocalCacheFolder),
                        PathBoundary.Normalize(intendedDestination),
                        StringComparison.OrdinalIgnoreCase))
                {
                    workflowNotifications.Failure("install", Game.GameId,
                        "Download blocked: the destination must be inside the configured local cache folder.");
                    SignalCancelledIfHosted();
                    return;
                }

                logger.Info($"Personal Cloud Library Source queueing item {item.Id} as {sourceType}.");
                var providerType = PersonalCloudLibrarySource.GetProviderType(settings);
                var destinationPath = sourceType == "directory"
                    ? destinationFolderPath
                    : destinationFilePath;
                var job = string.Equals(providerType,
                    PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                    StringComparison.OrdinalIgnoreCase)
                    ? transferQueue.EnqueueRclone(
                        Game.Id, string.IsNullOrWhiteSpace(item.Title) ? Game.Name : item.Title,
                        PersonalCloudLibrarySource.ResolveRcloneSourcePath(settings, sourcePath),
                        destinationPath, providerType, sourceType == "directory", settings)
                    : transferQueue.EnqueueLocal(
                        Game.Id, string.IsNullOrWhiteSpace(item.Title) ? Game.Name : item.Title,
                        PersonalCloudLibrarySource.ResolveLocalFolderSourcePath(settings, sourcePath),
                        destinationPath, providerType, sourceType == "directory");
                activeJobId = job.Id;
                ObserveCompletionAsync(job.Id, sourceType, launchPath, installDirectory);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "PCLS could not start install for " + (item?.Id ?? "unknown"));
                workflowNotifications.Failure("install", Game.GameId,
                    "Could not start download: " + ex.Message);
                SignalCancelledIfHosted();
            }
        }

        private async void ObserveCompletionAsync(
            Guid jobId, string sourceType, string launchPath, string installDirectory)
        {
            try
            {
                // Never synchronously wait on Playnite's calling (UI) thread.
                var result = await transferQueue.GetCompletion(jobId).ConfigureAwait(false);
                if (disposed) return;
                // Hash directory transfers away from Playnite's UI thread.
                // Failed inventory issuance never converts a failed transfer to
                // success or grants deletion authority.
                if (result.Succeeded && ownershipReceipts != null &&
                    string.Equals(sourceType, "directory", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var destination = PersonalCloudLibrarySource.ResolveDownloadDestinationFolder(
                            item, settings, launchPath);
                        await Task.Run(() => ownershipReceipts.RecordCompletedWithInventory(
                            settings.LocalCacheFolder, ownershipReceiptKey, destination,
                            inventoryCancellation.Token)).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        logger.Info("PCLS directory inventory receipt cancelled during controller disposal.");
                    }
                    catch (Exception receiptError)
                    {
                        logger.Warn(receiptError, "PCLS could not record directory inventory receipt.");
                    }
                }
                if (disposed) return;
                uiDispatcher.Invoke(() =>
                {
                    if (disposed) return;
                    activeJobId = Guid.Empty;
                    HandleCompletion(result, sourceType, launchPath, installDirectory);
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "PCLS install completion observer failed for " + item.Id);
                if (disposed) return;
                try
                {
                    uiDispatcher.Invoke(() =>
                    {
                        if (disposed) return;
                        activeJobId = Guid.Empty;
                        workflowNotifications.Failure("install", Game.GameId,
                            "Download failed: " + ex.Message);
                        SignalCancelledIfHosted();
                    });
                }
                catch (Exception dispatchError)
                {
                    logger.Error(dispatchError, "PCLS could not report installation completion.");
                }
            }
        }

        private void HandleCompletion(CloudTransferExecutionResult result,
            string sourceType, string launchPath, string installDirectory)
        {
            var succeeded = result.Succeeded;
            var cancelled = result.Cancelled;
            var message = result.Message;
            var exception = result.Exception;
            if (!succeeded)
            {
                if (cancelled)
                {
                    logger.Info($"Personal Cloud Library Source transfer cancelled for item {item.Id}.");
                    SignalCancelledIfHosted();
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

                SignalCancelledIfHosted();
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
                SignalCancelledIfHosted();
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

            // A successful queue result follows the adapters' no-overwrite staging
            // and final move. Receipts are issued only for that terminal success,
            // never while merely scanning a pre-existing legacy cache.
            if (ownershipReceipts != null &&
                !string.Equals(sourceType, "directory", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var destination = string.Equals(sourceType, "directory",
                        StringComparison.OrdinalIgnoreCase)
                        ? PersonalCloudLibrarySource.ResolveDownloadDestinationFolder(item, settings, launchPath)
                        : PersonalCloudLibrarySource.ResolveDownloadDestinationFilePath(item, settings, launchPath);
                    ownershipReceipts.RecordCompleted(settings.LocalCacheFolder, ownershipReceiptKey, destination);
                }
                catch (Exception ex)
                {
                    // Installation remains usable; failure to record provenance
                    // must NEVER silently grant the right to recursively delete.
                    logger.Warn(ex, "PCLS could not persist installation ownership receipt.");
                }
            }

            new LibraryItemStateApplicator().Apply(Game, itemState);

            if (hasPlayniteHost)
            {
                InvokeOnInstalled(new GameInstalledEventArgs(new GameInstallationData
                {
                    InstallDirectory = installDirectory
                }));
            }

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

        private void SignalCancelledIfHosted()
        {
            // Test controllers have no Playnite synchronization context.
            // The SDK event helper uses that context, which only the host supplies.
            if (hasPlayniteHost)
                InvokeOnInstallationCancelled(new GameInstallationCancelledEventArgs());
        }

        public override void Dispose()
        {
            disposed = true;
            inventoryCancellation.Cancel();
            if (activeJobId != Guid.Empty)
            {
                try { transferQueue.Cancel(activeJobId); }
                catch (Exception ex) { logger.Warn(ex, "PCLS could not cancel a disposed install controller."); }
                activeJobId = Guid.Empty;
            }
            base.Dispose();
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
