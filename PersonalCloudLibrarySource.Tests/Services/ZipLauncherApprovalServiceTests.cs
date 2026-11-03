using NUnit.Framework;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class ZipLauncherApprovalServiceTests
    {
        private string root;
        private string cache;
        private string archive;
        private ZipManagedExtractionPlan extracted;
        private CacheOwnershipReceiptStore receipts;
        private ZipLauncherApprovalService approvals;
        private PersonalCloudLibraryItem item;
        private PersonalCloudLibrarySourceSettings settings;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ZipApproval",
                Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            archive = Path.Combine(root, "game.zip");
            using (var file = new FileStream(archive, FileMode.CreateNew, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                Add(zip, "Game.exe", "original executable");
                Add(zip, "setup.exe", "do not approve installer");
                Add(zip, "readme.txt", "instructions");
            }
            receipts = new CacheOwnershipReceiptStore(Path.Combine(root, "receipts"));
            approvals = new ZipLauncherApprovalService(Path.Combine(root, "approvals"));
            item = new PersonalCloudLibraryItem
            {
                Id = "game", Title = "Game", SourceType = "file",
                SourcePath = archive, CachePath = "source/game.zip"
            };
            settings = new PersonalCloudLibrarySourceSettings
            {
                Enabled = true, LocalCacheFolder = cache,
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = Path.Combine(root, "catalog.json")
            };
            var review = new ZipPackageReviewService().Inspect(archive, "Game");
            Assert.That(review.PassedPreflight, Is.True, review.Reason);
            extracted = new ZipManagedExtractionService().Prepare(
                review, archive, cache, item.Id, receipts);
            Assert.That(extracted, Is.Not.Null);
            var result = new ZipManagedExtractionService().ExecuteApproved(
                extracted, true, receipts, CancellationToken.None);
            Assert.That(result.ReceiptRecorded, Is.True, result.Reason);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private static void Add(ZipArchive zip, string name, string content)
        {
            using (var stream = zip.CreateEntry(name).Open())
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private GameMetadata Game() => new GameMetadata
        {
            GameId = item.Id, Name = item.Title, IsInstalled = false
        };

        private bool Approve(string candidate = "Game.exe", bool confirmed = true)
        {
            string reason;
            return approvals.TryApprove(item, settings, candidate,
                receipts, confirmed, CancellationToken.None, out reason);
        }

        [Test]
        public void ExplicitApprovalAfterVerifiedExtractionCreatesPlayableMetadataOnlyOnImport()
        {
            var game = Game();
            Assert.That(approvals.TryApply(item, settings, game, receipts), Is.False);
            Assert.That(game.IsInstalled, Is.False);

            Assert.That(Approve(), Is.True);
            Assert.That(approvals.TryApply(item, settings, game, receipts), Is.True);
            Assert.That(game.IsInstalled, Is.True);
            Assert.That(game.InstallDirectory, Is.EqualTo(extracted.Destination));
            Assert.That(game.GameActions.Count, Is.EqualTo(1));
            Assert.That(game.GameActions[0].IsPlayAction, Is.True);
            Assert.That(game.GameActions[0].Path,
                Is.EqualTo(Path.Combine(extracted.Destination, "Game.exe")));
            Assert.That(game.GameActions[0].WorkingDir, Is.EqualTo(extracted.Destination));
            Assert.That(File.ReadAllText(archive).Length, Is.GreaterThan(0));
            Assert.That(File.ReadAllText(Path.Combine(extracted.Destination, "Game.exe")),
                Is.EqualTo("original executable"));
        }

        [Test]
        public void KeepChoiceCannotCreateApprovalOrPlayAction()
        {
            var before = Directory.Exists(Path.Combine(root, "approvals"));
            Assert.That(Approve(confirmed: false), Is.False);
            Assert.That(Directory.Exists(Path.Combine(root, "approvals")), Is.EqualTo(before));
            var game = Game();
            Assert.That(approvals.TryApply(item, settings, game, receipts), Is.False);
            Assert.That(game.IsInstalled, Is.False);
        }

        [Test]
        public void InstallerAndUnknownExecutableCannotBeApproved()
        {
            Assert.That(Approve("setup.exe"), Is.False);
            Assert.That(Approve("unlisted.exe"), Is.False);
            Assert.That(Approve("readme.txt"), Is.False);
            Assert.That(Approve("Game.exe/../setup.exe"), Is.False);
            Assert.That(Directory.Exists(Path.Combine(root, "approvals")), Is.False);
        }

        [Test]
        public void SourceOrManifestBindingEditRevokesEffectiveActivation()
        {
            Assert.That(Approve(), Is.True);
            item.SourcePath = Path.Combine(root, "changed.zip");
            Assert.That(approvals.TryApply(item, settings, Game(), receipts), Is.False);
            item.SourcePath = archive;
            settings.LocalManifestPath = Path.Combine(root, "another.json");
            Assert.That(approvals.TryApply(item, settings, Game(), receipts), Is.False);
        }

        [Test]
        public void ChangedOrAddedFilesRefuseApprovedPlayActionWithoutDeletion()
        {
            Assert.That(Approve(), Is.True);
            var userSave = Path.Combine(extracted.Destination, "save.sav");
            File.WriteAllText(userSave, "valuable");
            var game = Game();
            Assert.That(approvals.TryApply(item, settings, game, receipts), Is.False);
            Assert.That(game.IsInstalled, Is.False);
            Assert.That(File.ReadAllText(userSave), Is.EqualTo("valuable"));
            File.Delete(userSave);
            File.WriteAllText(Path.Combine(extracted.Destination, "Game.exe"), "modified");
            Assert.That(approvals.TryApply(item, settings, Game(), receipts), Is.False);
        }

        [Test]
        public void ExistingPlayActionsAndInstalledGamesAreNeverOverridden()
        {
            Assert.That(Approve(), Is.True);
            var existing = Game();
            existing.IsInstalled = true;
            Assert.That(approvals.TryApply(item, settings, existing, receipts), Is.False);
            var custom = Game();
            custom.GameActions = new List<GameAction>
            {
                new GameAction { Name = "Custom" }
            };
            Assert.That(approvals.TryApply(item, settings, custom, receipts), Is.False);
            Assert.That(custom.GameActions.Single().Name, Is.EqualTo("Custom"));
        }

        [Test]
        public void DifferentGameIdentityCannotBorrowApprovedLauncher()
        {
            Assert.That(Approve(), Is.True);
            var other = Game();
            other.GameId = "unrelated";
            Assert.That(approvals.TryApply(item, settings, other, receipts), Is.False);
            Assert.That(other.IsInstalled, Is.False);
        }

        [Test]
        public void ApprovalIsCreateOnlyAndCannotSilentlyChangeCandidate()
        {
            Assert.That(Approve(), Is.True);
            Assert.That(Approve("Game.exe"), Is.False);
            Assert.That(Approve("setup.exe"), Is.False);
            Assert.That(approvals.TryApply(item, settings, Game(), receipts), Is.True);
        }

        [Test]
        public void ChangedCacheRootOrMissingReceiptsFailClosed()
        {
            Assert.That(Approve(), Is.True);
            settings.LocalCacheFolder = Path.Combine(root, "other-cache");
            Directory.CreateDirectory(settings.LocalCacheFolder);
            Assert.That(approvals.TryApply(item, settings, Game(), receipts), Is.False);
            settings.LocalCacheFolder = cache;
            var empty = new CacheOwnershipReceiptStore(Path.Combine(root, "empty-receipts"));
            Assert.That(approvals.TryApply(item, settings, Game(), empty), Is.False);
        }

        [Test]
        public void CancelledApprovalNeverWritesFile()
        {
            using (var token = new CancellationTokenSource())
            {
                token.Cancel();
                string reason;
                Assert.That(approvals.TryApprove(item, settings, "Game.exe",
                    receipts, true, token.Token, out reason), Is.False);
            }
            Assert.That(Directory.Exists(Path.Combine(root, "approvals")), Is.False);
        }

        [Test]
        public void ExplicitRevocationRemovesOnlyApprovalAndNextImportCannotActivate()
        {
            Assert.That(Approve(), Is.True);
            Assert.That(approvals.HasApproval(item.Id, cache), Is.True);
            Assert.That(approvals.TryApply(item, settings, Game(), receipts), Is.True);
            string reason;
            Assert.That(approvals.TryRevoke(item.Id, cache, true, out reason),
                Is.True, reason);
            Assert.That(approvals.HasApproval(item.Id, cache), Is.False);
            var reimport = Game();
            Assert.That(approvals.TryApply(item, settings, reimport, receipts),
                Is.False);
            Assert.That(reimport.IsInstalled, Is.False);
            Assert.That(File.Exists(archive), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(extracted.Destination, "Game.exe")),
                Is.EqualTo("original executable"));
            CacheOwnershipReceipt receipt;
            Assert.That(receipts.TryRead(cache, extracted.ReceiptKey, out receipt),
                Is.True, "Revoking launch approval must not erase extraction ownership.");
        }

        [Test]
        public void MissingConsentCannotRevokeAnExistingApproval()
        {
            Assert.That(Approve(), Is.True);
            string reason;
            Assert.That(approvals.TryRevoke(item.Id, cache, false, out reason),
                Is.False);
            Assert.That(approvals.HasApproval(item.Id, cache), Is.True);
            Assert.That(approvals.TryApply(item, settings, Game(), receipts), Is.True);
        }

        [Test]
        public void StaleSourceApprovalCanStillBeRevokedWithoutSourceAccess()
        {
            Assert.That(Approve(), Is.True);
            item.SourcePath = Path.Combine(root, "changed.zip");
            Assert.That(approvals.TryApply(item, settings, Game(), receipts),
                Is.False);
            string reason;
            Assert.That(approvals.TryRevoke(item.Id, cache, true, out reason),
                Is.True, reason);
            Assert.That(approvals.HasApproval(item.Id, cache), Is.False);
            Assert.That(Directory.Exists(extracted.Destination), Is.True);
        }

        [Test]
        public void WrongGameIdentityCannotRevokeAnotherGamesApproval()
        {
            Assert.That(Approve(), Is.True);
            string reason;
            Assert.That(approvals.TryRevoke("other", cache, true, out reason),
                Is.False);
            Assert.That(approvals.TryRevoke("../game", cache, true, out reason),
                Is.False);
            Assert.That(approvals.HasApproval(item.Id, cache), Is.True);
        }

        [Test]
        public void WrongCacheRootCannotRevokeAnotherCachesApproval()
        {
            Assert.That(Approve(), Is.True);
            var anotherCache = Path.Combine(root, "another-cache");
            Directory.CreateDirectory(anotherCache);
            string reason;
            Assert.That(approvals.TryRevoke(item.Id, anotherCache, true, out reason),
                Is.False);
            Assert.That(approvals.HasApproval(item.Id, cache), Is.True);
        }

        [Test]
        public void CorruptedApprovalIsNeverDeletedByRevocation()
        {
            Assert.That(Approve(), Is.True);
            var record = Directory.GetFiles(Path.Combine(root, "approvals"),
                "*.json").Single();
            File.WriteAllText(record, "{ invalid json");
            string reason;
            Assert.That(approvals.TryRevoke(item.Id, cache, true, out reason),
                Is.False);
            Assert.That(File.ReadAllText(record), Is.EqualTo("{ invalid json"));
            Assert.That(File.Exists(Path.Combine(extracted.Destination, "Game.exe")),
                Is.True);
        }

        [Test]
        public void ReapprovalAfterExplicitRevocationIsAllowed()
        {
            Assert.That(Approve(), Is.True);
            string reason;
            Assert.That(approvals.TryRevoke(item.Id, cache, true, out reason),
                Is.True, reason);
            Assert.That(Approve(), Is.True);
            Assert.That(approvals.TryApply(item, settings, Game(), receipts), Is.True);
        }

        [Test]
        public void RevocationLeavesUserCreatedSaveInExtractedDirectory()
        {
            Assert.That(Approve(), Is.True);
            var save = Path.Combine(extracted.Destination, "progress.sav");
            File.WriteAllText(save, "keep this");
            string reason;
            Assert.That(approvals.TryRevoke(item.Id, cache, true, out reason),
                Is.True, reason);
            Assert.That(File.ReadAllText(save), Is.EqualTo("keep this"));
            Assert.That(File.Exists(archive), Is.True);
        }

        [Test]
        public void StoreUnderCacheCannotRevokeOrDeleteAnything()
        {
            Assert.That(Approve(), Is.True);
            var invalid = new ZipLauncherApprovalService(
                Path.Combine(cache, "approval-files"));
            string reason;
            Assert.That(invalid.TryRevoke(item.Id, cache, true, out reason),
                Is.False);
            Assert.That(approvals.HasApproval(item.Id, cache), Is.True);
        }

        private Guid guardedPluginId = Guid.NewGuid();

        private Playnite.SDK.Models.Game GuardedGame()
        {
            return new Playnite.SDK.Models.Game("Guarded Game")
            {
                PluginId = guardedPluginId, GameId = item.Id
            };
        }

        private GameAction ApprovedPlayAction()
        {
            Assert.That(Approve(), Is.True);
            var metadata = Game();
            Assert.That(approvals.TryApply(item, settings, metadata, receipts), Is.True);
            return metadata.GameActions.Single();
        }

        private ZipManagedLaunchDecision CheckLaunch(GameAction selected,
            Playnite.SDK.Models.Game game = null)
        {
            return new ZipManagedLaunchGate().Evaluate(
                guardedPluginId, game ?? GuardedGame(), selected,
                item, settings, receipts, approvals);
        }

        [Test]
        public void ExactApprovedLauncherIsVerifiedAgainJustBeforePlay()
        {
            var play = ApprovedPlayAction();
            var outcome = CheckLaunch(play);
            Assert.That(outcome.Applies, Is.True);
            Assert.That(outcome.Allowed, Is.True, outcome.Reason);
            Assert.That(File.Exists(play.Path), Is.True);
        }

        [Test]
        public void AManagedZipActionWithoutApprovalIsBlockedAtLaunchTime()
        {
            var play = new GameAction
            {
                Name = "Play", Type = GameActionType.File,
                Path = Path.Combine(extracted.Destination, "Game.exe"),
                WorkingDir = extracted.Destination,
                IsPlayAction = true
            };
            var outcome = CheckLaunch(play);
            Assert.That(outcome.Applies, Is.True);
            Assert.That(outcome.Allowed, Is.False);
        }

        [Test]
        public void RevokedApprovalBlocksPreviouslyImportedStalePlayAction()
        {
            var oldAction = ApprovedPlayAction();
            string reason;
            Assert.That(approvals.TryRevoke(item.Id, cache, true, out reason),
                Is.True, reason);
            var result = CheckLaunch(oldAction);
            Assert.That(result.Applies, Is.True);
            Assert.That(result.Allowed, Is.False);
        }

        [Test]
        public void ChangedExecutableBytesBlockPreviouslyApprovedLaunch()
        {
            var play = ApprovedPlayAction();
            File.WriteAllText(play.Path, "new executable contents");
            Assert.That(CheckLaunch(play).Allowed, Is.False);
        }

        [Test]
        public void UserSaveAddedAfterImportBlocksStaleLaunchWithoutDeletion()
        {
            var play = ApprovedPlayAction();
            var save = Path.Combine(extracted.Destination, "progress.sav");
            File.WriteAllText(save, "irreplaceable");
            var result = CheckLaunch(play);
            Assert.That(result.Applies, Is.True);
            Assert.That(result.Allowed, Is.False);
            Assert.That(File.ReadAllText(save), Is.EqualTo("irreplaceable"));
        }

        [Test]
        public void ChangedSourceBindingBlocksStalePlayAction()
        {
            var play = ApprovedPlayAction();
            item.SourcePath = Path.Combine(root, "different.zip");
            Assert.That(CheckLaunch(play).Allowed, Is.False);
            Assert.That(File.Exists(play.Path), Is.True);
        }

        [Test]
        public void ChangedCacheRootStillRecognizesAndBlocksStaleManagedPath()
        {
            var play = ApprovedPlayAction();
            settings.LocalCacheFolder = Path.Combine(root, "other-cache");
            Directory.CreateDirectory(settings.LocalCacheFolder);
            var result = CheckLaunch(play);
            Assert.That(result.Applies, Is.True);
            Assert.That(result.Allowed, Is.False);
        }

        [Test]
        public void TamperedArgumentsOrWorkingDirectoryBlockManagedPlay()
        {
            var approved = ApprovedPlayAction();
            var altered = new GameAction
            {
                Name = "Play", Type = GameActionType.File, IsPlayAction = true,
                Path = approved.Path, WorkingDir = approved.WorkingDir,
                Arguments = "--unexpected"
            };
            Assert.That(CheckLaunch(altered).Allowed, Is.False);
            altered.Arguments = string.Empty;
            altered.WorkingDir = root;
            Assert.That(CheckLaunch(altered).Allowed, Is.False);
        }

        [Test]
        public void UnrelatedGamePlayActionIsNotIntercepted()
        {
            var approved = ApprovedPlayAction();
            var otherAction = new GameAction
            {
                Name = "Other", Type = GameActionType.File, IsPlayAction = true,
                Path = Path.Combine(root, "unrelated.exe"),
                WorkingDir = root
            };
            var result = CheckLaunch(otherAction);
            Assert.That(result.Applies, Is.False);
            Assert.That(result.Allowed, Is.False);
            var foreign = GuardedGame();
            foreign.PluginId = Guid.NewGuid();
            var foreignResult = CheckLaunch(approved, foreign);
            Assert.That(foreignResult.Applies, Is.False);
        }

        [Test]
        public void ScriptsTargetingManagedExecutableAreNeverApproved()
        {
            var approved = ApprovedPlayAction();
            var script = new GameAction
            {
                Name = "Script", Type = GameActionType.Script,
                Path = approved.Path, WorkingDir = approved.WorkingDir,
                IsPlayAction = true
            };
            var decision = CheckLaunch(script);
            Assert.That(decision.Applies, Is.True);
            Assert.That(decision.Allowed, Is.False);
        }

        [Test]
        public void DisabledProviderBlocksPreviouslyApprovedManagedPlay()
        {
            var approved = ApprovedPlayAction();
            settings.Enabled = false;
            var result = CheckLaunch(approved);
            Assert.That(result.Applies, Is.True);
            Assert.That(result.Allowed, Is.False);
        }

        [Test]
        public void MissingCurrentManifestItemBlocksManagedPlay()
        {
            var approved = ApprovedPlayAction();
            var result = new ZipManagedLaunchGate().Evaluate(
                guardedPluginId, GuardedGame(), approved,
                null, settings, receipts, approvals);
            Assert.That(result.Applies, Is.True);
            Assert.That(result.Allowed, Is.False);
        }

        [Test]
        public void ManagedInstallDirectoryBlocksRewrittenUnrelatedPlayAction()
        {
            var approved = ApprovedPlayAction();
            var game = GuardedGame();
            game.InstallDirectory = extracted.Destination;
            var replacedAction = new GameAction
            {
                Name = "Changed", Type = GameActionType.File,
                Path = Path.Combine(root, "different-launch.exe"),
                WorkingDir = root, IsPlayAction = true
            };
            var result = CheckLaunch(replacedAction, game);
            Assert.That(result.Applies, Is.True);
            Assert.That(result.Allowed, Is.False);
            Assert.That(File.Exists(approved.Path), Is.True);
        }

        [Test]
        public void ManagedInstallWithoutSelectedSourceActionFailsClosed()
        {
            ApprovedPlayAction();
            var game = GuardedGame();
            game.InstallDirectory = extracted.Destination;
            var result = CheckLaunch(null, game);
            Assert.That(result.Applies, Is.True);
            Assert.That(result.Allowed, Is.False);
        }

        [Test]
        public void ExperimentalMultiSourceModeNeverUsesLegacyZipLauncherApproval()
        {
            var approved = ApprovedPlayAction();
            var multi = new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true,
                EnableMultiSourceProfiles = true,
                LocalCacheFolder = cache,
                SourceProviderType =
                    PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = settings.LocalManifestPath
            };
            var decision = new ZipManagedLaunchGate().Evaluate(
                guardedPluginId, GuardedGame(), approved,
                item, multi, receipts, approvals);
            Assert.That(decision.Applies, Is.True);
            Assert.That(decision.Allowed, Is.False,
                "Source-bound game IDs require independent ZIP approval provenance.");
            Assert.That(File.Exists(approved.Path), Is.True);
        }

        [Test]
        public void ApprovalStoreInsideCacheCannotActivateOrWrite()
        {
            var invalid = new ZipLauncherApprovalService(
                Path.Combine(cache, "approval-files"));
            string reason;
            Assert.That(invalid.TryApprove(item, settings, "Game.exe",
                receipts, true, CancellationToken.None, out reason), Is.False);
            Assert.That(Directory.Exists(Path.Combine(cache, "approval-files")), Is.False);
            Assert.That(invalid.TryApply(item, settings, Game(), receipts), Is.False);
        }
    }
}
