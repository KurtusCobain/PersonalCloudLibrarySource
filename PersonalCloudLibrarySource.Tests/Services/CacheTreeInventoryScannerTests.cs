using NUnit.Framework;
using System;
using System.IO;
using System.Threading;

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
        public void CancelledInventoryNeverProducesFingerprintOrDeletesFiles()
        {
            var file = Path.Combine(game, "keep.bin");
            File.WriteAllText(file, "keep");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() =>
                    new CacheTreeInventoryScanner().Scan(cache, game, cancellation.Token));
            }
            Assert.That(File.Exists(file), Is.True);
            Assert.That(File.ReadAllText(file), Is.EqualTo("keep"));
        }

        [Test]
        public void StreamingHashIsStableAcrossFilesLargerThanReadBuffer()
        {
            var file = Path.Combine(game, "large.bin");
            File.WriteAllBytes(file, new byte[2 * 1024 * 1024 + 17]);
            var scanner = new CacheTreeInventoryScanner();
            var original = scanner.Scan(cache, game).Fingerprint;
            Assert.That(scanner.Scan(cache, game).Fingerprint, Is.EqualTo(original));
            using (var stream = new FileStream(file, FileMode.Open, FileAccess.Write))
            {
                stream.Position = 1024 * 1024 + 2;
                stream.WriteByte(123);
            }
            Assert.That(scanner.Scan(cache, game).Fingerprint, Is.Not.EqualTo(original));
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
