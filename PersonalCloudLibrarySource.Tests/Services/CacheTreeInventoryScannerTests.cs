using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class CacheTreeInventoryScannerTests
    {
        private string root;
        private string cache;
        private string game;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-Inventory", Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            game = Path.Combine(cache, "game");
            Directory.CreateDirectory(game);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void SnapshotIsStableUntilContentsChange()
        {
            var path = Path.Combine(game, "play.exe");
            File.WriteAllText(path, "original");
            var scanner = new CacheTreeInventoryScanner();
            var before = scanner.Scan(cache, game);
            Assert.That(scanner.Scan(cache, game).Fingerprint, Is.EqualTo(before.Fingerprint));
            File.WriteAllText(path, "modified");
            Assert.That(scanner.Scan(cache, game).Fingerprint, Is.Not.EqualTo(before.Fingerprint));
        }

        [Test]
        public void AddedPersonalFileChangesFingerprint()
        {
            File.WriteAllText(Path.Combine(game, "play.exe"), "game");
            var scanner = new CacheTreeInventoryScanner();
            var before = scanner.Scan(cache, game);
            File.WriteAllText(Path.Combine(game, "personal.txt"), "keep");
            Assert.That(scanner.Scan(cache, game).Fingerprint, Is.Not.EqualTo(before.Fingerprint));
        }

        [Test]
        public void OutsideCacheAndCacheRootAreRefused()
        {
            var scanner = new CacheTreeInventoryScanner();
            Assert.Throws<InvalidOperationException>(() => scanner.Scan(cache, cache));
            Assert.Throws<InvalidOperationException>(() => scanner.Scan(cache, root));
        }

        [Test]
        public void EmptyFolderHasDeterministicFingerprint()
        {
            var scanner = new CacheTreeInventoryScanner();
            var snapshot = scanner.Scan(cache, game);
            Assert.That(snapshot.FileCount, Is.Zero);
            Assert.That(snapshot.Fingerprint, Has.Length.EqualTo(64));
        }
    }
}
