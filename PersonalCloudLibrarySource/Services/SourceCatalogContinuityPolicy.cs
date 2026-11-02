using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PersonalCloudLibrarySource
{
    // A read-only inventory of previously validated game identities.
    // This is NOT a playable catalog, an ownership receipt, or permission
    // to publish stale metadata or perform install/uninstall operations.
    public sealed class SourceCatalogIdentitySnapshot
    {
        private readonly ReadOnlyCollection<string> gameIds;

        internal SourceCatalogIdentitySnapshot(string profileId,
            string bindingFingerprint, IEnumerable<string> gameIds)
        {
            ProfileId = profileId;
            BindingFingerprint = bindingFingerprint;
            this.gameIds = new ReadOnlyCollection<string>(gameIds.ToList());
        }

        public string ProfileId { get; }
        internal string BindingFingerprint { get; }
        public IReadOnlyList<string> GameIds => gameIds;
        public int GameCount => gameIds.Count;
    }

    public enum SourceCatalogContinuityState
    {
        FreshValidated,
        OfflineIdentityCandidate,
        HoldMissingHistory,
        HoldChangedBinding,
        HoldInvalidOrUnsafe,
        HoldDisabled
    }

    public sealed class SourceCatalogContinuityResult
    {
        internal SourceCatalogContinuityResult(SourceCatalogContinuityState state,
            int retainedGameCount)
        {
            State = state;
            RetainedGameCount = retainedGameCount;
        }

        public SourceCatalogContinuityState State { get; }
        public int RetainedGameCount { get; }
        // True only for an identity inventory eligible for *future* recovery
        // planning. Never interpret this as permission to return GetGames data.
        public bool HasOfflineIdentityCandidate
            => State == SourceCatalogContinuityState.OfflineIdentityCandidate;
    }

    // Non-persistent, non-activating foundation for last-known-good continuity.
    // Current GetGames still refuses any partial or failed multi-source import.
    public sealed class SourceCatalogContinuityPolicy
    {
        private readonly SourceProfileSettingsProjector projector =
            new SourceProfileSettingsProjector();
        private readonly SourceProfileIdentityPolicy identity =
            new SourceProfileIdentityPolicy();

        public SourceCatalogIdentitySnapshot CaptureSuccessful(
            PclsSourceProfileDefinition profile,
            PersonalCloudLibrarySourceSettingsV3 shared,
            ImportOutcome successfulImport)
        {
            if (profile == null || shared == null || successfulImport == null ||
                !profile.Enabled || !shared.Enabled || !successfulImport.Succeeded)
                throw new InvalidOperationException("Only an enabled, successful source can be captured.");

            var scoped = projector.Project(profile, shared);
            if (successfulImport.ValidItems == null ||
                successfulImport.Games == null ||
                successfulImport.ValidItems.Count != successfulImport.Games.Count ||
                successfulImport.ValidItems.Count > ManifestParserValidator.MaxManifestItems)
                throw new InvalidOperationException("Source result does not form a complete catalog.");

            var originalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var mappedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in successfulImport.ValidItems)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id) ||
                    !originalIds.Add(item.Id))
                    throw new InvalidOperationException("Invalid or duplicate source item identity.");
            }
            foreach (var game in successfulImport.Games)
            {
                if (game == null || string.IsNullOrWhiteSpace(game.GameId) ||
                    !originalIds.Contains(game.GameId) || !mappedIds.Add(game.GameId))
                    throw new InvalidOperationException("The mapped game identities are incomplete.");
            }
            if (mappedIds.Count != originalIds.Count)
                throw new InvalidOperationException("Source game identities do not match manifest items.");

            // Store *only* identities. Cached launch paths and stale uninstall
            // controllers must never be replayed from an offline source.
            var ids = successfulImport.ValidItems
                .Select(item => identity.GameId(profile, item.Id)).ToArray();
            if (identity.HasAnyCollision(ids))
                throw new InvalidOperationException("Game identities are not unique.");

            return new SourceCatalogIdentitySnapshot(
                profile.ProfileId, BindingFingerprint(profile, scoped), ids);
        }

        public SourceCatalogContinuityResult Examine(
            PclsSourceProfileDefinition profile,
            PersonalCloudLibrarySourceSettingsV3 shared,
            ImportOutcome latestImport,
            SourceCatalogIdentitySnapshot history)
        {
            if (profile == null || shared == null || !profile.Enabled || !shared.Enabled)
                return Hold(SourceCatalogContinuityState.HoldDisabled);

            PersonalCloudLibrarySourceSettingsV3 scoped;
            try
            {
                scoped = projector.Project(profile, shared);
            }
            catch (Exception ex) when (ex is InvalidOperationException ||
                                       ex is ArgumentException)
            {
                return Hold(SourceCatalogContinuityState.HoldInvalidOrUnsafe);
            }
            if (latestImport != null && latestImport.Succeeded)
            {
                try
                {
                    var fresh = CaptureSuccessful(profile, shared, latestImport);
                    return new SourceCatalogContinuityResult(
                        SourceCatalogContinuityState.FreshValidated, fresh.GameCount);
                }
                catch (Exception ex) when (ex is InvalidOperationException ||
                                           ex is ArgumentException)
                {
                    return Hold(SourceCatalogContinuityState.HoldInvalidOrUnsafe);
                }
            }
            // Malformed, unsupported, explicitly disabled and unclassified
            // failures must never be passed off as an offline source.
            if (latestImport == null ||
                latestImport.FailureKind != ImportFailureKind.SourceUnavailable)
                return Hold(SourceCatalogContinuityState.HoldInvalidOrUnsafe);

            if (history == null || history.GameCount == 0)
                return Hold(SourceCatalogContinuityState.HoldMissingHistory);
            if (!string.Equals(history.ProfileId, profile.ProfileId,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(history.BindingFingerprint,
                    BindingFingerprint(profile, scoped), StringComparison.Ordinal))
                return Hold(SourceCatalogContinuityState.HoldChangedBinding);

            return new SourceCatalogContinuityResult(
                SourceCatalogContinuityState.OfflineIdentityCandidate,
                history.GameCount);
        }

        private static SourceCatalogContinuityResult Hold(SourceCatalogContinuityState state)
            => new SourceCatalogContinuityResult(state, 0);

        // A length-prefixed fingerprint binds the prior inventory to the
        // originating source and shared cache, without exposing user paths.
        // Display names and timeouts are deliberately not identity fields.
        private static string BindingFingerprint(PclsSourceProfileDefinition profile,
            PersonalCloudLibrarySourceSettingsV3 scoped)
        {
            var fields = new[]
            {
                profile.ProfileId.ToLowerInvariant(),
                profile.IsLegacySingleSource ? "legacy" : "additional",
                scoped.SourceProviderType,
                scoped.LocalCacheFolder,
                scoped.LocalManifestPath,
                scoped.LocalLibraryRoot,
                scoped.ManifestRelativePath,
                scoped.RcloneExecutablePath,
                scoped.RcloneRemoteName,
                scoped.RcloneManifestPath,
                scoped.RcloneContentRoot
            };
            var canonical = new StringBuilder("pcls-continuity-v1:");
            foreach (var value in fields)
            {
                var text = value ?? string.Empty;
                canonical.Append(text.Length).Append(':').Append(text);
            }
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(canonical.ToString()))).Replace("-", "");
            }
        }
    }
}
