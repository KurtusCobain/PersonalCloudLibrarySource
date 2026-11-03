using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class ZipManagedPermanentCleanupServiceTests
    {
        private string root;
        private string cache;
        private string archive;
        private string recoveryRoot;
        private string quarantine;
        private string gameId;
        private CacheOwnershipReceiptStore receipts;
        private ZipLauncherApprovalService approvals;
        private ZipManagedRecoveryService recover;
        private ZipManagedPermanentCleanupService cleanup;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ZipPurge",
                Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            archive = Path.Combine(root, "original.zip");
            gameId = "game";
            recoveryRoot = Path.Combine(root, "journals");
            receipts = new CacheOwnershipReceiptStore(Path.Combine(root, "receipts"));
            approvals = new ZipLauncherApprovalService(Path.Combine(root, "approvals"));
            recover = new ZipManagedRecoveryService(recoveryRoot);
            cleanup = new ZipManagedPermanentCleanupService(recoveryRoot);

            using (var stream = new FileStream(archive, FileMode.CreateNew, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                Add(zip, "Game.exe", "game original binary");
                Add(zip, "assets/content.dat", "original asset");
                Add(zip, "assets/info.txt", "original information");
            }
            var review = new ZipPackageReviewService().Inspect(archive, "Game");
            Assert.That(review.PassedPreflight, Is.True, review.Reason);
            var extract = new ZipManagedExtractionService();
            var plan = extract.Prepare(review, archive, cache, gameId, receipts);
            Assert.That(plan, Is.Not.Null);
            var written = extract.ExecuteApproved(plan, true,
                receipts, CancellationToken.None);
            Assert.That(written.Succeeded && written.ReceiptRecorded, Is.True, written.Reason);
            string reason;
            Assert.That(recover.TryQuarantine(cache, gameId, receipts,
                approvals, true, CancellationToken.None, out reason),
                Is.True, reason);
            quarantine = recover.Inspect(cache, gameId).RecoveryDirectory;
            Assert.That(Directory.Exists(quarantine), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private static void Add(ZipArchive zip, string path, string text)
        {
            using (var output = zip.CreateEntry(path).Open())
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                output.Write(bytes, 0, bytes.Length);
            }
        }

        private ZipCleanupResult Preview()
        {
            return cleanup.Preview(cache, gameId, receipts,
                approvals, CancellationToken.None);
        }

        private ZipCleanupResult Purge(bool confirmed = true,
            CancellationToken token = default(CancellationToken),
            Action<int> callback = null)
        {
            return cleanup.Execute(cache, gameId, receipts, approvals,
                confirmed, token, callback);
        }

        [Test]
        public void ExplicitRemovalReclaimsOnlyQuarantinedOriginalFilesAndKeepsSource()
        {
            var preview = Preview();
            Assert.That(preview.RequiresReview, Is.False, preview.Reason);
            Assert.That(preview.TotalFiles, Is.EqualTo(3));
            var removed = Purge();
            Assert.That(removed.Succeeded, Is.True, removed.Reason);
            Assert.That(removed.TotalFiles, Is.EqualTo(3));
            Assert.That(Directory.Exists(quarantine), Is.False);
            Assert.That(File.Exists(archive), Is.True);
            Assert.That(recover.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.Purged));
            Assert.That(File.Exists(Directory.GetFiles(recoveryRoot, "*.purge.json")
                .Single()), Is.True);
            Assert.That(Directory.GetFiles(recoveryRoot, "*.del.*").Length,
                Is.EqualTo(3));
            Assert.That(Directory.GetFiles(recoveryRoot, "*.purged").Length,
                Is.EqualTo(1));
            CacheOwnershipReceipt receipt;
            Assert.That(receipts.TryRead(cache,
                ZipManagedExtractionService.ReceiptKeyForGame(gameId), out receipt),
                Is.True, "The V2 ownership audit is preserved.");
            Assert.That(removed.CanDeleteUnattended, Is.False);
        }

        [Test]
        public void RefusingConsentDoesNotCreatePurgeManifestOrDeleteFiles()
        {
            var result = Purge(false);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.GetFiles(recoveryRoot, "*.purge.json"), Is.Empty);
            Assert.That(File.ReadAllText(Path.Combine(quarantine, "Game.exe")),
                Is.EqualTo("game original binary"));
            Assert.That(File.Exists(archive), Is.True);
        }

        [Test]
        public void AddedSaveBlocksCleanupAndPreservesEveryFile()
        {
            var saved = Path.Combine(quarantine, "progress.sav");
            File.WriteAllText(saved, "valuable user progress");
            Assert.That(Preview().RequiresReview, Is.True);
            Assert.That(Purge().Succeeded, Is.False);
            Assert.That(Directory.GetFiles(recoveryRoot, "*.purge.json"), Is.Empty);
            Assert.That(File.ReadAllText(saved), Is.EqualTo("valuable user progress"));
            Assert.That(File.ReadAllText(Path.Combine(quarantine, "Game.exe")),
                Is.EqualTo("game original binary"));
        }

        [Test]
        public void ModifiedOriginalFileBlocksCleanupBeforeAnyDeletion()
        {
            File.WriteAllText(Path.Combine(quarantine, "Game.exe"), "modified game");
            Assert.That(Purge().Succeeded, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(quarantine, "Game.exe")),
                Is.EqualTo("modified game"));
            Assert.That(File.Exists(Path.Combine(quarantine, "assets", "info.txt")),
                Is.True);
        }

        [Test]
        public void InterruptedCleanupCanResumeUsingOnlyRecordedDeletionIntents()
        {
            using (var cancel = new CancellationTokenSource())
            {
                var partial = Purge(true, cancel.Token, count =>
                {
                    if (count == 1) cancel.Cancel();
                });
                Assert.That(partial.Succeeded, Is.False);
                Assert.That(partial.RemovedFiles, Is.EqualTo(1));
            }
            Assert.That(recover.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.CleanupPending));
            Assert.That(File.Exists(archive), Is.True);
            var resumed = Purge();
            Assert.That(resumed.Succeeded, Is.True, resumed.Reason);
            Assert.That(recover.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.Purged));
            Assert.That(Directory.Exists(quarantine), Is.False);
        }

        [Test]
        public void UnknownFileArrivingAfterPartialCleanupBlocksResume()
        {
            using (var cancel = new CancellationTokenSource())
                Assert.That(Purge(true, cancel.Token, count => cancel.Cancel())
                    .Succeeded, Is.False);
            var saved = Path.Combine(quarantine, "important.sav");
            File.WriteAllText(saved, "do not remove");
            Assert.That(Purge().Succeeded, Is.False);
            Assert.That(File.ReadAllText(saved), Is.EqualTo("do not remove"));
            Assert.That(Directory.Exists(quarantine), Is.True);
            Assert.That(recover.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.CleanupPending));
        }

        [Test]
        public void ForgedOrCorruptedFileIntentBlocksResumeAndPreservesRemainingFiles()
        {
            using (var cancel = new CancellationTokenSource())
                Assert.That(Purge(true, cancel.Token, count => cancel.Cancel())
                    .Succeeded, Is.False);
            var intent = Directory.GetFiles(recoveryRoot, "*.del.*").Single();
            File.WriteAllText(intent, "forged");
            var remaining = Directory.GetFiles(quarantine, "*",
                SearchOption.AllDirectories);
            Assert.That(Purge().Succeeded, Is.False);
            Assert.That(remaining.Any(File.Exists), Is.True);
            Assert.That(File.ReadAllText(intent), Is.EqualTo("forged"));
        }

        [Test]
        public void InvalidReceiptBlocksFirstRemovalWithoutCreatingJournal()
        {
            var file = Directory.GetFiles(Path.Combine(root, "receipts"),
                "*.json").Single();
            File.WriteAllText(file, "{ corrupted");
            Assert.That(Purge().Succeeded, Is.False);
            Assert.That(Directory.GetFiles(recoveryRoot, "*.purge.json"), Is.Empty);
            Assert.That(File.Exists(Path.Combine(quarantine, "Game.exe")), Is.True);
        }

        [Test]
        public void CorruptRecoveryJournalCannotPermanentlyDeletePackage()
        {
            var file = Directory.GetFiles(recoveryRoot, "*.json")
                .Where(x => !x.EndsWith(".purge.json")).Single();
            File.WriteAllText(file, "{ corrupted");
            Assert.That(Purge().Succeeded, Is.False);
            Assert.That(File.Exists(Path.Combine(quarantine, "Game.exe")), Is.True);
        }

        [Test]
        public void CorruptMatchingLauncherApprovalFootprintBlocksAllDeletion()
        {
            var folder = Path.Combine(root, "approvals");
            Directory.CreateDirectory(folder);
            var identity = Path.GetFullPath(cache).ToUpperInvariant() +
                "\n" + gameId.ToUpperInvariant();
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(identity))).Replace("-", "");
            var record = Path.Combine(folder, hash + ".json");
            File.WriteAllText(record, "{corrupt approval");
            Assert.That(approvals.HasApprovalFootprint(gameId, cache), Is.True);
            Assert.That(Purge().Succeeded, Is.False);
            Assert.That(File.Exists(Path.Combine(quarantine, "Game.exe")), Is.True);
            Assert.That(Directory.GetFiles(recoveryRoot, "*.purge.json"), Is.Empty);
            // The unknown record is not removed by cleanup. The user must
            // resolve it separately; this test can then finish the fixture.
            Assert.That(File.ReadAllText(record), Is.EqualTo("{corrupt approval"));
        }

        [Test]
        public void RepeatedPurgeDoesNotReplayOrDeleteUserFiles()
        {
            Assert.That(Purge().Succeeded, Is.True);
            Assert.That(Purge().Succeeded, Is.False);
            var unrelated = Path.Combine(cache, "valuable.txt");
            File.WriteAllText(unrelated, "keep");
            Assert.That(Purge().Succeeded, Is.False);
            Assert.That(File.ReadAllText(unrelated), Is.EqualTo("keep"));
        }

        [Test]
        public void WrongGameIdentityCannotPurgeAnotherGame()
        {
            var result = cleanup.Execute(cache, "other", receipts, approvals,
                true, CancellationToken.None);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(File.Exists(Path.Combine(quarantine, "Game.exe")), Is.True);
        }
    }
}
