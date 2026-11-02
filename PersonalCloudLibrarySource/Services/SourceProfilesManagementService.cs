using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    // Explicit profile editing only. This does not enable multi-source mode
    // or write Playnite settings, and never deletes or retires a source.
    public sealed class SourceProfilesManagementService
    {
        private readonly SourceProfilesFileStore store = new SourceProfilesFileStore();
        private readonly SourceProfilesConfigurationCodec codec =
            new SourceProfilesConfigurationCodec();

        public List<PclsSourceProfileDefinition> OpenOrPreviewLegacy(
            string pluginDataDirectory,
            PersonalCloudLibrarySourceSettingsV3 currentSettings,
            Guid newLegacyProfileId)
        {
            if (currentSettings == null)
                throw new ArgumentNullException(nameof(currentSettings));
            var path = Path.Combine(pluginDataDirectory, SourceProfilesFileStore.FileName);
            if (File.Exists(path) || Directory.Exists(path))
            {
                var read = store.Load(pluginDataDirectory);
                if (!read.Succeeded)
                    throw new InvalidOperationException(
                        "Existing source profiles are invalid; do not overwrite them.");
                return Clone(read.Profiles);
            }
            // The old source receives one stable identity in the preview.
            // No new settings file is created until explicit Save.
            var original = new SourceProfileSettingsProjector()
                .FromLegacySettings(currentSettings, newLegacyProfileId);
            return new List<PclsSourceProfileDefinition> { original };
        }

        public void AddProfile(IList<PclsSourceProfileDefinition> profiles,
            string providerType, Guid newProfileId)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            if (profiles.Count >= 32)
                throw new InvalidOperationException("Maximum of 32 source profiles.");
            if (newProfileId == Guid.Empty ||
                profiles.Any(x => string.Equals(x?.ProfileId,
                    newProfileId.ToString("D"), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("The new source profile ID is invalid.");
            if (!string.Equals(providerType, "LocalFile", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(providerType, "LocalFolder", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(providerType, "RcloneRemote", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unsupported source provider.");
            profiles.Add(new PclsSourceProfileDefinition
            {
                ProfileId = newProfileId.ToString("D"),
                DisplayName = "New source",
                ProviderType = providerType
            });
        }

        public string SaveWithoutActivation(string pluginDataDirectory,
            IReadOnlyList<PclsSourceProfileDefinition> baseline,
            IReadOnlyList<PclsSourceProfileDefinition> proposed,
            PersonalCloudLibrarySourceSettingsV3 shared)
        {
            if (baseline == null || baseline.Count == 0 || proposed == null ||
                shared == null)
                throw new InvalidOperationException("Load profiles before saving.");
            var validator = new SourceProfileIdentityPolicy();
            var report = validator.Validate(proposed);
            if (!report.Valid)
                throw new InvalidOperationException(string.Join("; ", report.Issues));
            var baselineIds = new HashSet<string>(
                baseline.Select(x => x.ProfileId), StringComparer.OrdinalIgnoreCase);
            var proposedIds = new HashSet<string>(
                proposed.Select(x => x.ProfileId), StringComparer.OrdinalIgnoreCase);
            if (!baselineIds.IsSubsetOf(proposedIds))
                throw new InvalidOperationException(
                    "Removing or renaming a source ID requires a separate explicit retirement workflow.");
            foreach (var prior in baseline)
            {
                var current = proposed.Single(x =>
                    string.Equals(x.ProfileId, prior.ProfileId,
                        StringComparison.OrdinalIgnoreCase));
                if (prior.IsLegacySingleSource != current.IsLegacySingleSource)
                    throw new InvalidOperationException(
                        "An existing source cannot change its legacy identity.");
            }
            if (proposed.Count(x => x.IsLegacySingleSource) != 1 ||
                proposed.Any(x => !x.Enabled))
                throw new InvalidOperationException(
                    "A legacy profile is required and disabled sources cannot be retired silently.");
            // Check paths and remote requirements without contacting the provider.
            var projector = new SourceProfileSettingsProjector();
            foreach (var profile in proposed)
                projector.Project(profile, shared);

            var path = Path.Combine(pluginDataDirectory, SourceProfilesFileStore.FileName);
            if (File.Exists(path) || Directory.Exists(path))
            {
                var current = store.Load(pluginDataDirectory);
                if (!current.Succeeded)
                    throw new InvalidOperationException(
                        "Existing source profiles are invalid. Recover before editing.");
                // Refuse stale screens: another editor may have saved new profiles.
                if (!string.Equals(codec.Serialize(baseline),
                    codec.Serialize(current.Profiles), StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Source profiles changed on disk; reload before saving.");
            }
            // This only saves the independent JSON file. Never enable
            // EnableMultiSourceProfiles here, even when valid.
            return store.Save(pluginDataDirectory, proposed);
        }

        public List<PclsSourceProfileDefinition> Clone(
            IReadOnlyList<PclsSourceProfileDefinition> profiles)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            var parsed = codec.Parse(codec.Serialize(profiles));
            if (!parsed.Succeeded)
                throw new InvalidOperationException("Could not clone validated profiles.");
            return parsed.Profiles.ToList();
        }
    }
}
