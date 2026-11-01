using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class CacheDirectoryInventoryDiffServiceTests
    {
        private string root;
        private string cache;
        private string game;
        private CacheTreeInventoryScanner scanner;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(),
                "PCLS-ReadOnlyInventoryDiff", Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            game = Path.Combine(cache, "test-game");
            Directory.CreateDirectory(game);
            scanner = new CacheTreeInventoryScanner();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [Test]
        public void MatchingObservations_NeverGrantDeletionAuthority()
        {
            var file = Path.Combine(game, "game.exe");
            File.WriteAllText(file, "original");
            var before = scanner.ScanDetailed(cache, game, CancellationToken.None);
            var after = scanner.ScanDetailed(cache, game, CancellationToken.None);

            var result = new CacheDirectoryInventoryDiffService().Compare(before, after);

            Assert.That(result.HasChanges, Is.False);
            Assert.That(result.Entries, Has.Count.EqualTo(1));
            Assert.That(result.Entries[0].Change, Is.EqualTo(
                CacheDirectoryObservedChange.Unchanged));
            Assert.That(result.CanDeleteAutomatically, Is.False);
            Assert.That(File.ReadAllText(file), Is.EqualTo("original"));
        }

        [Test]
        public void AddedSaveAndModifiedPayload_AreReportedSeparatelyWithoutDeletion()
        {
            var file = Path.Combine(game, "game.exe");
            File.WriteAllText(file, "original");
            var before = scanner.ScanDetailed(cache, game, CancellationToken.None);
            var save = Path.Combine(game, "personal.sav");
            File.WriteAllText(save, "my important save");
            File.WriteAllText(file, "updated");

            var after = scanner.ScanDetailed(cache, game, CancellationToken.None);
            var diff = new CacheDirectoryInventoryDiffService().Compare(before, after);

            Assert.That(diff.HasChanges, Is.True);
            Assert.That(diff.Entries.Single(x => x.RelativePath == "personal.sav").Change,
                Is.EqualTo(CacheDirectoryObservedChange.Added));
            Assert.That(diff.Entries.Single(x => x.RelativePath == "game.exe").Change,
                Is.EqualTo(CacheDirectoryObservedChange.Modified));
            Assert.That(diff.CanDeleteAutomatically, Is.False);
            Assert.That(File.ReadAllText(save), Is.EqualTo("my important save"));
            Assert.That(File.ReadAllText(file), Is.EqualTo("updated"));
        }

        [Test]
        public void RemovedDisposableFile_IsReportedButOtherEntriesStayIntact()
        {
            var removed = Path.Combine(game, "old.bin");
            var remaining = Path.Combine(game, "keep.bin");
            File.WriteAllText(removed, "old");
            File.WriteAllText(remaining, "keep");
            var before = scanner.ScanDetailed(cache, game, CancellationToken.None);
            // This is a disposable test fixture, not an implementation deletion.
            File.Delete(removed);
            var after = scanner.ScanDetailed(cache, game, CancellationToken.None);

            var diff = new CacheDirectoryInventoryDiffService().Compare(before, after);

            Assert.That(diff.Entries.Single(x => x.RelativePath == "old.bin").Change,
                Is.EqualTo(CacheDirectoryObservedChange.Missing));
            Assert.That(diff.Entries.Single(x => x.RelativePath == "keep.bin").Change,
                Is.EqualTo(CacheDirectoryObservedChange.Unchanged));
            Assert.That(diff.CanDeleteAutomatically, Is.False);
            Assert.That(File.ReadAllText(remaining), Is.EqualTo("keep"));
        }

        [Test]
        public void SummaryOnlyV2Snapshot_CannotMasqueradeAsDetailedPerFileEvidence()
        {
            File.WriteAllText(Path.Combine(game, "game.exe"), "original");
            var historical = scanner.Scan(cache, game);
            var detailed = scanner.ScanDetailed(cache, game, CancellationToken.None);

            Assert.Throws<InvalidOperationException>(() =>
                new CacheDirectoryInventoryDiffService().Compare(historical, detailed));
            Assert.That(File.Exists(Path.Combine(game, "game.exe")), Is.True);
        }

        [Test]
        public void EmptyDetailedSnapshots_StayNonAuthorizing()
        {
            var empty = scanner.ScanDetailed(cache, game, CancellationToken.None);
            var diff = new CacheDirectoryInventoryDiffService().Compare(empty, empty);

            Assert.That(diff.Entries, Is.Empty);
            Assert.That(diff.HasChanges, Is.False);
            Assert.That(diff.CanDeleteAutomatically, Is.False);
            Assert.That(Directory.Exists(game), Is.True);
        }
    }
}
