using NUnit.Framework;
using System;
using System.IO;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class CacheDirectoryCleanupReadinessServiceTests
    {
        private string root;
        private string cache;
        private string gameDirectory;
        private string receiptRoot;
        private string gameFile;
        private CacheOwnershipReceiptStore receipts;
        private CacheDirectoryCleanupReadinessService review;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(),
                "PCLS-CleanupReviewTests", Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            gameDirectory = Path.Combine(cache, "game");
            receiptRoot = Path.Combine(root, "receipts");
            gameFile = Path.Combine(gameDirectory, "game.bin");
            Directory.CreateDirectory(gameDirectory);
            File.WriteAllText(gameFile, "original owned test bytes");
            receipts = new CacheOwnershipReceiptStore(receiptRoot);
            review = new CacheDirectoryCleanupReadinessService(receipts);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void MatchingV2Inventory_IsInformationalNeverDeletionAuthority()
        {
            receipts.RecordCompletedWithInventory(cache, "game", gameDirectory);

            var result = review.Inspect(cache, "game", gameDirectory);

            Assert.That(result.Kind, Is.EqualTo(
                CacheDirectoryCleanupReadinessKind.InventoryMatchesButUntrusted));
            Assert.That(result.CanDeleteAutomatically, Is.False);
            Assert.That(File.ReadAllText(gameFile), Is.EqualTo("original owned test bytes"));
        }

        [Test]
        public void UserAddedSave_AfterInstallChangesInventoryAndRemainsOnDisk()
        {
            receipts.RecordCompletedWithInventory(cache, "game", gameDirectory);
            var personalSave = Path.Combine(gameDirectory, "personal.sav");
            File.WriteAllText(personalSave, "user-added-save");

            var result = review.Inspect(cache, "game", gameDirectory);

            Assert.That(result.Kind, Is.EqualTo(CacheDirectoryCleanupReadinessKind.InventoryChanged));
            Assert.That(result.CanDeleteAutomatically, Is.False);
            Assert.That(File.ReadAllText(personalSave), Is.EqualTo("user-added-save"));
            Assert.That(File.ReadAllText(gameFile), Is.EqualTo("original owned test bytes"));
        }

        [Test]
        public void MutatedInstalledFile_IsNotAutomaticallyOwnedForDeletion()
        {
            receipts.RecordCompletedWithInventory(cache, "game", gameDirectory);
            File.WriteAllText(gameFile, "modified after install");

            var result = review.Inspect(cache, "game", gameDirectory);

            Assert.That(result.Kind, Is.EqualTo(CacheDirectoryCleanupReadinessKind.InventoryChanged));
            Assert.That(result.CanDeleteAutomatically, Is.False);
            Assert.That(File.ReadAllText(gameFile), Is.EqualTo("modified after install"));
        }

        [Test]
        public void V1Receipt_IsNotPromotedToDirectoryDeletionAuthority()
        {
            receipts.RecordCompleted(cache, "game", gameDirectory);

            var result = review.Inspect(cache, "game", gameDirectory);

            Assert.That(result.Kind, Is.EqualTo(CacheDirectoryCleanupReadinessKind.LegacyReceipt));
            Assert.That(result.CanDeleteAutomatically, Is.False);
            Assert.That(File.Exists(gameFile), Is.True);
        }

        [Test]
        public void LegacyUnownedCache_IsNotAdopted()
        {
            var result = review.Inspect(cache, "game", gameDirectory);

            Assert.That(result.Kind, Is.EqualTo(CacheDirectoryCleanupReadinessKind.UnverifiedReceipt));
            Assert.That(result.CanDeleteAutomatically, Is.False);
            Assert.That(File.Exists(gameFile), Is.True);
        }

        [Test]
        public void WrongItemDestination_RefusesEvenWhenOtherValidReceiptExists()
        {
            receipts.RecordCompletedWithInventory(cache, "game", gameDirectory);
            var other = Path.Combine(cache, "other");
            Directory.CreateDirectory(other);
            File.WriteAllText(Path.Combine(other, "other.bin"), "other content");

            var result = review.Inspect(cache, "game", other);

            Assert.That(result.Kind, Is.EqualTo(CacheDirectoryCleanupReadinessKind.ReceiptMismatch));
            Assert.That(result.CanDeleteAutomatically, Is.False);
            Assert.That(File.Exists(gameFile), Is.True);
            Assert.That(File.Exists(Path.Combine(other, "other.bin")), Is.True);
        }

        [Test]
        public void CacheRootAndExternalDestination_AreUnsafeAndNotMutated()
        {
            var outside = Path.Combine(root, "personal");
            Directory.CreateDirectory(outside);
            var outsideFile = Path.Combine(outside, "keep.txt");
            File.WriteAllText(outsideFile, "keep");

            var rootResult = review.Inspect(cache, "game", cache);
            var outsideResult = review.Inspect(cache, "game", outside);

            Assert.That(rootResult.Kind, Is.EqualTo(CacheDirectoryCleanupReadinessKind.UnsafeScope));
            Assert.That(outsideResult.Kind, Is.EqualTo(CacheDirectoryCleanupReadinessKind.UnsafeScope));
            Assert.That(rootResult.CanDeleteAutomatically, Is.False);
            Assert.That(outsideResult.CanDeleteAutomatically, Is.False);
            Assert.That(File.ReadAllText(outsideFile), Is.EqualTo("keep"));
            Assert.That(File.Exists(gameFile), Is.True);
        }

        [Test]
        public void MissingGameDirectory_ReportsMissingAndLeavesOtherItems()
        {
            Directory.Delete(gameDirectory, true);
            var sibling = Path.Combine(cache, "other", "keep.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(sibling));
            File.WriteAllText(sibling, "keep");

            var result = review.Inspect(cache, "game", gameDirectory);

            Assert.That(result.Kind, Is.EqualTo(CacheDirectoryCleanupReadinessKind.MissingDirectory));
            Assert.That(result.CanDeleteAutomatically, Is.False);
            Assert.That(File.ReadAllText(sibling), Is.EqualTo("keep"));
        }

        [Test]
        public void CancelledReview_DoesNotPublishOrModifyReceiptOrContent()
        {
            receipts.RecordCompletedWithInventory(cache, "game", gameDirectory);
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var result = review.Inspect(cache, "game", gameDirectory, cancellation.Token);

            Assert.That(result.Kind, Is.EqualTo(CacheDirectoryCleanupReadinessKind.Cancelled));
            Assert.That(result.CanDeleteAutomatically, Is.False);
            Assert.That(File.ReadAllText(gameFile), Is.EqualTo("original owned test bytes"));
            CacheOwnershipReceipt stored;
            Assert.That(receipts.TryRead(cache, "game", out stored), Is.True);
            Assert.That(stored.Version, Is.EqualTo(2));
        }
    }
}
