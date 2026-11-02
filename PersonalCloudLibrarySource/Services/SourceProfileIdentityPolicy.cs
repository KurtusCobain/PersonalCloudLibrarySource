using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PersonalCloudLibrarySource
{
    // Pure model for planned multi-source settings. Not enabled or persisted yet.
    // A source identifier is created once, and must never change when its
    // display name, path, remote, or ordering changes.
    public sealed class PclsSourceProfileDefinition
    {
        public string ProfileId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ProviderType { get; set; } = string.Empty;
        public bool IsLegacySingleSource { get; set; }
        public bool Enabled { get; set; } = true;
        // Source-local configuration. The shared cache remains outside profiles.
        public string LocalManifestPath { get; set; } = string.Empty;
        public string LocalLibraryRoot { get; set; } = string.Empty;
        public string ManifestRelativePath { get; set; } = string.Empty;
        public string RcloneExecutablePath { get; set; } = "rclone";
        public string RcloneRemoteName { get; set; } = string.Empty;
        public string RcloneManifestPath { get; set; } = string.Empty;
        public string RcloneContentRoot { get; set; } = string.Empty;
        public int RcloneTimeoutSeconds { get; set; } = 90;
    }

    public sealed class PclsSourceProfileValidation
    {
        public bool Valid { get; internal set; }
        public List<string> Issues { get; } = new List<string>();
    }

    public sealed class SourceProfileIdentityPolicy
    {
        public const string NamespacedPrefix = "pcls1-";

        public PclsSourceProfileValidation Validate(IReadOnlyCollection<PclsSourceProfileDefinition> profiles)
        {
            var result = new PclsSourceProfileValidation();
            if (profiles == null || profiles.Count == 0)
            {
                result.Issues.Add("At least one source profile is required.");
                return result;
            }
            if (profiles.Count > 32)
            {
                result.Issues.Add("Maximum of 32 source profiles.");
                return result;
            }
            var ids = new HashSet<Guid>();
            var legacyCount = 0;
            foreach (var profile in profiles)
            {
                if (profile == null)
                {
                    result.Issues.Add("Null source profile.");
                    continue;
                }
                Guid identifier;
                if (!Guid.TryParse(profile.ProfileId, out identifier) ||
                    identifier == Guid.Empty || !ids.Add(identifier))
                    result.Issues.Add("Source profile ID must be a unique nonempty GUID.");
                if (string.IsNullOrWhiteSpace(profile.DisplayName))
                    result.Issues.Add("Source profile display name is required.");
                if (!string.Equals(profile.ProviderType, "LocalFile", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(profile.ProviderType, "LocalFolder", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(profile.ProviderType, "RcloneRemote", StringComparison.OrdinalIgnoreCase))
                    result.Issues.Add("Unsupported source provider type.");
                if (profile.IsLegacySingleSource && ++legacyCount > 1)
                    result.Issues.Add("Only one source may preserve the pre-1.0 game ID namespace.");
            }
            result.Valid = result.Issues.Count == 0;
            return result;
        }

        // Legacy IDs remain byte-for-byte unchanged. New sources get a
        // namespace derived from their persisted GUID and an item-ID hash.
        // Never derive identity from provider name, library path or ordering.
        public string GameId(PclsSourceProfileDefinition profile, string itemId)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("Game item ID is required.", nameof(itemId));
            Guid identifier;
            if (!Guid.TryParse(profile.ProfileId, out identifier) || identifier == Guid.Empty)
                throw new ArgumentException("Profile requires a persistent GUID.", nameof(profile));
            if (profile.IsLegacySingleSource)
                return itemId;
            using (var sha = SHA256.Create())
            {
                var canonical = itemId.ToUpperInvariant();
                var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                return NamespacedPrefix + identifier.ToString("N") + "-" +
                    BitConverter.ToString(digest).Replace("-", "");
            }
        }

        // Call after merging profiles and before mutating Playnite's catalog.
        // Includes collisions with arbitrary legacy GameIds that happen to
        // look like new namespaced IDs.
        public bool HasAnyCollision(IEnumerable<string> gameIds)
        {
            if (gameIds == null) return false;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in gameIds)
            {
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                    return true;
            }
            return false;
        }
    }
}
