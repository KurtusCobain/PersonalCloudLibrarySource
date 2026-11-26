using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public sealed class ZipManagedExtractionServiceTests
    {
        private string root;
        private string archive;
        private string cache;
        private CacheOwnershipReceiptStore receipts;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ZipManaged",
                Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            archive = Path.Combine(root, "game.zip");
            using (var stream = new FileStream(archive, FileMode.CreateNew, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("game.exe", CompressionLevel.Optimal);
                using (var payload = entry.Open())
                {
                    var bytes = Encoding.UTF8.GetBytes("verified fixture bytes");
                    payload.Write(bytes, 0, bytes.Length);
                }
            }
            receipts = new CacheOwnershipReceiptStore(Path.Combine(root, "receipts"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private ZipManagedExtractionPlan Plan(string gameId = "game")
        {
            var review = new ZipPackageReviewService().Inspect(archive, "game");
            Assert.That(review.PassedPreflight, Is.True, review.Reason);
            var plan = new ZipManagedExtractionService().Prepare(
                review, archive, cache, gameId, receipts);
            Assert.That(plan, Is.Not.Null);
            return plan;
        }

        [Test]
        public void ApprovedExtractionPublishesNewFolderAndInventoryReceiptOnly()
        {
            var plan = Plan();
            var result = new ZipManagedExtractionService().ExecuteApproved(
                plan, true, receipts, CancellationToken.None);
            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.ReceiptRecorded, Is.True);
            Assert.That(result.NeedsManualAttention, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(plan.Destination, "game.exe")),
                Is.EqualTo("verified fixture bytes"));
            Assert.That(File.Exists(archive), Is.True);
            CacheOwnershipReceipt ownership;
            Assert.That(receipts.TryRead(cache, plan.ReceiptKey, out ownership), Is.True);
            Assert.That(ownership.Version, Is.EqualTo(2));
            Assert.That(ownership.Destination, Is.EqualTo(plan.Destination));
            Assert.That(ownership.InventoryFileCount, Is.EqualTo(1));
            CacheOwnershipReceipt normalInstallReceipt;
            Assert.That(receipts.TryRead(cache, "game", out normalInstallReceipt), Is.False,
                "A ZIP extraction receipt must not impersonate a normal install receipt.");
        }

        [Test]
        public void KeepOrCancelChoiceNeverWritesCache()
        {
            var plan = Plan();
            var result = new ZipManagedExtractionService().ExecuteApproved(
                plan, false, receipts, CancellationToken.None);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.GetFileSystemEntries(cache), Is.Empty);
            Assert.That(Directory.Exists(plan.Destination), Is.False);
        }

        [Test]
        public void DestinationAlreadyOwnedByUserIsNeverOverwritten()
        {
            var plan = Plan();
            Directory.CreateDirectory(plan.Destination);
            var userSave = Path.Combine(plan.Destination, "user.sav");
            File.WriteAllText(userSave, "valuable");
            var result = new ZipManagedExtractionService().ExecuteApproved(
                plan, true, receipts, CancellationToken.None);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(File.ReadAllText(userSave), Is.EqualTo("valuable"));
        }

        [Test]
        public void RejectChangedSourceAfterUserReview()
        {
            var plan = Plan();
            var bytes = File.ReadAllBytes(archive);
            bytes[bytes.Length - 1] ^= 1;
            File.WriteAllBytes(archive, bytes);
            var result = new ZipManagedExtractionService().ExecuteApproved(
                plan, true, receipts, CancellationToken.None);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(plan.Destination), Is.False);
        }

        [Test]
        public void CancelledBeforeExtractionLeavesSourceAndCacheUntouched()
        {
            var plan = Plan();
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                var result = new ZipManagedExtractionService().ExecuteApproved(
                    plan, true, receipts, cancel.Token);
                Assert.That(result.Cancelled, Is.True);
            }
            Assert.That(Directory.Exists(plan.Destination), Is.False);
            Assert.That(File.Exists(archive), Is.True);
        }

        [Test]
        public void UnexpectedStagingContentPropagatesManualAttentionWithoutReceipt()
        {
            var plan = Plan();
            string foreign = null;
            var result = new ZipManagedExtractionService().ExecuteApproved(
                plan, true, receipts, CancellationToken.None, (copied, total) =>
                {
                    if (foreign != null) return;
                    var stage = Directory.GetDirectories(cache, "*.pcls-partial-*")[0];
                    foreign = Path.Combine(stage, "user.sav");
                    File.WriteAllText(foreign, "preserve");
                });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.NeedsManualAttention, Is.True);
            Assert.That(result.ReceiptRecorded, Is.False);
            Assert.That(File.ReadAllText(foreign), Is.EqualTo("preserve"));
            Assert.That(Directory.Exists(plan.Destination), Is.False);
            CacheOwnershipReceipt ownership;
            Assert.That(receipts.TryRead(cache, plan.ReceiptKey, out ownership), Is.False);
        }

        [Test]
        public void InventoryFailurePreservesSuccessfullyExtractedUserContent()
        {
            var service = new ZipManagedExtractionService();
            var plan = Plan();
            var unavailable = Path.Combine(root, "receipts");
            var result = service.ExecuteApproved(plan, true,
                receipts, CancellationToken.None, (copied, total) =>
                {
                    if (!File.Exists(unavailable)) File.WriteAllText(unavailable, "preserve unexpected store");
                });
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ReceiptRecorded, Is.False);
            Assert.That(result.NeedsManualAttention, Is.True);
            Assert.That(File.ReadAllText(Path.Combine(plan.Destination, "game.exe")),
                Is.EqualTo("verified fixture bytes"));
        }

        [Test]
        public void SourceInsidePlannedDestinationIsRejected()
        {
            var plan = Plan();
            Directory.CreateDirectory(plan.Destination);
            var nested = Path.Combine(plan.Destination, "nested.zip");
            File.Copy(archive, nested);
            var review = new ZipPackageReviewService().Inspect(nested, "game");
            Assert.That(new ZipManagedExtractionService().Prepare(
                review, nested, cache, "game", receipts), Is.Null);
        }

        [Test]
        public void DifferentGamesReceiveSeparateDeterministicDestinations()
        {
            var first = Plan("game");
            var second = Plan("game-two");
            Assert.That(first.Destination, Is.Not.EqualTo(second.Destination));
            Assert.That(first.ReceiptKey, Is.Not.EqualTo(second.ReceiptKey));
            Assert.That(Path.GetDirectoryName(first.Destination), Is.EqualTo(cache));
            Assert.That(Path.GetDirectoryName(second.Destination), Is.EqualTo(cache));
        }

        [Test]
        public void HostedExtractionReservationBlocksCompetingTransferAndReleasesAfterSuccess()
        {
            var plan = Plan();
            var journal = new TransferRecoveryJournal(Path.Combine(root, "transfer-journal"));
            var hostId = Guid.NewGuid();
            var observed = false;
            var result = new ZipManagedExtractionService().ExecuteApproved(plan, true, receipts,
                CancellationToken.None, (copied, total) =>
                {
                    if (observed) return;
                    var snapshot = journal.Inspect();
                    Assert.That(snapshot.Records, Has.Count.EqualTo(1));
                    Assert.That(snapshot.Records[0].GameId, Is.EqualTo(hostId));
                    Assert.That(snapshot.Records[0].StagingPath,
                        Is.EqualTo(Directory.GetDirectories(cache, "*.pcls-partial-*")[0]));
                    var other = new CloudTransferManager(1).Enqueue(Guid.NewGuid(), "fixture",
                        "source", Path.Combine(plan.Destination, "overlap.bin"), "LocalFolder");
                    Assert.Throws<TransferRetryRefusedException>(() =>
                        new TransferRecoveryJournal(Path.Combine(root, "transfer-journal")).Begin(other));
                    observed = true;
                }, null, null, journal, hostId);
            Assert.That(observed, Is.True);
            Assert.That(result.Succeeded && result.ReceiptRecorded, Is.True, result.Reason);
            Assert.That(result.NeedsManualAttention, Is.False, result.Reason);
            Assert.That(journal.Inspect().Records, Is.Empty);
        }

        [Test]
        public void HostedExtractionRefusesExistingReservationBeforeWritingStaging()
        {
            var plan = Plan();
            var journalPath = Path.Combine(root, "transfer-journal");
            var prior = new CloudTransferManager(1).Enqueue(Guid.NewGuid(), "fixture",
                "source", plan.Destination, "LocalFolder", true);
            new TransferRecoveryJournal(journalPath).Begin(prior);
            var journal = new TransferRecoveryJournal(journalPath);
            var result = new ZipManagedExtractionService().ExecuteApproved(plan, true, receipts,
                CancellationToken.None, null, null, null, journal, Guid.NewGuid());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.GetFileSystemEntries(cache), Is.Empty);
            Assert.That(journal.Inspect().Records, Has.Count.EqualTo(1));
            Assert.That(journal.Inspect().Records[0].JobId, Is.EqualTo(prior.Id));
        }

        [Test]
        public void HostedExtractionPreservesReservationWhenStagingContainsForeignContent()
        {
            var plan = Plan();
            var journal = new TransferRecoveryJournal(Path.Combine(root, "transfer-journal"));
            string foreign = null;
            var result = new ZipManagedExtractionService().ExecuteApproved(plan, true, receipts,
                CancellationToken.None, (copied, total) =>
                {
                    if (foreign != null) return;
                    foreign = Path.Combine(Directory.GetDirectories(cache, "*.pcls-partial-*")[0], "user.sav");
                    File.WriteAllText(foreign, "preserve");
                }, null, null, journal, Guid.NewGuid());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.NeedsManualAttention, Is.True);
            Assert.That(File.ReadAllText(foreign), Is.EqualTo("preserve"));
            Assert.That(new TransferRecoveryJournal(Path.Combine(root, "transfer-journal")).Inspect().Records,
                Has.Count.EqualTo(1));
        }

        [Test]
        public void CorruptReceiptFootprintBlocksPreparedAndNewExtraction()
        {
            var service = new ZipManagedExtractionService();
            var plan = Plan();
            Directory.CreateDirectory(plan.Destination);
            receipts.RecordCompleted(cache, plan.ReceiptKey, plan.Destination);
            Directory.Delete(plan.Destination);
            var record = Directory.GetFiles(Path.Combine(root, "receipts"), "*.json")[0];
            File.WriteAllText(record, "{corrupt receipt");
            var review = new ZipPackageReviewService().Inspect(archive, "game");
            Assert.That(service.Prepare(review, archive, cache, "game", receipts), Is.Null);
            Assert.That(service.ExecuteApproved(plan, true, receipts, CancellationToken.None).Succeeded, Is.False);
            Assert.That(Directory.Exists(plan.Destination), Is.False);
            Assert.That(File.ReadAllText(record), Is.EqualTo("{corrupt receipt"));
        }

        [Test]
        public void UnavailableReceiptStoreRefusesPreparation()
        {
            var review = new ZipPackageReviewService().Inspect(archive, "game");
            var invalid = new CacheOwnershipReceiptStore(Path.Combine(cache, "receipts"));
            Assert.That(new ZipManagedExtractionService().Prepare(review, archive, cache, "game", invalid), Is.Null);
            Assert.That(Directory.GetFileSystemEntries(cache), Is.Empty);
        }

        [Test]
        public void ExistingReceiptBlocksRepeatExtractionEvenAfterOutputMoved()
        {
            var service = new ZipManagedExtractionService();
            var plan = Plan();
            Assert.That(service.ExecuteApproved(plan, true,
                receipts, CancellationToken.None).Succeeded, Is.True);
            var moved = Path.Combine(root, "moved");
            Directory.Move(plan.Destination, moved);
            var review = new ZipPackageReviewService().Inspect(archive, "game");
            Assert.That(service.Prepare(review, archive, cache, "game", receipts), Is.Null);
            Assert.That(File.Exists(Path.Combine(moved, "game.exe")), Is.True);
        }

        [Test]
        public void MissingSourceOrUnsafeCacheFailsClosed()
        {
            var review = new ZipPackageReviewService().Inspect(archive, "game");
            var service = new ZipManagedExtractionService();
            Assert.That(service.Prepare(review, archive, Path.Combine(root, "missing"),
                "game", receipts), Is.Null);
            Assert.That(service.Prepare(review, archive, cache, "../", receipts), Is.Null);
            Assert.That(service.Prepare(review, Path.Combine(root, "other.zip"),
                cache, "game", receipts), Is.Null);
        }
    }
}
