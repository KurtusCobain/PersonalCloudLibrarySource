using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    // Input is an already validated per-source ImportOutcome. This planner
    // does not read providers or mutate Playnite or user files.
    public sealed class PclsSourceCatalogBatch
    {
        public PclsSourceProfileDefinition Profile { get; set; }
        public ImportOutcome Import { get; set; }
    }

    public sealed class PclsGameCatalogRoute
    {
        public string GameId { get; internal set; } = string.Empty;
        public string OriginalItemId { get; internal set; } = string.Empty;
        public string ProfileId { get; internal set; } = string.Empty;
    }

    public sealed class MultiSourceCatalogPlan
    {
        public bool CanPublish { get; internal set; }
        public string RefusalReason { get; internal set; } = string.Empty;
        public IReadOnlyList<GameMetadata> Games { get; internal set; }
            = new GameMetadata[0];
        public IReadOnlyList<PclsGameCatalogRoute> Routes { get; internal set; }
            = new PclsGameCatalogRoute[0];
    }

    // Safety contract: never report only healthy sources as the full catalog.
    // Playnite may remove entries missing from a successful GetGames result.
    // A caller can implement persisted last-known-good snapshots later, but
    // partial source failures MUST NOT cause a successful partial import.
    public sealed class MultiSourceCatalogPlanner
    {
        public MultiSourceCatalogPlan Build(IReadOnlyList<PclsSourceCatalogBatch> batches)
        {
            if (batches == null || batches.Count == 0)
                return Refuse("At least one source catalog is required.");
            if (batches.Any(batch => batch == null || batch.Profile == null))
                return Refuse("A source profile is missing.");

            var identity = new SourceProfileIdentityPolicy();
            var validation = identity.Validate(
                batches.Select(batch => batch.Profile).ToList());
            if (!validation.Valid)
                return Refuse("Source profiles are invalid: " +
                    string.Join("; ", validation.Issues));

            var games = new List<GameMetadata>();
            var routes = new List<PclsGameCatalogRoute>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var batch in batches)
            {
                var profile = batch.Profile;
                if (!profile.Enabled)
                    return Refuse("A source was disabled. Preserve existing games until " +
                        "an explicit source retirement/keep-metadata decision.");
                if (batch.Import == null || !batch.Import.Succeeded)
                    return Refuse("A source catalog is unavailable or invalid. " +
                        "Keep the previously imported Playnite library unchanged.");
                if (batch.Import.Games == null || batch.Import.ValidItems == null)
                    return Refuse("A source catalog is missing validated item records.");
                if (batch.Import.Games.Count != batch.Import.ValidItems.Count)
                    return Refuse("A source returned fewer or more games than its validated manifest. " +
                        "Do not publish an incomplete catalog.");
                var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in batch.Import.ValidItems)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Id) ||
                        !known.Add(item.Id))
                        return Refuse("A source contains invalid or repeated item identities.");
                }
                foreach (var item in batch.Import.Games)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.GameId) ||
                        !known.Contains(item.GameId))
                        return Refuse("A source game lacks matching validated manifest identity.");
                    var id = identity.GameId(profile, item.GameId);
                    if (!used.Add(id))
                        return Refuse("Game identity collision across source catalogs.");
                    // Copy current PCLS-managed metadata rather than rewriting
                    // the caller's previously validated source game in place.
                    games.Add(new GameMetadata
                    {
                        GameId = id,
                        Name = item.Name,
                        Description = item.Description,
                        IsInstalled = item.IsInstalled,
                        InstallDirectory = item.InstallDirectory,
                        InstallSize = item.InstallSize,
                        GameActions = item.GameActions == null
                            ? null : new List<GameAction>(item.GameActions)
                    });
                    routes.Add(new PclsGameCatalogRoute
                    {
                        GameId = id,
                        ProfileId = profile.ProfileId,
                        OriginalItemId = item.GameId
                    });
                }
            }
            return new MultiSourceCatalogPlan
            {
                CanPublish = true,
                Games = games.AsReadOnly(),
                Routes = routes.AsReadOnly()
            };
        }

        private static MultiSourceCatalogPlan Refuse(string reason)
            => new MultiSourceCatalogPlan { RefusalReason = reason };
    }
}
