using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;
using System.Windows;

namespace PersonalCloudLibrarySource
{
    public class PersonalCloudLibraryUninstallController : UninstallController
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private readonly IPlayniteAPI playniteApi;
        private readonly PersonalCloudLibraryItem item;
        private readonly PersonalCloudLibrarySourceSettings settings;

        public PersonalCloudLibraryUninstallController(
            IPlayniteAPI playniteApi,
            Game game,
            PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings) : base(game)
        {
            this.playniteApi = playniteApi;
            this.item = item;
            this.settings = settings;
            Name = "Remove cached copy";
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            var launchPath = PersonalCloudLibrarySource.ResolveLaunchPath(item, settings);
            var installDirectory = PersonalCloudLibrarySource.ResolveInstallDirectory(item, settings, launchPath);
            var behavior = NormalizeUninstallBehavior(settings.UninstallBehavior);

            if (string.Equals(behavior, PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior, StringComparison.OrdinalIgnoreCase))
            {
                behavior = ChooseUninstallBehavior();
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
                playniteApi?.Dialogs?.ShowMessage(
                    "Remove cached copy was refused." + Environment.NewLine + Environment.NewLine +
                    "Item: " + item.Title + Environment.NewLine +
                    "Reason: " + refusalReason + Environment.NewLine + Environment.NewLine +
                    "Next: review LocalCacheFolder and uninstall safety settings.",
                    "Personal Cloud Library Source");
                return;
            }

            try
            {
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }
                else if (Directory.Exists(targetPath))
                {
                    Directory.Delete(targetPath, recursive: true);
                }
                else
                {
                    logger.Info($"Personal Cloud Library Source uninstall skipped for {Game.GameId}: cached target does not exist.");
                    playniteApi?.Dialogs?.ShowMessage(
                        "Remove cached copy was skipped because the cached target was not found." + Environment.NewLine + Environment.NewLine +
                        "Item: " + item.Title,
                        "Personal Cloud Library Source");
                    return;
                }

                Game.IsInstalled = false;
                InvokeOnUninstalled();
                logger.Info($"Personal Cloud Library Source uninstall succeeded for {Game.GameId}: deleted {targetPath}.");
                playniteApi?.Dialogs?.ShowMessage(
                    "Remove cached copy completed." + Environment.NewLine + Environment.NewLine +
                    "Item: " + item.Title + Environment.NewLine +
                    "Result: cached local content was removed safely." + Environment.NewLine + Environment.NewLine +
                    "Next: run Update Game Library if you want Playnite to refresh the installed state immediately.",
                    "Personal Cloud Library Source");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Personal Cloud Library Source uninstall failed for {Game.GameId}: targetPath={targetPath}");
                playniteApi?.Dialogs?.ShowMessage(
                    "Remove cached copy failed." + Environment.NewLine + Environment.NewLine +
                    "Item: " + item.Title + Environment.NewLine +
                    "Reason: " + ex.Message,
                    "Personal Cloud Library Source");
            }
        }

        private string ChooseUninstallBehavior()
        {
            if (playniteApi?.Dialogs == null)
            {
                logger.Warn("Personal Cloud Library Source AskEachTime uninstall requested, but no Playnite dialog API was available. Defaulting to RemoveCachedFileOnly.");
                return PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior;
            }

            var result = playniteApi.Dialogs.ShowMessage(
                "Remove the cached install folder? Choose No to remove only the cached launch file.",
                "Personal Cloud Library Source",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                return PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior;
            }

            if (result == MessageBoxResult.No)
            {
                return PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior;
            }

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
    }
}
