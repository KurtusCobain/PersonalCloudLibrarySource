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

        // A backup is previewed and checked against the current set of
        // source identities before the user is allowed to restore it.
        // Never silently remove sources added after a backup was made.
        public List<PclsSourceProfileDefinition> PreviewBackupForRestore(
            string pluginDataDirectory, string backupFileName,
            IReadOnlyList<PclsSourceProfileDefinition> baseline,
            PersonalCloudLibrarySourceSettingsV3 shared)
        {
            if (baseline == null || shared == null)
                throw new InvalidOperationException(
                    "Load current source profiles before inspecting a backup.");

            var active = store.Load(pluginDataDirectory);
            if (!active.Succeeded ||
                !string.Equals(codec.Serialize(baseline),
                    codec.Serialize(active.Profiles), StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Current profiles changed or are invalid; reload before restore.");

            var backup = store.InspectBackup(pluginDataDirectory, backupFileName);
            if (!backup.Succeeded)
                throw new InvalidOperationException(
                    "The selected backup is missing or invalid.");

            var currentIds = new HashSet<string>(
                baseline.Select(p => p.ProfileId), StringComparer.OrdinalIgnoreCase);
            var backupIds = new HashSet<string>(
                backup.Profiles.Select(p => p.ProfileId),
                StringComparer.OrdinalIgnoreCase);
            if (!currentIds.SetEquals(backupIds))
                throw new InvalidOperationException(
                    "Restoring this backup would add or remove source identities; refuse.");
            foreach (var prior in baseline)
            {
                var restored = backup.Profiles.Single(p =>
                    string.Equals(p.ProfileId, prior.ProfileId,
                        StringComparison.OrdinalIgnoreCase));
                if (prior.IsLegacySingleSource != restored.IsLegacySingleSource)
                    throw new InvalidOperationException(
                        "Restoring this backup would change a source's legacy identity.");
            }
            if (backup.Profiles.Count(p => p.IsLegacySingleSource) != 1 ||
                backup.Profiles.Any(p => !p.Enabled))
                throw new InvalidOperationException(
                    "The backup cannot restore a missing or disabled original source.");
            var projector = new SourceProfileSettingsProjector();
            foreach (var profile in backup.Profiles)
                projector.Project(profile, shared);

            return Clone(backup.Profiles);
        }

        // Restore is an explicit, separately confirmed action in the UI.
        // Re-check active state and backup contents at commit time; Save()
        // retains a *new* backup of the version being replaced.
        public string RestoreBackupWithoutActivation(
            string pluginDataDirectory, string backupFileName,
            IReadOnlyList<PclsSourceProfileDefinition> baseline,
            IReadOnlyList<PclsSourceProfileDefinition> previewedBackup,
            PersonalCloudLibrarySourceSettingsV3 shared)
        {
            if (previewedBackup == null)
                throw new InvalidOperationException("Preview a backup before restoring.");
            var fresh = PreviewBackupForRestore(
                pluginDataDirectory, backupFileName, baseline, shared);
            if (!string.Equals(codec.Serialize(previewedBackup),
                codec.Serialize(fresh), StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The backup changed since it was previewed; inspect again.");
            return SaveWithoutActivation(
                pluginDataDirectory, baseline, fresh, shared);
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
