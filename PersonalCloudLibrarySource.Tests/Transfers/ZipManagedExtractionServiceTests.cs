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
        public void InventoryFailurePreservesSuccessfullyExtractedUserContent()
        {
            var review = new ZipPackageReviewService().Inspect(archive, "game");
            var invalidReceiptStore = new CacheOwnershipReceiptStore(
                Path.Combine(cache, "receipts"));
            var service = new ZipManagedExtractionService();
            var plan = service.Prepare(review, archive, cache, "game", invalidReceiptStore);
            Assert.That(plan, Is.Not.Null);
            var result = service.ExecuteApproved(plan, true,
                invalidReceiptStore, CancellationToken.None);
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
