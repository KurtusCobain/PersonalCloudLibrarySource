using System;

namespace PersonalCloudLibrarySource
{
    // Explicit read-only check of one source's manifest, not a catalog refresh.
    // In particular, "ManifestValid" does not prove that game payloads are online.
    public enum SourceManifestHealth
    {
        ManifestValid,
        SourceUnavailable,
        InvalidManifest,
        UnsupportedSchema,
        InvalidConfiguration,
        Disabled
    }

    public sealed class SourceProfileHealthResult
    {
        public string ProfileId { get; internal set; }
        public SourceManifestHealth Status { get; internal set; }
        public int ManifestItemCount { get; internal set; }
        public bool ManifestReadable => Status == SourceManifestHealth.ManifestValid;
    }

    public sealed class SourceProfileHealthProbe
    {
        private readonly SourceProfileSettingsProjector projector =
            new SourceProfileSettingsProjector();
        private readonly ImportOutcomeService importer = new ImportOutcomeService();

        // The caller deliberately selects a source; this method never saves
        // settings, changes Playnite entries, writes caches or retires games.
        // Do not echo a provider's potentially private paths or error text.
        public SourceProfileHealthResult Probe(
            PclsSourceProfileDefinition profile,
            PersonalCloudLibrarySourceSettingsV3 shared,
            Func<PersonalCloudLibrarySourceSettings, string> readRclone)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (shared == null) throw new ArgumentNullException(nameof(shared));

            var result = new SourceProfileHealthResult
            {
                ProfileId = profile.ProfileId,
                Status = SourceManifestHealth.InvalidConfiguration
            };
            if (!profile.Enabled || !shared.Enabled)
            {
                result.Status = SourceManifestHealth.Disabled;
                return result;
            }
            try
            {
                var scoped = projector.Project(profile, shared);
                ImportOutcome outcome;
                try
                {
                    outcome = importer.Import(scoped, readRclone);
                }
                catch (Exception)
                {
                    // A provider may throw before a structured load result is
                    // available (process startup, filesystem, timeout).
                    result.Status = SourceManifestHealth.SourceUnavailable;
                    return result;
                }
                if (outcome.Succeeded)
                {
                    result.Status = SourceManifestHealth.ManifestValid;
                    result.ManifestItemCount = outcome.ValidItems.Count;
                }
                else if (outcome.FailureKind == ImportFailureKind.UnsupportedSchema)
                    result.Status = SourceManifestHealth.UnsupportedSchema;
                else if (outcome.FailureKind == ImportFailureKind.InvalidManifest)
                    result.Status = SourceManifestHealth.InvalidManifest;
                else
                    result.Status = SourceManifestHealth.SourceUnavailable;
            }
            catch (InvalidOperationException)
            {
                result.Status = SourceManifestHealth.InvalidConfiguration;
            }
            catch (ArgumentException)
            {
                result.Status = SourceManifestHealth.InvalidConfiguration;
            }
            return result;
        }
    }
}
