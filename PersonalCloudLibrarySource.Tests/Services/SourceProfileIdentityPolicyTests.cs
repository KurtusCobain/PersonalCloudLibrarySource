using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class SourceProfileIdentityPolicyTests
    {
        private static PclsSourceProfileDefinition Profile(string id, bool legacy = false)
            => new PclsSourceProfileDefinition
            {
                ProfileId = id, DisplayName = "Test Source",
                ProviderType = "LocalFolder", IsLegacySingleSource = legacy
            };

        [Test]
        public void LegacySourcePreservesExactHistoricalGameId()
        {
            var profile = Profile("889ed3da-7c18-442b-9cd8-ad4b744824cc", true);
            var result = new SourceProfileIdentityPolicy().GameId(profile, "old-playnite-id");

            Assert.That(result, Is.EqualTo("old-playnite-id"));
        }

        [Test]
        public void NewSourcesWithIdenticalItemIdsReceiveDifferentStableGameIds()
        {
            var policy = new SourceProfileIdentityPolicy();
            var a = Profile("889ed3da-7c18-442b-9cd8-ad4b744824cc");
            var b = Profile("f91d470f-006c-48ec-af3b-e2b211b322bc");

            var first = policy.GameId(a, "my-game");
            var second = policy.GameId(b, "my-game");

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first.StartsWith(SourceProfileIdentityPolicy.NamespacedPrefix), Is.True);
            Assert.That(first, Is.EqualTo(policy.GameId(a, "my-game")));
            Assert.That(first, Is.EqualTo(policy.GameId(a, "MY-GAME")),
                "Manifest IDs are currently compared case-insensitively.");
            Assert.That(policy.HasAnyCollision(new[] { first, second }), Is.False);
        }

        [Test]
        public void ChangingSourceDisplayNameAndProviderDoesNotRewriteGameIds()
        {
            var policy = new SourceProfileIdentityPolicy();
            var profile = Profile("889ed3da-7c18-442b-9cd8-ad4b744824cc");
            var original = policy.GameId(profile, "game-one");
            profile.DisplayName = "A renamed external SSD";
            profile.ProviderType = "RcloneRemote";

            Assert.That(policy.GameId(profile, "game-one"), Is.EqualTo(original));
        }

        [Test]
        public void ProfileValidationRejectsDuplicateIdsAndTwoLegacyProfiles()
        {
            var id = "889ed3da-7c18-442b-9cd8-ad4b744824cc";
            var result = new SourceProfileIdentityPolicy().Validate(
                new List<PclsSourceProfileDefinition>
                {
                    Profile(id, true), Profile(id, true)
                });

            Assert.That(result.Valid, Is.False);
            Assert.That(result.Issues, Has.Count.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ProfileValidationRejectsMissingIdsUnsupportedProviderAndEmptyCollection()
        {
            var policy = new SourceProfileIdentityPolicy();
            Assert.That(policy.Validate(new List<PclsSourceProfileDefinition>()).Valid, Is.False);

            var invalid = Profile(Guid.Empty.ToString());
            invalid.ProviderType = "Unrecognized";
            Assert.That(policy.Validate(new[] { invalid }).Valid, Is.False);
        }

        [Test]
        public void ExplicitCrossSourceCollisionCheckRejectsLegacyLookalike()
        {
            var policy = new SourceProfileIdentityPolicy();
            var newer = policy.GameId(
                Profile("889ed3da-7c18-442b-9cd8-ad4b744824cc"), "game");
            var legacy = policy.GameId(
                Profile("f91d470f-006c-48ec-af3b-e2b211b322bc", true), newer);

            Assert.That(policy.HasAnyCollision(new[] { newer, legacy }), Is.True);
        }
    }
}
