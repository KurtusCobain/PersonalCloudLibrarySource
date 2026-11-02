using NUnit.Framework;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class MultiSourceCatalogPlannerTests
    {
        private static PclsSourceProfileDefinition Source(string id, bool legacy = false)
            => new PclsSourceProfileDefinition
            {
                ProfileId = id, DisplayName = "Disposable test source",
                ProviderType = "LocalFolder", IsLegacySingleSource = legacy,
                Enabled = true
            };

        private static ImportOutcome Success(params string[] ids)
        {
            var valid = ids.Select(id =>
                new PersonalCloudLibraryItem { Id = id, Title = "Title " + id }).ToArray();
            var games = ids.Select(id => new GameMetadata
            {
                GameId = id, Name = "Title " + id, IsInstalled = true,
                InstallDirectory = "C:\\DisposableCache\\" + id,
                InstallSize = 17,
                GameActions = new List<GameAction>
                {
                    new GameAction
                    {
                        Name = "Play", Path = "C:\\DisposableCache\\" + id + "\\game.exe",
                        WorkingDir = "C:\\DisposableCache\\" + id,
                        Type = GameActionType.File, IsPlayAction = true
                    }
                }
            }).ToArray();
            return ImportOutcome.Success("disposable test", valid, games);
        }

        [Test]
        public void CombinesLegacyAndNewSourceWithoutRewritingOldGameId()
        {
            var legacy = Source("6a94c3b0-ae21-4f3b-a4eb-87bd6f5f8ba5", true);
            var remote = Source("397ebc5e-83c4-4e4e-b8d4-fb3bd42b409a");
            var outcome = new MultiSourceCatalogPlanner().Build(new[]
            {
                new PclsSourceCatalogBatch { Profile = legacy, Import = Success("game") },
                new PclsSourceCatalogBatch { Profile = remote, Import = Success("game") }
            });

            Assert.That(outcome.CanPublish, Is.True, outcome.RefusalReason);
            Assert.That(outcome.Games, Has.Count.EqualTo(2));
            Assert.That(outcome.Games[0].GameId, Is.EqualTo("game"));
            Assert.That(outcome.Games[1].GameId, Does.StartWith("pcls1-"));
            Assert.That(outcome.Games[1].GameId, Is.Not.EqualTo("game"));
            Assert.That(outcome.Routes[0].OriginalItemId, Is.EqualTo("game"));
            Assert.That(outcome.Routes[1].ProfileId, Is.EqualTo(remote.ProfileId));
            Assert.That(outcome.Routes[1].OriginalItemId, Is.EqualTo("game"));
        }

        [Test]
        public void ChangingSourceNameAndOrderKeepsNewGameIdsStable()
        {
            var first = Source("6a94c3b0-ae21-4f3b-a4eb-87bd6f5f8ba5");
            var second = Source("397ebc5e-83c4-4e4e-b8d4-fb3bd42b409a");
            var planner = new MultiSourceCatalogPlanner();
            var before = planner.Build(new[]
            {
                new PclsSourceCatalogBatch { Profile = first, Import = Success("one") },
                new PclsSourceCatalogBatch { Profile = second, Import = Success("two") }
            });
            first.DisplayName = "Renamed location";
            second.ProviderType = "RcloneRemote";
            var after = planner.Build(new[]
            {
                new PclsSourceCatalogBatch { Profile = second, Import = Success("two") },
                new PclsSourceCatalogBatch { Profile = first, Import = Success("one") }
            });

            Assert.That(before.CanPublish && after.CanPublish, Is.True);
            Assert.That(
                before.Routes.ToDictionary(x => x.ProfileId, x => x.GameId),
                Is.EquivalentTo(after.Routes.ToDictionary(x => x.ProfileId, x => x.GameId)));
        }

        [Test]
        public void UnavailableSourceRefusesPartialCatalogEvenWhenAnotherSourceIsHealthy()
        {
            var healthy = Source("6a94c3b0-ae21-4f3b-a4eb-87bd6f5f8ba5", true);
            var offline = Source("397ebc5e-83c4-4e4e-b8d4-fb3bd42b409a");
            var result = new MultiSourceCatalogPlanner().Build(new[]
            {
                new PclsSourceCatalogBatch { Profile = healthy, Import = Success("good") },
                new PclsSourceCatalogBatch
                {
                    Profile = offline,
                    Import = ImportOutcome.Failure(ImportFailureKind.SourceUnavailable,
                        "offline NAS", "Disconnected")
                }
            });

            Assert.That(result.CanPublish, Is.False);
            Assert.That(result.Games, Is.Empty, "Do not publish a partial successful import.");
            Assert.That(result.Routes, Is.Empty);
            Assert.That(result.RefusalReason, Does.Contain("unchanged"));
        }

        [Test]
        public void DisabledSourceCannotSilentlyRemovePreviouslyImportedGames()
        {
            var profile = Source("6a94c3b0-ae21-4f3b-a4eb-87bd6f5f8ba5", true);
            profile.Enabled = false;
            var result = new MultiSourceCatalogPlanner().Build(new[]
            {
                new PclsSourceCatalogBatch { Profile = profile, Import = Success("old") }
            });

            Assert.That(result.CanPublish, Is.False);
            Assert.That(result.Games, Is.Empty);
            Assert.That(result.RefusalReason, Does.Contain("retirement"));
        }

        [Test]
        public void RepeatedIdentityAndMismatchedManifestRefusePublication()
        {
            var profile = Source("6a94c3b0-ae21-4f3b-a4eb-87bd6f5f8ba5", true);
            var duplicate = new MultiSourceCatalogPlanner().Build(new[]
            {
                new PclsSourceCatalogBatch { Profile = profile, Import = Success("same", "same") }
            });
            Assert.That(duplicate.CanPublish, Is.False);

            var missingItem = ImportOutcome.Success("test",
                new[] { new PersonalCloudLibraryItem { Id = "real", Title = "real" } },
                new[] { new GameMetadata { GameId = "not-real", Name = "other" } });
            var mismatch = new MultiSourceCatalogPlanner().Build(new[]
            {
                new PclsSourceCatalogBatch { Profile = profile, Import = missingItem }
            });
            Assert.That(mismatch.CanPublish, Is.False);
            Assert.That(mismatch.Games, Is.Empty);
        }

        [Test]
        public void PlannerDoesNotMutateCallerGameMetadata()
        {
            var profile = Source("6a94c3b0-ae21-4f3b-a4eb-87bd6f5f8ba5");
            var original = Success("original");
            var beforeId = original.Games[0].GameId;
            var beforePath = original.Games[0].GameActions[0].Path;

            var result = new MultiSourceCatalogPlanner().Build(new[]
            {
                new PclsSourceCatalogBatch { Profile = profile, Import = original }
            });

            Assert.That(result.CanPublish, Is.True);
            Assert.That(result.Games[0], Is.Not.SameAs(original.Games[0]));
            Assert.That(original.Games[0].GameId, Is.EqualTo(beforeId));
            Assert.That(original.Games[0].GameActions[0].Path, Is.EqualTo(beforePath));
            Assert.That(result.Games[0].Name, Is.EqualTo(original.Games[0].Name));
            Assert.That(result.Games[0].InstallSize, Is.EqualTo(original.Games[0].InstallSize));
            Assert.That(result.Games[0].GameActions[0].WorkingDir,
                Is.EqualTo(original.Games[0].GameActions[0].WorkingDir));
        }
    }
}
