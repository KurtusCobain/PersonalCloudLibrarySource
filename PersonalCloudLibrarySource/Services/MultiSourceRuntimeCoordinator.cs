using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    // An action's origin is captured at import time. Never infer it from the
    // currently selected settings provider or from a user-facing display name.
    public sealed class SourceScopedGameAction
    {
        private readonly PersonalCloudLibraryItem item;
        private readonly PersonalCloudLibrarySourceSettingsV3 settings;

        internal SourceScopedGameAction(string gameId, string profileId,
            PersonalCloudLibraryItem sourceItem, PersonalCloudLibrarySourceSettingsV3 sourceSettings)
        {
            GameId = gameId;
            ProfileId = profileId;
            OriginalItemId = sourceItem.Id;
            item = CopyItem(sourceItem);
            settings = CopySettings(sourceSettings);
        }

        public string GameId { get; }
        public string ProfileId { get; }
        public string OriginalItemId { get; }

        // Return new objects: a caller must not mutate a published source route.
        public PersonalCloudLibraryItem CreateItemSnapshot() => CopyItem(item);

        public PersonalCloudLibrarySourceSettingsV3 CreateSettingsSnapshot()
            => CopySettings(settings);

        private static PersonalCloudLibraryItem CopyItem(PersonalCloudLibraryItem source)
        {
            return new PersonalCloudLibraryItem
            {
                Id = source.Id, Title = source.Title, Platform = source.Platform,
                LocalPath = source.LocalPath, InstallDirectory = source.InstallDirectory,
                LaunchFile = source.LaunchFile, SourcePath = source.SourcePath,
                CachePath = source.CachePath, RemotePath = source.RemotePath,
                SourceType = source.SourceType, PackageRole = source.PackageRole,
                Notes = source.Notes
            };
        }

        private static PersonalCloudLibrarySourceSettingsV3 CopySettings(
            PersonalCloudLibrarySourceSettingsV3 source)
        {
            return new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = source.Enabled, LibraryDisplayName = source.LibraryDisplayName,
                SourceProviderType = source.SourceProviderType,
                LocalManifestPath = source.LocalManifestPath,
                LocalLibraryRoot = source.LocalLibraryRoot,
                ManifestRelativePath = source.ManifestRelativePath,
                LocalCacheFolder = source.LocalCacheFolder,
                TreatMissingFilesAsUninstalled = source.TreatMissingFilesAsUninstalled,
                RcloneExecutablePath = source.RcloneExecutablePath,
                RcloneRemoteName = source.RcloneRemoteName,
                RcloneManifestPath = source.RcloneManifestPath,
                RcloneContentRoot = source.RcloneContentRoot,
                RcloneTimeoutSeconds = source.RcloneTimeoutSeconds,
                AllowDownloads = source.AllowDownloads,
                EnableDiagnostics = source.EnableDiagnostics,
                UninstallBehavior = source.UninstallBehavior,
                AllowUninstallOutsideCacheFolder = false,
                TransferConcurrency = source.TransferConcurrency,
                VerifyAfterTransfer = source.VerifyAfterTransfer,
                RemoveIncompleteTransferFiles = source.RemoveIncompleteTransferFiles
            };
        }
    }

    public sealed class MultiSourceRuntimePlan
    {
        private readonly IReadOnlyDictionary<string, SourceScopedGameAction> routes;

        internal MultiSourceRuntimePlan(bool canPublish, string refusal,
            IReadOnlyList<GameMetadata> games,
            IReadOnlyDictionary<string, SourceScopedGameAction> routes)
        {
            CanPublish = canPublish;
            RefusalReason = refusal ?? string.Empty;
            Games = games;
            this.routes = routes;
        }

        public bool CanPublish { get; }
        public string RefusalReason { get; }
        public IReadOnlyList<GameMetadata> Games { get; }

        public bool TryResolveAction(string gameId, out SourceScopedGameAction action)
        {
            action = null;
            return CanPublish && !string.IsNullOrWhiteSpace(gameId) &&
                routes.TryGetValue(gameId, out action);
        }
    }

    // Purely opt-in coordinator, NOT yet called by LibraryPlugin.GetGames.
    // No Playnite database mutation, no settings save, and no cache deletion.
    // A failure in any source means that NONE of the combined games can publish.
    public sealed class MultiSourceRuntimeCoordinator
    {
        private readonly ImportOutcomeService importer;
        private readonly SourceProfileSettingsProjector projector;
        private readonly MultiSourceCatalogPlanner planner;

        public MultiSourceRuntimeCoordinator()
            : this(new ImportOutcomeService(),
                new SourceProfileSettingsProjector(), new MultiSourceCatalogPlanner())
        {
        }

        public MultiSourceRuntimeCoordinator(ImportOutcomeService importer,
            SourceProfileSettingsProjector projector, MultiSourceCatalogPlanner planner)
        {
            this.importer = importer ?? throw new ArgumentNullException(nameof(importer));
            this.projector = projector ?? throw new ArgumentNullException(nameof(projector));
            this.planner = planner ?? throw new ArgumentNullException(nameof(planner));
        }

        public MultiSourceRuntimePlan Prepare(
            IReadOnlyList<PclsSourceProfileDefinition> profiles,
            PersonalCloudLibrarySourceSettingsV3 shared,
            Func<PersonalCloudLibrarySourceSettings, string> readRclone)
        {
            if (shared == null || !shared.Enabled || profiles == null ||
                profiles.Count == 0)
                return Refuse("Multi-source settings are disabled or unavailable.");

            var profileValidation = new SourceProfileIdentityPolicy().Validate(profiles);
            if (!profileValidation.Valid)
                return Refuse("Source profile configuration is invalid.");
            if (profiles.Any(profile => !profile.Enabled))
                return Refuse("A disabled source cannot silently retire catalog entries.");

            var batches = new List<PclsSourceCatalogBatch>();
            var settingsByProfile = new Dictionary<string, PersonalCloudLibrarySourceSettingsV3>(
                StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var profile in profiles)
                {
                    var scoped = projector.Project(profile, shared);
                    var outcome = importer.Import(scoped, readRclone);
                    if (!outcome.Succeeded)
                        return Refuse("A source is unavailable or its manifest is invalid. " +
                            "The existing library must remain unchanged.");
                    settingsByProfile.Add(profile.ProfileId, scoped);
                    batches.Add(new PclsSourceCatalogBatch
                    {
                        Profile = profile, Import = outcome
                    });
                }

                var preflightFailure = CheckAllDestinations(batches, settingsByProfile,
                    shared.LocalCacheFolder);
                if (!string.IsNullOrWhiteSpace(preflightFailure))
                    return Refuse(preflightFailure);

                var planned = planner.Build(batches);
                if (!planned.CanPublish)
                    return Refuse(planned.RefusalReason);

                var byProfile = batches.ToDictionary(
                    x => x.Profile.ProfileId, StringComparer.OrdinalIgnoreCase);
                var routes = new Dictionary<string, SourceScopedGameAction>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var route in planned.Routes)
                {
                    var batch = byProfile[route.ProfileId];
                    var sourceItem = batch.Import.ValidItems.Single(x =>
                        string.Equals(x.Id, route.OriginalItemId,
                            StringComparison.OrdinalIgnoreCase));
                    var scoped = settingsByProfile[route.ProfileId];
                    var binding = new SourceScopedGameAction(route.GameId,
                        route.ProfileId, sourceItem, scoped);
                    routes.Add(route.GameId, binding);
                }
                if (routes.Count != planned.Games.Count)
                    return Refuse("A game could not be associated with one verified source.");

                return new MultiSourceRuntimePlan(true, string.Empty,
                    new ReadOnlyCollection<GameMetadata>(planned.Games.ToList()),
                    new ReadOnlyDictionary<string, SourceScopedGameAction>(routes));
            }
            catch (Exception ex) when (ex is InvalidOperationException ||
                                       ex is ArgumentException ||
                                       ex is IOException ||
                                       ex is UnauthorizedAccessException)
            {
                // Never expose partial success or provider paths through a failure.
                return Refuse("Multi-source preparation could not verify every source.");
            }
        }

        private static string CheckAllDestinations(
            IReadOnlyList<PclsSourceCatalogBatch> batches,
            IDictionary<string, PersonalCloudLibrarySourceSettingsV3> scoped,
            string cacheRoot)
        {
            var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var safety = new SafeCacheDeletionPolicy();
            var count = 0;

            foreach (var batch in batches)
            {
                var settings = scoped[batch.Profile.ProfileId];
                foreach (var item in batch.Import.ValidItems)
                {
                    if (++count > ManifestParserValidator.MaxManifestItems)
                        return "Multi-source catalog exceeds the combined item limit.";

                    var paths = new CachePathResolver().Resolve(item, settings);
                    if (!paths.Succeeded)
                        return "A source's cache destination could not be verified.";

                    var target = PersonalCloudLibrarySource.IsDirectoryItem(item)
                        ? paths.DestinationDirectory : paths.DestinationFile;
                    var permission = safety.Authorize(cacheRoot, target, false);
                    if (!permission.Allowed)
                        return "A source has a cache path outside the safe cache scope.";
                    if (targets.ContainsKey(permission.TargetPath))
                        return "Two sources map games to the same cache destination.";
                    targets.Add(permission.TargetPath, batch.Profile.ProfileId);
                }
            }

            // O(number of games * Windows directory depth), not O(n^2).
            // No two games may own ancestor/descendant cache targets.
            foreach (var path in targets.Keys)
            {
                var parent = Path.GetDirectoryName(path);
                while (!string.IsNullOrEmpty(parent) &&
                       PathBoundary.IsContained(cacheRoot, parent))
                {
                    if (targets.ContainsKey(parent))
                        return "Source games have overlapping cache ownership paths.";
                    var next = Path.GetDirectoryName(parent);
                    if (string.Equals(next, parent, StringComparison.OrdinalIgnoreCase))
                        break;
                    parent = next;
                }
            }
            return string.Empty;
        }

        private static MultiSourceRuntimePlan Refuse(string reason)
        {
            return new MultiSourceRuntimePlan(false, reason,
                new GameMetadata[0],
                new ReadOnlyDictionary<string, SourceScopedGameAction>(
                    new Dictionary<string, SourceScopedGameAction>(
                        StringComparer.OrdinalIgnoreCase)));
        }
    }
}
