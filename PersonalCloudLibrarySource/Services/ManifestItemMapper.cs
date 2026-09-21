using Playnite.SDK.Models;
using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource
{
    public sealed class ManifestItemMapper
    {
        public List<GameMetadata> Map(
            IEnumerable<PersonalCloudLibraryItem> items,
            PersonalCloudLibrarySourceSettings settings,
            IList<string> diagnostics)
        {
            var games = new List<GameMetadata>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items ?? new PersonalCloudLibraryItem[0])
            {
                if (item == null)
                {
                    diagnostics?.Add("item=<null>; skipReason=null item");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(item.Id) || string.IsNullOrWhiteSpace(item.Title))
                {
                    diagnostics?.Add("itemId=" + item.Id + "; title=" + item.Title + "; skipReason=missing id or title");
                    continue;
                }
                if (!ids.Add(item.Id))
                {
                    diagnostics?.Add("itemId=" + item.Id + "; title=" + item.Title + "; skipReason=duplicate id");
                    continue;
                }

                var paths = new CachePathResolver().Resolve(item, settings);
                var state = new LibraryItemStateResolver().Resolve(
                    item,
                    paths.LaunchPath,
                    paths.InstallDirectory,
                    settings.TreatMissingFilesAsUninstalled);
                var sourcePath = PersonalCloudLibrarySource.GetItemSourcePath(item);
                var downloadEligible = !state.IsCached && settings.AllowDownloads &&
                    !string.IsNullOrWhiteSpace(sourcePath) && PersonalCloudLibrarySource.CanResolveSourcePath(settings, sourcePath);
                var game = new GameMetadata
                {
                    GameId = item.Id,
                    Name = item.Title,
                    IsInstalled = state.IsInstalled,
                    InstallDirectory = paths.InstallDirectory
                };
                if (!string.IsNullOrWhiteSpace(item.Notes)) game.Description = item.Notes;
                if (state.HasPlayAction)
                {
                    game.GameActions = new List<GameAction>
                    {
                        new GameAction
                        {
                            Name = "Play",
                            Type = GameActionType.File,
                            Path = state.PlayPath,
                            WorkingDir = state.WorkingDirectory,
                            IsPlayAction = true
                        }
                    };
                }
                diagnostics?.Add(
                    "itemId=" + item.Id + "; title=" + item.Title + "; sourcePath=" + sourcePath +
                    "; cachePath=" + paths.DestinationFile + "; localExists=" + state.IsCached +
                    "; isInstalled=" + state.IsInstalled + "; downloadEligible=" + downloadEligible +
                    "; playActionCount=" + (state.HasPlayAction ? 1 : 0) + "; playActionName=" + (state.HasPlayAction ? "Play" : string.Empty) +
                    "; playActionPath=" + state.PlayPath + "; skipReason=");
                games.Add(game);
            }
            return games;
        }
    }
}
