using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class ZipManagedRecoveryServiceTests
    {
        private string root, cache, archive;
        private ZipManagedExtractionPlan plan;
        private CacheOwnershipReceiptStore receipts;
        private ZipLauncherApprovalService approvals;
        private ZipManagedRecoveryService recovery;
        private readonly string gameId = "game";

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ZipRecovery",
                Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            archive = Path.Combine(root, "game.zip");
            using (var output = new FileStream(archive, FileMode.CreateNew,
                FileAccess.ReadWrite))
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
            {
                Add(zip, "Game.exe", "original game");
                Add(zip, "data/info.txt", "important game data");
            }
            receipts = new CacheOwnershipReceiptStore(Path.Combine(root, "receipts"));
            approvals = new ZipLauncherApprovalService(Path.Combine(root, "approvals"));
            recovery = new ZipManagedRecoveryService(Path.Combine(root, "journals"));
            var reviewed = new ZipPackageReviewService().Inspect(archive, "Game");
            Assert.That(reviewed.PassedPreflight, Is.True, reviewed.Reason);
            var extractor = new ZipManagedExtractionService();
            plan = extractor.Prepare(reviewed, archive, cache, gameId, receipts);
            Assert.That(plan, Is.Not.Null);
            var result = extractor.ExecuteApproved(plan, true,
                receipts, CancellationToken.None);
            Assert.That(result.Succeeded && result.ReceiptRecorded, Is.True, result.Reason);
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

        private bool Quarantine(bool yes = true)
        {
            string reason;
            return recovery.TryQuarantine(cache, gameId, receipts,
                approvals, yes, CancellationToken.None, out reason);
        }

        private bool Restore(bool yes = true)
        {
            string reason;
            return recovery.TryRestore(cache, gameId, receipts, approvals,
                yes, CancellationToken.None, out reason);
        }

        private string RecoveryDirectory()
        {
            return recovery.Inspect(cache, gameId).RecoveryDirectory;
        }

        [Test]
        public void ExplicitQuarantineMovesWholeCleanPackageWithImmutableJournal()
        {
            Assert.That(recovery.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.NoJournal));
            Assert.That(Quarantine(), Is.True);
            var report = recovery.Inspect(cache, gameId);
            Assert.That(report.State, Is.EqualTo(ZipRecoveryState.Quarantined), report.Reason);
            Assert.That(report.CanDeleteUnattended, Is.False);
            Assert.That(recovery.HasJournalFootprint(cache, gameId), Is.True);
            Assert.That(Directory.Exists(plan.Destination), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(report.RecoveryDirectory, "Game.exe")),
                Is.EqualTo("original game"));
            Assert.That(File.ReadAllText(Path.Combine(report.RecoveryDirectory,
                "data", "info.txt")), Is.EqualTo("important game data"));
            Assert.That(File.Exists(archive), Is.True);
            CacheOwnershipReceipt record;
            Assert.That(receipts.TryRead(cache, plan.ReceiptKey, out record), Is.True);
            Assert.That(record.Destination, Is.EqualTo(plan.Destination));
        }

        [Test]
        public void RestoreReturnsSameFolderWithoutRemovingOriginalReceiptOrJournal()
        {
            Assert.That(Quarantine(), Is.True);
            Assert.That(Restore(), Is.True);
            Assert.That(Directory.Exists(plan.Destination), Is.True);
            Assert.That(Directory.Exists(RecoveryDirectory()), Is.False);
            Assert.That(recovery.HasJournalFootprint(cache, gameId), Is.True);
            Assert.That(recovery.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.Restored));
            var marker = Directory.GetFiles(Path.Combine(root, "journals"),
                "*.restored").Single();
            Assert.That(File.ReadAllText(marker).Length, Is.EqualTo(32));
            Assert.That(File.ReadAllText(Path.Combine(plan.Destination, "Game.exe")),
                Is.EqualTo("original game"));
            string resumeReason;
            Assert.That(recovery.TryResumePrepared(cache, gameId, receipts,
                approvals, true, CancellationToken.None, out resumeReason), Is.False,
                "A completed restore must not be offered as a pending move.");
            CacheOwnershipReceipt record;
            Assert.That(receipts.TryRead(cache, plan.ReceiptKey, out record), Is.True);
            Assert.That(Restore(), Is.False);
        }

        [Test]
        public void MissingConfirmationDoesNotCreateJournalOrMoveFiles()
        {
            Assert.That(Quarantine(false), Is.False);
            Assert.That(recovery.HasJournalFootprint(cache, gameId), Is.False);
            Assert.That(Directory.Exists(plan.Destination), Is.True);
            Assert.That(File.Exists(archive), Is.True);
            Assert.That(Restore(false), Is.False);
        }

        [Test]
        public void NewSaveDataBlocksQuarantineAndPreservesProgress()
        {
            var save = Path.Combine(plan.Destination, "progress.sav");
            File.WriteAllText(save, "irreplaceable save");
            Assert.That(Quarantine(), Is.False);
            Assert.That(recovery.HasJournalFootprint(cache, gameId), Is.False);
            Assert.That(File.ReadAllText(save), Is.EqualTo("irreplaceable save"));
        }

        [Test]
        public void ModifiedOriginalExecutableCannotBeQuarantinedAutomatically()
        {
            File.WriteAllText(Path.Combine(plan.Destination, "Game.exe"), "modified");
            Assert.That(Quarantine(), Is.False);
            Assert.That(Directory.Exists(plan.Destination), Is.True);
            Assert.That(recovery.HasJournalFootprint(cache, gameId), Is.False);
        }

        [Test]
        public void ValidOrCorruptedLauncherApprovalBlocksRecovery()
        {
            var settings = new PersonalCloudLibrarySourceSettings
            {
                Enabled = true, LocalCacheFolder = cache,
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType
            };
            var item = new PersonalCloudLibraryItem
            {
                Id = gameId, Title = "Game", SourceType = "file",
                SourcePath = archive, CachePath = "game.zip"
            };
            string reason;
            Assert.That(approvals.TryApprove(item, settings, "Game.exe",
                receipts, true, CancellationToken.None, out reason), Is.True, reason);
            Assert.That(Quarantine(), Is.False);
            var record = Directory.GetFiles(Path.Combine(root, "approvals"),
                "*.json").Single();
            File.WriteAllText(record, "{ not valid json");
            Assert.That(approvals.HasApproval(gameId, cache), Is.False);
            Assert.That(approvals.HasApprovalFootprint(gameId, cache), Is.True);
            Assert.That(Quarantine(), Is.False);
            Assert.That(File.Exists(Path.Combine(plan.Destination, "Game.exe")), Is.True);
        }

        [Test]
        public void PreparedJournalCanBeExplicitlyResumedAfterSimulatedInterruptedMove()
        {
            Assert.That(Quarantine(), Is.True);
            var recovered = RecoveryDirectory();
            // Simulate crash after journal persisted but before the folder move.
            Directory.Move(recovered, plan.Destination);
            Assert.That(recovery.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.Prepared));
            string reason;
            Assert.That(recovery.TryResumePrepared(cache, gameId, receipts, approvals,
                false, CancellationToken.None, out reason), Is.False);
            Assert.That(Directory.Exists(plan.Destination), Is.True);
            Assert.That(recovery.TryResumePrepared(cache, gameId, receipts, approvals,
                true, CancellationToken.None, out reason), Is.True, reason);
            Assert.That(recovery.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.Quarantined));
            Assert.That(File.Exists(Path.Combine(recovered, "Game.exe")), Is.True);
        }

        [Test]
        public void ConflictingRestoredMarkerCannotReplayOrOverwriteQuarantine()
        {
            Assert.That(Quarantine(), Is.True);
            var recovered = RecoveryDirectory();
            File.WriteAllText(Directory.GetFiles(Path.Combine(root, "journals"),
                "*.json").Single() + ".restored", "invalid-transaction");
            Assert.That(recovery.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.Ambiguous));
            Assert.That(Restore(), Is.False);
            string reason;
            Assert.That(recovery.TryResumePrepared(cache, gameId, receipts,
                approvals, true, CancellationToken.None, out reason), Is.False);
            Assert.That(File.Exists(Path.Combine(recovered, "Game.exe")), Is.True);
        }

        [Test]
        public void CorruptedJournalRefusesRestoreAndPreservesRecoveryContent()
        {
            Assert.That(Quarantine(), Is.True);
            var recovered = RecoveryDirectory();
            var journal = Directory.GetFiles(Path.Combine(root, "journals"),
                "*.json").Single();
            File.WriteAllText(journal, "{corrupt");
            Assert.That(recovery.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.Ambiguous));
            Assert.That(Restore(), Is.False);
            Assert.That(File.Exists(Path.Combine(recovered, "Game.exe")), Is.True);
            Assert.That(File.Exists(archive), Is.True);
        }

        [Test]
        public void NewContentInRecoveryPreventsAutomaticRestoreWithoutDeletingIt()
        {
            Assert.That(Quarantine(), Is.True);
            var recovered = RecoveryDirectory();
            var save = Path.Combine(recovered, "later.sav");
            File.WriteAllText(save, "critical save");
            Assert.That(Restore(), Is.False);
            Assert.That(File.ReadAllText(save), Is.EqualTo("critical save"));
            Assert.That(Directory.Exists(plan.Destination), Is.False);
        }

        [Test]
        public void ExistingRecoveryDestinationCannotBeOverwritten()
        {
            var destination = RecoveryDirectory();
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "user.sav"), "keep");
            Assert.That(Quarantine(), Is.False);
            Assert.That(recovery.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.Ambiguous));
            Assert.That(File.ReadAllText(Path.Combine(destination, "user.sav")),
                Is.EqualTo("keep"));
            Assert.That(Directory.Exists(plan.Destination), Is.True);
        }

        [Test]
        public void RestoreRefusesOccupiedOriginalDestinationWithoutOverwritingEitherFolder()
        {
            Assert.That(Quarantine(), Is.True);
            var quarantined = RecoveryDirectory();
            Directory.CreateDirectory(plan.Destination);
            var userSave = Path.Combine(plan.Destination, "user-save.sav");
            File.WriteAllText(userSave, "keep the new save");
            Assert.That(Restore(), Is.False);
            Assert.That(File.ReadAllText(userSave), Is.EqualTo("keep the new save"));
            Assert.That(File.ReadAllText(Path.Combine(quarantined, "Game.exe")),
                Is.EqualTo("original game"));
            Assert.That(recovery.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.Ambiguous));
        }

        [Test]
        public void MissingOrCorruptOwnershipReceiptBlocksRestoreWithoutDataLoss()
        {
            Assert.That(Quarantine(), Is.True);
            var quarantined = RecoveryDirectory();
            var receiptPath = Directory.GetFiles(Path.Combine(root, "receipts"),
                "*.json").Single();
            File.WriteAllText(receiptPath, "{ invalid");
            Assert.That(Restore(), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(quarantined, "Game.exe")),
                Is.EqualTo("original game"));
            Assert.That(File.Exists(archive), Is.True);
        }

        [Test]
        public void CancelledRestorePreservesQuarantineAndJournal()
        {
            Assert.That(Quarantine(), Is.True);
            using (var stop = new CancellationTokenSource())
            {
                stop.Cancel();
                string reason;
                Assert.That(recovery.TryRestore(cache, gameId, receipts,
                    approvals, true, stop.Token, out reason), Is.False);
            }
            Assert.That(recovery.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.Quarantined));
            Assert.That(recovery.HasJournalFootprint(cache, gameId), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(RecoveryDirectory(), "Game.exe")),
                Is.EqualTo("original game"));
        }

        [Test]
        public void WrongGameIdAndCacheCannotUseAnotherGamesJournal()
        {
            Assert.That(Quarantine(), Is.True);
            string reason;
            Assert.That(recovery.TryRestore(cache, "other-game", receipts,
                approvals, true, CancellationToken.None, out reason), Is.False);
            Assert.That(recovery.TryRestore(cache, "../game", receipts,
                approvals, true, CancellationToken.None, out reason), Is.False);
            var otherCache = Path.Combine(root, "other-cache");
            Directory.CreateDirectory(otherCache);
            Assert.That(recovery.TryRestore(otherCache, gameId, receipts,
                approvals, true, CancellationToken.None, out reason), Is.False);
            Assert.That(recovery.Inspect(cache, gameId).State,
                Is.EqualTo(ZipRecoveryState.Quarantined));
        }

        [Test]
        public void RecoveryJournalStoreInsideCacheIsNotAllowed()
        {
            var unsafeStore = new ZipManagedRecoveryService(
                Path.Combine(cache, "recovery-journal-store"));
            string reason;
            Assert.That(unsafeStore.TryQuarantine(cache, gameId, receipts,
                approvals, true, CancellationToken.None, out reason), Is.False);
            Assert.That(Directory.Exists(plan.Destination), Is.True);
            Assert.That(Directory.Exists(Path.Combine(cache,
                "recovery-journal-store")), Is.False);
        }

        [Test]
        public void CancellationDoesNotMoveAnything()
        {
            using (var stop = new CancellationTokenSource())
            {
                stop.Cancel();
                string reason;
                Assert.That(recovery.TryQuarantine(cache, gameId, receipts,
                    approvals, true, stop.Token, out reason), Is.False);
            }
            Assert.That(recovery.HasJournalFootprint(cache, gameId), Is.False);
            Assert.That(File.Exists(Path.Combine(plan.Destination, "Game.exe")), Is.True);
        }

        [Test]
        public void RepeatedQuarantineCannotOverwriteExistingJournalOrRecovery()
        {
            Assert.That(Quarantine(), Is.True);
            Assert.That(Quarantine(), Is.False);
            Assert.That(Restore(), Is.True);
            Assert.That(Quarantine(), Is.False,
                "The immutable journal forbids silent new transaction reuse.");
            Assert.That(File.Exists(Path.Combine(plan.Destination, "Game.exe")), Is.True);
        }
    }
}
