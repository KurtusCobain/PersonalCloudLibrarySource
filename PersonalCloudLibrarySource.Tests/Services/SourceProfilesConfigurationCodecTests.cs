using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class SourceProfilesConfigurationCodecTests
    {
        private const string OriginalId = "cbce1e42-8f44-4424-9da4-2140d5c86a52";
        private const string AdditionalId = "eaad5d6d-ec8c-4f03-bff2-f32dd1201c99";

        [Test]
        public void LegacyPreviewRoundTripPreservesSourceFieldsAndOriginalGameIdPolicy()
        {
            var old = new PersonalCloudLibrarySourceSettingsV3
            {
                LibraryDisplayName = "My old PCLS library",
                SourceProviderType = "RcloneRemote",
                RcloneExecutablePath = @"C:\Tools\rclone.exe",
                RcloneRemoteName = "personal-remote",
                RcloneManifestPath = "catalog.json",
                RcloneContentRoot = "games",
                RcloneTimeoutSeconds = 127
            };
            var codec = new SourceProfilesConfigurationCodec();
            var preview = codec.PreviewLegacyMigration(old, Guid.Parse(OriginalId));
            var parsed = codec.Parse(preview);

            Assert.That(parsed.Succeeded, Is.True, parsed.Error);
            Assert.That(parsed.Profiles, Has.Count.EqualTo(1));
            Assert.That(parsed.Profiles[0].IsLegacySingleSource, Is.True);
            Assert.That(parsed.Profiles[0].ProfileId, Is.EqualTo(OriginalId));
            Assert.That(parsed.Profiles[0].RcloneTimeoutSeconds, Is.EqualTo(127));
            Assert.That(parsed.Profiles[0].RcloneRemoteName, Is.EqualTo("personal-remote"));
            Assert.That(new SourceProfileIdentityPolicy()
                .GameId(parsed.Profiles[0], "old-game-id"), Is.EqualTo("old-game-id"));
            Assert.That(old.RcloneTimeoutSeconds, Is.EqualTo(127),
                "Migration preview must not save or change old settings.");
        }

        [Test]
        public void TwoProfilesRoundTripAllRoutingRelevantFields()
        {
            var first = new PclsSourceProfileDefinition
            {
                ProfileId = OriginalId, DisplayName = "Local games",
                ProviderType = "LocalFile", IsLegacySingleSource = true,
                LocalManifestPath = @"D:\ROMs\pcls.json"
            };
            var second = new PclsSourceProfileDefinition
            {
                ProfileId = AdditionalId, DisplayName = "NAS archive",
                ProviderType = "RcloneRemote", Enabled = true,
                RcloneExecutablePath = @"C:\Utils\rclone.exe",
                RcloneRemoteName = "test-remote", RcloneManifestPath = "archive/pcls.json",
                RcloneContentRoot = "archive/games", RcloneTimeoutSeconds = 149
            };
            var codec = new SourceProfilesConfigurationCodec();
            var serialized = codec.Serialize(new[] { first, second });
            var parsed = codec.Parse(serialized);

            Assert.That(parsed.Succeeded, Is.True, parsed.Error);
            Assert.That(parsed.Profiles, Has.Count.EqualTo(2));
            Assert.That(parsed.Profiles[0].LocalManifestPath, Is.EqualTo(first.LocalManifestPath));
            Assert.That(parsed.Profiles[1].RcloneContentRoot, Is.EqualTo(second.RcloneContentRoot));
            Assert.That(parsed.Profiles[1].RcloneTimeoutSeconds, Is.EqualTo(149));
            Assert.That(parsed.Profiles[1].ProfileId, Is.EqualTo(AdditionalId));
            Assert.That(first.ProviderType, Is.EqualTo("LocalFile"));
        }

        [Test]
        public void InvalidSchemaAndDuplicateProfileIdsFailAsOneDocument()
        {
            var codec = new SourceProfilesConfigurationCodec();
            Assert.That(codec.Parse("{\"SchemaVersion\":99,\"Profiles\":[]}").Succeeded, Is.False);
            Assert.That(codec.Parse("{\"SchemaVersion\":1,\"Profiles\":[]}").Succeeded, Is.False);
            var duplicated = new[]
            {
                new PclsSourceProfileDefinition
                {
                    ProfileId = OriginalId, ProviderType = "LocalFile",
                    DisplayName = "A", LocalManifestPath = "a"
                },
                new PclsSourceProfileDefinition
                {
                    ProfileId = OriginalId, ProviderType = "LocalFile",
                    DisplayName = "B", LocalManifestPath = "b"
                }
            };
            Assert.Throws<InvalidOperationException>(() => codec.Serialize(duplicated));
            Assert.That(codec.Parse("not json").Succeeded, Is.False);
        }

        [Test]
        public void OversizedDocumentRefusedWithoutPartialProfiles()
        {
            var codec = new SourceProfilesConfigurationCodec();
            var huge = new string('x',
                SourceProfilesConfigurationCodec.MaxDocumentCharacters + 1);
            var result = codec.Parse(huge);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Profiles, Is.Empty);
            Assert.That(result.Error, Does.Contain("size limit"));
        }

        [Test]
        public void ParsedProfilesDoNotAliasInputInstances()
        {
            var original = new PclsSourceProfileDefinition
            {
                ProfileId = OriginalId, DisplayName = "Before",
                ProviderType = "LocalFile", LocalManifestPath = "original.json"
            };
            var codec = new SourceProfilesConfigurationCodec();
            var json = codec.Serialize(new[] { original });
            original.LocalManifestPath = "changed.json";

            var parsed = codec.Parse(json);
            Assert.That(parsed.Succeeded, Is.True, parsed.Error);
            Assert.That(parsed.Profiles[0].LocalManifestPath, Is.EqualTo("original.json"));
            Assert.That(parsed.Profiles[0], Is.Not.SameAs(original));
        }
    }
}
