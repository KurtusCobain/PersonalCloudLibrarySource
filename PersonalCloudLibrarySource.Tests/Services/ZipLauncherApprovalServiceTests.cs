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
