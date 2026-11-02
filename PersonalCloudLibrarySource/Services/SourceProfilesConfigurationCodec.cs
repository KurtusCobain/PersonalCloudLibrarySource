using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Web.Script.Serialization;

namespace PersonalCloudLibrarySource
{
    // Proposed independent multi-source document. NOT yet wired to
    // the live v0.3.2 settings file or saved automatically.
    public sealed class PclsSourceProfilesDocument
    {
        // Default absent version to unsupported; do not adopt unversioned data.
        public int SchemaVersion { get; set; }
        public List<PclsSourceProfileDefinition> Profiles { get; set; }
            = new List<PclsSourceProfileDefinition>();
    }

    public sealed class SourceProfilesParseResult
    {
        public bool Succeeded { get; internal set; }
        public string Error { get; internal set; } = string.Empty;
        public IReadOnlyList<PclsSourceProfileDefinition> Profiles { get; internal set; }
            = new PclsSourceProfileDefinition[0];
    }

    // Pure bounded JSON codec. No filesystem mutation, no persistent user state
    // changes, no rclone credentials and no library database writes.
    public sealed class SourceProfilesConfigurationCodec
    {
        public const int CurrentSchemaVersion = 1;
        public const int MaxDocumentCharacters = 128 * 1024;

        public string Serialize(IReadOnlyList<PclsSourceProfileDefinition> profiles)
        {
            var validation = new SourceProfileIdentityPolicy().Validate(profiles);
            if (!validation.Valid)
                throw new InvalidOperationException(
                    "Source profile identifiers or providers are invalid.");
            var document = new PclsSourceProfilesDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                Profiles = profiles.Select(Clone).ToList()
            };
            var json = CreateSerializer().Serialize(document);
            if (json.Length > MaxDocumentCharacters)
                throw new InvalidOperationException("Source profiles exceed the size limit.");
            return json;
        }

        public SourceProfilesParseResult Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaxDocumentCharacters)
                return Refuse("Source profiles are missing or exceed the size limit.");
            try
            {
                var document = CreateSerializer()
                    .Deserialize<PclsSourceProfilesDocument>(json);
                if (document == null || document.SchemaVersion != CurrentSchemaVersion)
                    return Refuse("Unsupported source profiles document version.");
                if (document.Profiles == null)
                    return Refuse("Source profiles collection is missing.");
                var validation = new SourceProfileIdentityPolicy().Validate(
                    document.Profiles);
                if (!validation.Valid)
                    return Refuse("Source profile identifiers or providers are invalid.");
                return new SourceProfilesParseResult
                {
                    Succeeded = true,
                    Profiles = new ReadOnlyCollection<PclsSourceProfileDefinition>(
                        document.Profiles.Select(Clone).ToList())
                };
            }
            catch (ArgumentException)
            {
                return Refuse("Source profiles JSON could not be validated.");
            }
            catch (InvalidOperationException)
            {
                return Refuse("Source profiles JSON could not be validated.");
            }
        }

        public string PreviewLegacyMigration(
            PersonalCloudLibrarySourceSettings legacy, Guid persistentProfileId)
        {
            var profile = new SourceProfileSettingsProjector()
                .FromLegacySettings(legacy, persistentProfileId);
            return Serialize(new[] { profile });
        }

        private static JavaScriptSerializer CreateSerializer()
            => new JavaScriptSerializer
            {
                MaxJsonLength = MaxDocumentCharacters,
                RecursionLimit = 16
            };

        private static PclsSourceProfileDefinition Clone(PclsSourceProfileDefinition s)
        {
            return new PclsSourceProfileDefinition
            {
                ProfileId = s.ProfileId, DisplayName = s.DisplayName,
                ProviderType = s.ProviderType,
                IsLegacySingleSource = s.IsLegacySingleSource,
                Enabled = s.Enabled,
                LocalManifestPath = s.LocalManifestPath,
                LocalLibraryRoot = s.LocalLibraryRoot,
                ManifestRelativePath = s.ManifestRelativePath,
                RcloneExecutablePath = s.RcloneExecutablePath,
                RcloneRemoteName = s.RcloneRemoteName,
                RcloneManifestPath = s.RcloneManifestPath,
                RcloneContentRoot = s.RcloneContentRoot,
                RcloneTimeoutSeconds = s.RcloneTimeoutSeconds
            };
        }

        private static SourceProfilesParseResult Refuse(string reason)
            => new SourceProfilesParseResult { Error = reason };
    }
}
