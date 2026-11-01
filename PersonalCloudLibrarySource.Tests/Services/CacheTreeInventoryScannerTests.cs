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
        public void DetailedScan_ReportsOnlyRelativeFilesAndPreservesV2Fingerprint()
        {
            var folder = Path.Combine(game, "sub");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(game, "tiny.exe"), "tiny");
            File.WriteAllText(Path.Combine(folder, "save.dat"), "save");
            var scanner = new CacheTreeInventoryScanner();
            var original = scanner.Scan(cache, game);
            var detailed = scanner.ScanDetailed(cache, game, CancellationToken.None);

            Assert.That(original.DetailedEntries, Is.Empty,
                "Normal V2 receipt scans must not hold extra per-file details.");
            Assert.That(detailed.Fingerprint, Is.EqualTo(original.Fingerprint));
            Assert.That(detailed.FileCount, Is.EqualTo(original.FileCount));
            Assert.That(detailed.DirectoryCount, Is.EqualTo(original.DirectoryCount));
            Assert.That(detailed.DetailedEntries, Has.Count.EqualTo(3));
            Assert.That(detailed.DetailedEntries[0].RelativePath, Is.EqualTo("sub"));
            Assert.That(detailed.DetailedEntries[0].IsDirectory, Is.True);
            Assert.That(detailed.DetailedEntries[1].RelativePath, Is.EqualTo(
                Path.Combine("sub", "save.dat")));
            Assert.That(detailed.DetailedEntries[1].FileBytes, Is.EqualTo(4));
            Assert.That(detailed.DetailedEntries[1].Sha256, Has.Length.EqualTo(64));
            Assert.That(detailed.DetailedEntries[2].RelativePath, Is.EqualTo("tiny.exe"));
            Assert.That(detailed.DetailedEntries[2].FileBytes, Is.EqualTo(4));
            foreach (var entry in detailed.DetailedEntries)
                Assert.That(entry.RelativePath.StartsWith(root, StringComparison.OrdinalIgnoreCase),
                    Is.False, "Detailed inventory must not report absolute cache paths.");
        }

        [Test]
        public void DetailedScan_UserAddedSaveAppearsAsSeparateEntryWithoutMutation()
        {
            File.WriteAllText(Path.Combine(game, "play.exe"), "original");
            var scanner = new CacheTreeInventoryScanner();
            var before = scanner.ScanDetailed(cache, game, CancellationToken.None);
            var save = Path.Combine(game, "personal.sav");
            File.WriteAllText(save, "personal changes");

            var after = scanner.ScanDetailed(cache, game, CancellationToken.None);

            Assert.That(after.Fingerprint, Is.Not.EqualTo(before.Fingerprint));
            Assert.That(before.DetailedEntries, Has.Count.EqualTo(1));
            Assert.That(after.DetailedEntries, Has.Count.EqualTo(2));
            Assert.That(after.DetailedEntries[1].RelativePath, Is.EqualTo("personal.sav"));
            Assert.That(File.ReadAllText(save), Is.EqualTo("personal changes"));
        }

        [Test]
        public void DetailedScan_CancelledOrOutsideCacheNeverInspectsOrModifiesFiles()
        {
            var original = Path.Combine(game, "keep.txt");
            File.WriteAllText(original, "keep");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() =>
                    new CacheTreeInventoryScanner().ScanDetailed(cache, game, cancellation.Token));
            }
            Assert.Throws<InvalidOperationException>(() =>
                new CacheTreeInventoryScanner().ScanDetailed(
                    cache, root, CancellationToken.None));
            Assert.That(File.ReadAllText(original), Is.EqualTo("keep"));
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
