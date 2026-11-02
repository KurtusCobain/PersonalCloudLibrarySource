using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public sealed class ZipPackageReviewServiceTests
    {
        private string root;
        private string source;
        private string cache;
        private string destination;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ZipReview",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            source = Path.Combine(root, "example.zip");
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            destination = Path.Combine(cache, "Game");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private void WriteZip(params string[] names)
        {
            using (var file = new FileStream(source, FileMode.CreateNew, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                foreach (var name in names)
                {
                    var entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
                    if (name.EndsWith("/", StringComparison.Ordinal))
                        continue;
                    using (var stream = entry.Open())
                    {
                        var content = Encoding.UTF8.GetBytes("fixture game " + name);
                        stream.Write(content, 0, content.Length);
                    }
                }
        }

        [Test]
        public void SingleRomZipIsKeepCandidateButNeverAutoExtracted()
        {
            WriteZip("game.gba");
            var review = new ZipPackageReviewService().Inspect(source, "My Game");
            Assert.That(review.PassedPreflight, Is.True, review.Reason);
            Assert.That(review.Kind, Is.EqualTo(ZipPackageReviewKind.KeepArchiveCandidate));
            Assert.That(review.MemberPaths, Is.EquivalentTo(new[] { "game.gba" }));
            Assert.That(review.FileCount, Is.EqualTo(1));
            Assert.That(review.RequiresUserChoice, Is.True);
            Assert.That(review.CanExtractUnattended, Is.False);
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [Test]
        public void GameExeZipSuggestsCandidateWithoutRunningInstaller()
        {
            WriteZip("Game.exe", "setup.exe", "readme.txt");
            var review = new ZipPackageReviewService().Inspect(source, "Game");
            Assert.That(review.PassedPreflight, Is.True, review.Reason);
            Assert.That(review.Kind, Is.EqualTo(ZipPackageReviewKind.ExtractCandidate));
            Assert.That(review.Launchers.SuggestedRelativePath, Is.EqualTo("Game.exe"));
            Assert.That(review.Launchers.RequiresUserConfirmation, Is.True);
            Assert.That(review.Launchers.Candidates.Single(x =>
                x.RelativePath == "setup.exe").Kind,
                Is.EqualTo(LauncherCandidateKind.InstallerOrHelper));
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [Test]
        public void NoConsentMeansNoStagingAndNoWrites()
        {
            WriteZip("game.exe");
            var service = new ZipPackageReviewService();
            var review = service.Inspect(source, "Game");
            Assert.That(review.PassedPreflight, Is.True, review.Reason);
            var jobId = Guid.NewGuid();
            var result = service.ExtractAfterExplicitChoice(review, false,
                source, cache, destination, jobId, CancellationToken.None);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(destination), Is.False);
            Assert.That(Directory.Exists(TransferPartialPathPolicy.Create(destination, jobId)),
                Is.False);
            Assert.That(File.Exists(source), Is.True);
        }

        [Test]
        public void ExplicitChoiceCanStageVerifiedZipWithoutExecutingIt()
        {
            WriteZip("game.exe", "readme.txt");
            var service = new ZipPackageReviewService();
            var review = service.Inspect(source, "Game");
            Assert.That(review.PassedPreflight, Is.True, review.Reason);
            var result = service.ExtractAfterExplicitChoice(review, true,
                source, cache, destination, Guid.NewGuid(), CancellationToken.None);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(File.ReadAllText(Path.Combine(destination, "game.exe")),
                Is.EqualTo("fixture game game.exe"));
            Assert.That(File.Exists(source), Is.True);
        }

        [Test]
        public void SourceContentChangedSinceReviewCannotBePublished()
        {
            WriteZip("game.exe");
            var service = new ZipPackageReviewService();
            var review = service.Inspect(source, "Game");
            Assert.That(review.PassedPreflight, Is.True);
            var bytes = File.ReadAllBytes(source);
            bytes[bytes.Length - 1] ^= 0x01;
            File.WriteAllBytes(source, bytes);
            var result = service.ExtractAfterExplicitChoice(review, true,
                source, cache, destination, Guid.NewGuid(), CancellationToken.None);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("changed after review"));
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [Test]
        public void ApprovalCannotBeRedirectedToDifferentArchivePath()
        {
            WriteZip("game.exe");
            var service = new ZipPackageReviewService();
            var review = service.Inspect(source, "Game");
            var another = Path.Combine(root, "other.zip");
            File.Copy(source, another);
            var result = service.ExtractAfterExplicitChoice(review, true,
                another, cache, destination, Guid.NewGuid(), CancellationToken.None);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [Test]
        public void FailedReviewNeverAuthorizesExtraction()
        {
            WriteZip("../escape.exe");
            var service = new ZipPackageReviewService();
            var review = service.Inspect(source, "Game");
            Assert.That(review.PassedPreflight, Is.False);
            var result = service.ExtractAfterExplicitChoice(review, true,
                source, cache, destination, Guid.NewGuid(), CancellationToken.None);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(File.Exists(Path.Combine(root, "escape.exe")), Is.False);
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [Test]
        public void ExistingDestinationRemainsUntouchedDespiteApproval()
        {
            WriteZip("game.exe");
            Directory.CreateDirectory(destination);
            var userSave = Path.Combine(destination, "save.dat");
            File.WriteAllText(userSave, "do not touch");
            var service = new ZipPackageReviewService();
            var result = service.ExtractAfterExplicitChoice(
                service.Inspect(source, "Game"), true,
                source, cache, destination, Guid.NewGuid(), CancellationToken.None);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(File.ReadAllText(userSave), Is.EqualTo("do not touch"));
        }

        [Test]
        public void CancelledApprovalDoesNotCreateAnyStage()
        {
            WriteZip("game.exe");
            var service = new ZipPackageReviewService();
            var review = service.Inspect(source, "Game");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var result = service.ExtractAfterExplicitChoice(review, true,
                    source, cache, destination, Guid.NewGuid(), cancellation.Token);
                Assert.That(result.Cancelled, Is.True);
            }
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [Test]
        public void Unsupported7zAndRarDoNotMasqueradeAsZip()
        {
            WriteZip("game.exe");
            var service = new ZipPackageReviewService();
            foreach (var extension in new[] { ".7z", ".rar" })
            {
                var other = Path.Combine(root, "example" + extension);
                File.Copy(source, other);
                var review = service.Inspect(other, "Game");
                Assert.That(review.PassedPreflight, Is.False);
                var result = service.ExtractAfterExplicitChoice(review, true,
                    other, cache, destination, Guid.NewGuid(), CancellationToken.None);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(Directory.Exists(destination), Is.False);
            }
        }

        [Test]
        public void DirectoryOnlyArchiveIsRejectedAsGamePayload()
        {
            WriteZip("empty/");
            var review = new ZipPackageReviewService().Inspect(source, "Empty");
            Assert.That(review.PassedPreflight, Is.False);
            Assert.That(review.Reason, Does.Contain("no regular game payload"));
        }
    }
}
