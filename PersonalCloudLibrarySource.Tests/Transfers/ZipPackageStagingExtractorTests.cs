using NUnit.Framework;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public sealed class ZipPackageStagingExtractorTests
    {
        private string root;
        private string cache;
        private string source;
        private string destination;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ZipStagingTests",
                Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            source = Path.Combine(root, "package.zip");
            destination = Path.Combine(cache, "game");
            Directory.CreateDirectory(cache);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private void WriteArchive(Action<ZipArchive> build)
        {
            using (var file = new FileStream(source, FileMode.CreateNew, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                build(zip);
        }

        private static void AddFile(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        [Test]
        public void ExtractsIntoPrivateStageAndAtomicallyPublishesOnlyAfterVerifiedBytes()
        {
            WriteArchive(zip =>
            {
                zip.CreateEntry("folder/");
                AddFile(zip, "folder/play.exe", "sample executable bytes");
                AddFile(zip, "folder/readme.txt", "example instructions");
            });
            var job = Guid.NewGuid();
            long reported = 0;
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None,
                (bytes, total) =>
                {
                    reported = bytes;
                    Assert.That(total.HasValue, Is.True);
                    Assert.That(Directory.Exists(destination), Is.False,
                        "Do not publish unfinished extraction during progress.");
                });

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.BytesTransferred, Is.EqualTo(reported));
            Assert.That(File.ReadAllText(Path.Combine(destination, "folder", "play.exe")),
                Is.EqualTo("sample executable bytes"));
            Assert.That(File.ReadAllText(Path.Combine(destination, "folder", "readme.txt")),
                Is.EqualTo("example instructions"));
            Assert.That(Directory.Exists(TransferPartialPathPolicy.Create(destination, job)),
                Is.False);
            Assert.That(File.Exists(source), Is.True);
        }

        [Test]
        public void ExistingDestinationRemainsUntouchedWithNoStageCreation()
        {
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "user-save.sav"), "precious");
            WriteArchive(zip => AddFile(zip, "game.exe", "overwrite attempt"));
            var job = Guid.NewGuid();
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(destination, "user-save.sav")),
                Is.EqualTo("precious"));
            Assert.That(Directory.Exists(TransferPartialPathPolicy.Create(destination, job)),
                Is.False);
        }

        [TestCase("../escape.exe")]
        [TestCase("C:/Windows/system.exe")]
        [TestCase("game/CON.txt")]
        public void MaliciousArchivePathCannotEscapeOrLeaveStaging(string unsafeName)
        {
            WriteArchive(zip => AddFile(zip, unsafeName, "wrong location"));
            var job = Guid.NewGuid();
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(destination), Is.False);
            Assert.That(Directory.Exists(TransferPartialPathPolicy.Create(destination, job)),
                Is.False);
            Assert.That(File.Exists(Path.Combine(root, "escape.exe")), Is.False);
        }

        [Test]
        public void FileAsParentConflictIsRejectedBeforeWriting()
        {
            WriteArchive(zip =>
            {
                AddFile(zip, "folder", "a regular file");
                AddFile(zip, "folder/game.exe", "would need folder to be directory");
            });
            var job = Guid.NewGuid();
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(TransferPartialPathPolicy.Create(destination, job)),
                Is.False);
        }

        [Test]
        public void CaseInsensitiveCollisionIsRejectedBeforeWriting()
        {
            WriteArchive(zip =>
            {
                AddFile(zip, "Data/game.exe", "one");
                AddFile(zip, "data/GAME.EXE", "two");
            });
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, Guid.NewGuid(), CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [Test]
        public void UnixSymlinkHeaderNeverCreatesFilesystemLink()
        {
            WriteArchive(zip => AddFile(zip, "external-link", "C:/Private"));
            // .NET 4.6.2 cannot set ZipArchiveEntry.ExternalAttributes.
            // Rewrite only the ZIP central record's 4 external attribute bytes.
            var bytes = File.ReadAllBytes(source);
            var patched = false;
            for (var i = 0; i <= bytes.Length - 46; i++)
            {
                if (bytes[i] != 0x50 || bytes[i + 1] != 0x4b ||
                    bytes[i + 2] != 0x01 || bytes[i + 3] != 0x02)
                    continue;
                bytes[i + 38] = 0x00;
                bytes[i + 39] = 0x00;
                bytes[i + 40] = 0xff;
                bytes[i + 41] = 0xa1; // Unix symlink 0120777
                patched = true;
                break;
            }
            Assert.That(patched, Is.True);
            File.WriteAllBytes(source, bytes);
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, Guid.NewGuid(), CancellationToken.None);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [Test]
        public void CancellingDuringStreamingRemovesOnlyOwnedPartial()
        {
            var random = new byte[2 * 1024 * 1024];
            new Random(151).NextBytes(random);
            WriteArchive(zip =>
            {
                var entry = zip.CreateEntry("payload.bin", CompressionLevel.Fastest);
                using (var stream = entry.Open())
                    stream.Write(random, 0, random.Length);
            });
            var sibling = Path.Combine(cache, "unrelated-save.sav");
            File.WriteAllText(sibling, "preserve");
            var job = Guid.NewGuid();
            using (var cancellation = new CancellationTokenSource())
            {
                var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                    source, cache, destination, job, cancellation.Token,
                    (bytes, total) =>
                    {
                        if (bytes > 0)
                            cancellation.Cancel();
                    });

                Assert.That(result.Cancelled, Is.True, result.Message);
            }
            Assert.That(Directory.Exists(destination), Is.False);
            Assert.That(Directory.Exists(TransferPartialPathPolicy.Create(destination, job)),
                Is.False);
            Assert.That(File.ReadAllText(sibling), Is.EqualTo("preserve"));
            Assert.That(File.Exists(source), Is.True);
        }

        [Test]
        public void PreexistingPartialStageIsNeverAdoptedOrDeleted()
        {
            WriteArchive(zip => AddFile(zip, "game.exe", "sample"));
            var job = Guid.NewGuid();
            var stage = TransferPartialPathPolicy.Create(destination, job);
            Directory.CreateDirectory(stage);
            var existing = Path.Combine(stage, "important.txt");
            File.WriteAllText(existing, "not owned by extractor");

            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(File.ReadAllText(existing), Is.EqualTo("not owned by extractor"));
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [Test]
        public void CacheRootAndOutsideCacheAreNeverExtractionDestinations()
        {
            WriteArchive(zip => AddFile(zip, "game.exe", "sample"));
            var extractor = new ZipPackageStagingExtractor();
            var intoRoot = extractor.ExtractApprovedZip(source, cache, cache,
                Guid.NewGuid(), CancellationToken.None);
            var outside = Path.Combine(root, "other-game");
            var external = extractor.ExtractApprovedZip(source, cache, outside,
                Guid.NewGuid(), CancellationToken.None);

            Assert.That(intoRoot.Succeeded, Is.False);
            Assert.That(external.Succeeded, Is.False);
            Assert.That(Directory.Exists(outside), Is.False);
            Assert.That(File.Exists(source), Is.True);
        }
    }
}
