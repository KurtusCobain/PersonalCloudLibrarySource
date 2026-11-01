using System;
using System.IO;
using System.Linq;
using System.Collections.ObjectModel;
using Playnite.SDK.Models;

namespace PersonalCloudLibrarySource
{
    public sealed class LibraryItemState
    {
        public bool IsCached { get; set; }
        public bool IsInstalled { get; set; }
        public bool HasPlayAction { get; set; }
        public string PlayPath { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        // Do not confuse the working directory with Playnite's scanned install folder.
        public string PlayniteInstallDirectory { get; set; } = string.Empty;
        public ulong? OwnedFileSize { get; set; }
    }

    public sealed class LibraryItemStateResolver
    {
        public LibraryItemState Resolve(PersonalCloudLibraryItem item, string launchPath, string installDirectory)
        {
            return Resolve(item, launchPath, installDirectory, true);
        }

        public LibraryItemState Resolve(
            PersonalCloudLibraryItem item,
            string launchPath,
            string installDirectory,
            bool treatMissingFilesAsUninstalled)
        {
            var launchExists = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
            var implicitSingleFile = item != null && !PersonalCloudLibrarySource.IsDirectoryItem(item) &&
                string.IsNullOrWhiteSpace(item.InstallDirectory);
            var directoryExists = item != null && PersonalCloudLibrarySource.IsDirectoryItem(item) &&
                !string.IsNullOrWhiteSpace(installDirectory) && Directory.Exists(installDirectory);
            return new LibraryItemState
            {
                IsCached = launchExists || directoryExists,
                IsInstalled = launchExists || directoryExists || !treatMissingFilesAsUninstalled,
                HasPlayAction = launchExists,
                PlayPath = launchExists ? launchPath : string.Empty,
                WorkingDirectory = launchExists || directoryExists ? installDirectory ?? string.Empty : string.Empty,
                // Playnite scans InstallDirectory recursively to compute InstallSize.
                // For an implicit file-type install, its parent can be a shared cache folder.
                // Keep the real parent only in the launch action's WorkingDir.
                PlayniteInstallDirectory = implicitSingleFile ? string.Empty : installDirectory ?? string.Empty,
                OwnedFileSize = implicitSingleFile && launchExists ? TryGetFileLength(launchPath) : null
            };
        }

        public LibraryItemState ResolveGame(Game game, bool treatMissingFilesAsUninstalled)
        {
            if (game == null) return new LibraryItemState();
            var playAction = game.GameActions?.FirstOrDefault(action => action != null && action.IsPlayAction);
            var launchPath = playAction?.Path ?? string.Empty;
            var launchExists = !string.IsNullOrWhiteSpace(launchPath) && File.Exists(launchPath);
            return new LibraryItemState
            {
                IsCached = launchExists,
                IsInstalled = launchExists || !treatMissingFilesAsUninstalled,
                HasPlayAction = launchExists,
                PlayPath = launchExists ? launchPath : string.Empty,
                WorkingDirectory = launchExists ? game.InstallDirectory ?? string.Empty : string.Empty,
                PlayniteInstallDirectory = game.InstallDirectory ?? string.Empty,
                OwnedFileSize = game.InstallSize
            };
        }

        private static ulong? TryGetFileLength(string path)
        {
            try
            {
                return File.Exists(path) ? (ulong?)new FileInfo(path).Length : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    public sealed class LibraryItemStateApplicator
    {
        public LibraryItemState Reconcile(
            Game game,
            PersonalCloudLibraryItem item,
            string launchPath,
            string installDirectory,
            bool treatMissingFilesAsUninstalled)
        {
            var state = new LibraryItemStateResolver().Resolve(
                item,
                launchPath,
                installDirectory,
                treatMissingFilesAsUninstalled);
            Apply(game, state);
            return state;
        }

        public void Apply(Game game, LibraryItemState state)
        {
            if (game == null || state == null) return;
            game.IsInstalled = state.IsInstalled;
            game.InstallDirectory = state.IsCached ? state.PlayniteInstallDirectory : string.Empty;
            if (state.IsCached && state.OwnedFileSize.HasValue)
                game.InstallSize = state.OwnedFileSize.Value;
            else if (!state.IsCached)
                game.InstallSize = null;
            var actions = new ObservableCollection<GameAction>(
                game.GameActions?.Where(action => action != null && !action.IsPlayAction) ?? Enumerable.Empty<GameAction>());
            if (state.HasPlayAction)
            {
                actions.Add(new GameAction
                {
                    Name = "Play",
                    Type = GameActionType.File,
                    Path = state.PlayPath,
                    WorkingDir = state.WorkingDirectory,
                    IsPlayAction = true
                });
            }
            game.GameActions = actions;
        }

        public void ApplyUninstalled(Game game)
        {
            if (game == null) return;
            game.IsInstalled = false;
            game.InstallDirectory = string.Empty;
            game.InstallSize = null;
            game.GameActions = new ObservableCollection<GameAction>(
                game.GameActions?.Where(action => action != null && !action.IsPlayAction) ?? Enumerable.Empty<GameAction>());
        }
    }
}
