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
    public sealed class ZipExtractedLauncherReviewServiceTests
    {
        private string root;
        private string cache;
        private string archive;
        private CacheOwnershipReceiptStore receipts;
        private readonly ZipManagedExtractionService extractor = new ZipManagedExtractionService();
        private readonly ZipExtractedLauncherReviewService reviewer = new ZipExtractedLauncherReviewService();

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ZipLauncherReview",
                Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            archive = Path.Combine(root, "sample.zip");
            using (var file = new FileStream(archive, FileMode.CreateNew, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                Add(zip, "Game.exe", "game payload");
                Add(zip, "setup.exe", "installer payload");
                Add(zip, "readme.txt", "readme");
            }
            receipts = new CacheOwnershipReceiptStore(Path.Combine(root, "receipts"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private static void Add(ZipArchive zip, string name, string value)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private ZipManagedExtractionPlan Extract()
        {
            var preview = new ZipPackageReviewService().Inspect(archive, "Game");
            Assert.That(preview.PassedPreflight, Is.True, preview.Reason);
            var plan = extractor.Prepare(preview, archive, cache, "game", receipts);
            Assert.That(plan, Is.Not.Null);
            var result = extractor.ExecuteApproved(plan, true,
                receipts, CancellationToken.None);
            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.ReceiptRecorded, Is.True, result.Reason);
            return plan;
        }

        [Test]
        public void ValidReceiptAndExactInventoryPermitOnlyReadOnlySuggestions()
        {
            var plan = Extract();
            Assert.That(reviewer.HasRecordedExtraction(cache, "game", receipts), Is.True);
            var review = reviewer.Inspect(cache, "game", "Game", receipts);
            Assert.That(review.InventoryVerified, Is.True, review.Reason);
            Assert.That(review.ExtractedDirectory, Is.EqualTo(plan.Destination));
            Assert.That(review.Launchers.SuggestedRelativePath, Is.EqualTo("Game.exe"));
            Assert.That(review.Launchers.Candidates.Count, Is.EqualTo(2));
            Assert.That(review.Launchers.Candidates.Single(x =>
                x.RelativePath == "setup.exe").Kind,
                Is.EqualTo(LauncherCandidateKind.InstallerOrHelper));
            Assert.That(review.Launchers.RequiresUserConfirmation, Is.True);
            Assert.That(review.CanLaunchWithoutUserConfirmation, Is.False);
            Assert.That(review.CanAuthorizeRemoval, Is.False);
            Assert.That(File.Exists(Path.Combine(plan.Destination, "Game.exe")), Is.True);
        }

        [Test]
        public void NoReceiptDoesNotAdoptArbitraryFolder()
        {
            var key = ZipManagedExtractionService.ReceiptKeyForGame("game");
            Directory.CreateDirectory(Path.Combine(cache, key));
            File.WriteAllText(Path.Combine(cache, key, "Game.exe"), "user content");
            var reviewed = reviewer.Inspect(cache, "game", "Game", receipts);
            Assert.That(reviewed.InventoryVerified, Is.False);
            Assert.That(reviewer.HasRecordedExtraction(cache, "game", receipts), Is.False);
        }

        [Test]
        public void ChangedExeBytesQuarantineLauncherPreview()
        {
            var plan = Extract();
            File.WriteAllText(Path.Combine(plan.Destination, "Game.exe"), "tampered bytes");
            Assert.That(reviewer.Inspect(cache, "game", "Game", receipts)
                .InventoryVerified, Is.False);
        }

        [Test]
        public void AddedSaveOrModFileQuarantinesWithoutDeletion()
        {
            var plan = Extract();
            var added = Path.Combine(plan.Destination, "new-save.sav");
            File.WriteAllText(added, "valuable user data");
            Assert.That(reviewer.Inspect(cache, "game", "Game", receipts)
                .InventoryVerified, Is.False);
            Assert.That(File.ReadAllText(added), Is.EqualTo("valuable user data"));
        }

        [Test]
        public void MissingOriginalExecutableQuarantinesPreview()
        {
            var plan = Extract();
            File.Delete(Path.Combine(plan.Destination, "Game.exe"));
            Assert.That(reviewer.Inspect(cache, "game", "Game", receipts)
                .InventoryVerified, Is.False);
        }

        [Test]
        public void AnotherGameIdentityCannotBorrowReceipt()
        {
            Extract();
            Assert.That(reviewer.Inspect(cache, "different", "Game", receipts)
                .InventoryVerified, Is.False);
            Assert.That(reviewer.HasRecordedExtraction(cache, "different", receipts),
                Is.False);
        }

        [Test]
        public void InvalidGameIdsAndMissingCacheRefuse()
        {
            Extract();
            foreach (var id in new[] { "", ".", "..", "../game", "game/child" })
                Assert.That(reviewer.Inspect(cache, id, "Game", receipts)
                    .InventoryVerified, Is.False, id);
            Assert.That(reviewer.Inspect(Path.Combine(root, "missing"),
                "game", "Game", receipts).InventoryVerified, Is.False);
        }

        [Test]
        public void MovedExtractedFolderIsNotAdoptedAtNewLocation()
        {
            var plan = Extract();
            var moved = Path.Combine(root, "moved");
            Directory.Move(plan.Destination, moved);
            Assert.That(reviewer.Inspect(cache, "game", "Game", receipts)
                .InventoryVerified, Is.False);
            Assert.That(reviewer.HasRecordedExtraction(cache, "game", receipts), Is.False);
            Assert.That(File.ReadAllText(Path.Combine(moved, "Game.exe")),
                Is.EqualTo("game payload"));
        }

        [Test]
        public void CancellationNeverCreatesLauncherOrModifiesFiles()
        {
            var plan = Extract();
            using (var stop = new CancellationTokenSource())
            {
                stop.Cancel();
                var review = reviewer.Inspect(cache, "game", "Game",
                    receipts, stop.Token);
                Assert.That(review.InventoryVerified, Is.False);
                Assert.That(review.Reason, Does.Contain("cancelled"));
            }
            Assert.That(File.ReadAllText(Path.Combine(plan.Destination, "Game.exe")),
                Is.EqualTo("game payload"));
        }

        [Test]
        public void ReceiptWithWrongFileInventoryDoesNotAuthorizePreview()
        {
            var plan = Extract();
            CacheOwnershipReceipt record;
            Assert.That(receipts.TryRead(cache, plan.ReceiptKey, out record), Is.True);
            var other = new CacheOwnershipReceiptStore(Path.Combine(root, "other-receipts"));
            var review = reviewer.Inspect(cache, "game", "Game", other);
            Assert.That(review.InventoryVerified, Is.False);
            Assert.That(review.Launchers.Candidates, Is.Empty);
        }
    }
}
