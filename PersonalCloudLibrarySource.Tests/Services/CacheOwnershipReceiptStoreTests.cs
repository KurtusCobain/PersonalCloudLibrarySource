using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class CacheOwnershipReceiptStoreTests
    {
        private string root;
        private string cache;
        private string receipts;
        private string game;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ReceiptTests", Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            receipts = Path.Combine(root, "receipts");
            game = Path.Combine(cache, "game");
            Directory.CreateDirectory(game);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void LegacyCacheHasNoReceiptAndIsNotAdopted()
        {
            var store = new CacheOwnershipReceiptStore(receipts);
            CacheOwnershipReceipt receipt;
            Assert.That(store.TryRead(cache, "game-id", out receipt), Is.False);
            Assert.That(Directory.Exists(receipts), Is.False);
            Assert.That(Directory.Exists(game), Is.True);
        }

        [Test]
        public void CompletedDestinationCreatesReadableReceipt()
        {
            var store = new CacheOwnershipReceiptStore(receipts);
            store.RecordCompleted(cache, "game-id", game);
            CacheOwnershipReceipt receipt;
            Assert.That(store.TryRead(cache, "game-id", out receipt), Is.True);
            Assert.That(receipt.Destination, Is.EqualTo(PathBoundary.Normalize(game)));
            Assert.That(receipt.ItemId, Is.EqualTo("game-id"));
        }

        [Test]
        public void RecordingSameItemAgainRefusesOverwrite()
        {
            var store = new CacheOwnershipReceiptStore(receipts);
            store.RecordCompleted(cache, "game-id", game);
            Assert.Throws<IOException>(() => store.RecordCompleted(cache, "game-id", game));
        }

        [Test]
        public void OutsideCacheDestinationNeverCreatesReceipt()
        {
            var store = new CacheOwnershipReceiptStore(receipts);
            Assert.Throws<InvalidOperationException>(() => store.RecordCompleted(cache, "game-id", receipts));
            Assert.That(Directory.Exists(receipts), Is.False);
        }

        [Test]
        public void ReceiptStoreInsideCache_IsRefused()
        {
            var store = new CacheOwnershipReceiptStore(Path.Combine(cache, "receipts"));
            Assert.Throws<InvalidOperationException>(() => store.RecordCompleted(cache, "game-id", game));
            Assert.That(Directory.Exists(Path.Combine(cache, "receipts")), Is.False);
        }

        [Test]
        public void DifferentCaseItemId_CannotOverwriteReceipt()
        {
            var store = new CacheOwnershipReceiptStore(receipts);
            store.RecordCompleted(cache, "Game-ID", game);
            Assert.Throws<IOException>(() => store.RecordCompleted(cache, "game-id", game));
        }

        [Test]
        public void CorruptReceiptFailsClosed()
        {
            var store = new CacheOwnershipReceiptStore(receipts);
            store.RecordCompleted(cache, "game-id", game);
            File.WriteAllText(Directory.GetFiles(receipts, "*.json")[0], "{broken");
            CacheOwnershipReceipt receipt;
            Assert.That(store.TryRead(cache, "game-id", out receipt), Is.False);
        }
    }
}
