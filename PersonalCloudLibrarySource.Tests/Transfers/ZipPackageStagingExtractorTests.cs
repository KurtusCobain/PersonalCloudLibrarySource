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
        [TestCase("../../escape.exe")]
        [TestCase("game/../../escape.exe")]
        [TestCase(@"game\..\..\escape.exe")]
        [TestCase(@"..\escape.exe")]
        [TestCase("./escape.exe")]
        [TestCase("game/./play.exe")]
        [TestCase("game//play.exe")]
        [TestCase("game/trailing./play.exe")]
        [TestCase("game/trailing /play.exe")]
        [TestCase("game/play.exe:ads")]
        [TestCase("C:/Windows/system.exe")]
        [TestCase(@"C:\Windows\system.exe")]
        [TestCase("/absolute.exe")]
        [TestCase(@"\server\share\game.exe")]
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
        public void UnsafeMemberAnywhereInZipStopsAllWritesBeforePrivateStage()
        {
            var sentinel = Path.Combine(root, "escape.exe");
            File.WriteAllText(sentinel, "owned by user");
            WriteArchive(zip =>
            {
                AddFile(zip, "safe/good.exe", "trusted fixture");
                AddFile(zip, "safe/../../../escape.exe", "malicious overwrite");
            });
            var job = Guid.NewGuid();
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(File.ReadAllText(sentinel), Is.EqualTo("owned by user"));
            Assert.That(Directory.Exists(destination), Is.False);
            Assert.That(Directory.Exists(
                TransferPartialPathPolicy.Create(destination, job)), Is.False);
            Assert.That(File.Exists(source), Is.True);
        }

        [Test]
        public void SafeWindowsStyleAndUnixStyleMemberPathsRemainContained()
        {
            WriteArchive(zip =>
            {
                AddFile(zip, @"Game\bin\play.exe", "windows separators");
                AddFile(zip, "Game/assets/data.bin", "unix separators");
            });
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, Guid.NewGuid(), CancellationToken.None);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(File.ReadAllText(Path.Combine(destination, "Game", "bin",
                "play.exe")), Is.EqualTo("windows separators"));
            Assert.That(File.ReadAllText(Path.Combine(destination, "Game", "assets",
                "data.bin")), Is.EqualTo("unix separators"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnexpectedContentInjectedIntoPrivateStageIsNeverPublished(bool folder)
        {
            WriteArchive(zip =>
            {
                AddFile(zip, "game.exe", "legitimate game executable");
                AddFile(zip, "data/info.txt", "legitimate game data");
            });
            var job = Guid.NewGuid();
            var stage = TransferPartialPathPolicy.Create(destination, job);
            var unexpected = Path.Combine(stage, folder
                ? "unexpected-folder" : "unexpected-save.sav");
            var injected = false;
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None,
                (bytes, total) =>
                {
                    if (injected) return;
                    injected = true;
                    if (folder)
                        Directory.CreateDirectory(unexpected);
                    else
                        File.WriteAllText(unexpected, "unowned user progress");
                });

            Assert.That(injected, Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(destination), Is.False,
                "A foreign staging member must block publication.");
            Assert.That(Directory.Exists(stage), Is.True,
                "Refuse destructive cleanup of unrelated staging content.");
            if (folder)
                Assert.That(Directory.Exists(unexpected), Is.True);
            else
                Assert.That(File.ReadAllText(unexpected),
                    Is.EqualTo("unowned user progress"));
            Assert.That(File.Exists(source), Is.True);
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
        public void CancellationDoesNotDeleteAJobFileReplacedWithDifferentUserContent()
        {
            const string original = "first original file";
            WriteArchive(zip =>
            {
                AddFile(zip, "first.txt", original);
                AddFile(zip, "second.bin", "second original file");
            });
            var job = Guid.NewGuid();
            var stage = TransferPartialPathPolicy.Create(destination, job);
            var altered = Path.Combine(stage, "first.txt");
            using (var cancellation = new CancellationTokenSource())
            {
                var replaced = false;
                var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                    source, cache, destination, job, cancellation.Token,
                    (copied, total) =>
                    {
                        if (replaced || copied <= Encoding.UTF8.GetByteCount(original))
                            return;
                        replaced = true;
                        File.WriteAllText(altered, "valuable user replacement");
                        cancellation.Cancel();
                    });
                Assert.That(replaced, Is.True,
                    "The earlier entry must be closed before replacement.");
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Cancelled, Is.False,
                    "A modified staged file requires manual inspection.");
                Assert.That(Directory.Exists(destination), Is.False);
                Assert.That(File.ReadAllText(altered),
                    Is.EqualTo("valuable user replacement"));
                Assert.That(Directory.Exists(stage), Is.True,
                    "Rollback must preserve a modified staged file.");
                Assert.That(File.Exists(source), Is.True);
            }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW",
            CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool CreateHardLinkNative(string link, string existing, IntPtr reserved);

        [Test]
        public void StagedFileWithUnexpectedHardlinkIsPreservedAndNotPublished()
        {
            const string original = "linked fixture bytes";
            WriteArchive(zip =>
            {
                AddFile(zip, "first.txt", original);
                AddFile(zip, "second.bin", "second entry");
            });
            var job = Guid.NewGuid();
            var stage = TransferPartialPathPolicy.Create(destination, job);
            var staged = Path.Combine(stage, "first.txt");
            var external = Path.Combine(cache, "external-hardlink.txt");
            var injected = false;
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None,
                (copied, total) =>
                {
                    if (injected || copied <= Encoding.UTF8.GetByteCount(original)) return;
                    injected = CreateHardLinkNative(external, staged, IntPtr.Zero);
                });
            Assert.That(injected, Is.True, "Windows test volume must support hardlinks.");
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RequiresManualRecovery, Is.True);
            Assert.That(Directory.Exists(destination), Is.False);
            Assert.That(File.ReadAllText(staged), Is.EqualTo(original));
            Assert.That(File.ReadAllText(external), Is.EqualTo(original));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReplacedEmptyDirectoryIsNotPublishedOrRemoved(bool cancel)
        {
            WriteArchive(zip =>
            {
                zip.CreateEntry("empty/");
                AddFile(zip, "file.txt", "fixture");
            });
            var job = Guid.NewGuid();
            var stage = TransferPartialPathPolicy.Create(destination, job);
            var empty = Path.Combine(stage, "empty");
            var moved = Path.Combine(cache, "original-empty");
            var injected = false;
            using (var cancellation = new CancellationTokenSource())
            {
                var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                    source, cache, destination, job, cancellation.Token, (copied, total) =>
                    {
                        if (injected) return;
                        injected = true;
                        Directory.Move(empty, moved);
                        Directory.CreateDirectory(empty);
                        if (cancel) cancellation.Cancel();
                    });
                Assert.That(injected, Is.True);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.RequiresManualRecovery, Is.True);
                Assert.That(Directory.Exists(empty), Is.True);
                Assert.That(Directory.Exists(moved), Is.True);
                Assert.That(Directory.Exists(destination), Is.False);
            }
        }

        [Test]
        public void SameByteReplacementIsNotPublishedOrDeletedAsOwned()
        {
            const string original = "same bytes, distinct file identity";
            WriteArchive(zip =>
            {
                AddFile(zip, "first.txt", original);
                AddFile(zip, "second.bin", "second entry");
            });
            var job = Guid.NewGuid();
            var stage = TransferPartialPathPolicy.Create(destination, job);
            var replaced = Path.Combine(stage, "first.txt");
            var moved = Path.Combine(cache, "preserved-original.txt");
            var injected = false;
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None,
                (copied, total) =>
                {
                    if (injected || copied <= Encoding.UTF8.GetByteCount(original)) return;
                    injected = true;
                    File.Move(replaced, moved);
                    File.Copy(moved, replaced);
                });
            Assert.That(injected, Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(destination), Is.False);
            Assert.That(File.ReadAllText(replaced), Is.EqualTo(original));
            Assert.That(File.ReadAllText(moved), Is.EqualTo(original));
        }

        [Test]
        public void PreviouslyWrittenMemberChangedDuringLaterMemberNeverPublishes()
        {
            const string original = "first immutable file";
            WriteArchive(zip =>
            {
                AddFile(zip, "first.txt", original);
                AddFile(zip, "second.bin", "second file for verification boundary");
            });
            var job = Guid.NewGuid();
            var stage = TransferPartialPathPolicy.Create(destination, job);
            var changed = Path.Combine(stage, "first.txt");
            var injected = false;
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None,
                (copied, total) =>
                {
                    if (injected || copied <= Encoding.UTF8.GetByteCount(original))
                        return;
                    injected = true;
                    File.WriteAllText(changed, "new content not from archive");
                });

            Assert.That(injected, Is.True);
            Assert.That(result.Succeeded, Is.False,
                "A post-write modification cannot be published as owned ZIP content.");
            Assert.That(Directory.Exists(destination), Is.False);
            Assert.That(File.ReadAllText(changed),
                Is.EqualTo("new content not from archive"));
            Assert.That(Directory.Exists(stage), Is.True,
                "Preserve the unexpected file for inspection, not rollback deletion.");
        }

        [Test]
        public void CorruptedStoredZipMember_FailsCrcAndLeavesNoPublishedGame()
        {
            WriteArchive(zip =>
            {
                var entry = zip.CreateEntry("payload.txt", CompressionLevel.NoCompression);
                using (var stream = entry.Open())
                {
                    var payload = Encoding.ASCII.GetBytes("correct fixture content");
                    stream.Write(payload, 0, payload.Length);
                }
            });
            var bytes = File.ReadAllBytes(source);
            Assert.That(bytes[0], Is.EqualTo(0x50));
            Assert.That(bytes[1], Is.EqualTo(0x4b));
            Assert.That(bytes[2], Is.EqualTo(0x03));
            Assert.That(bytes[3], Is.EqualTo(0x04));
            var nameLength = bytes[26] | (bytes[27] << 8);
            var extraLength = bytes[28] | (bytes[29] << 8);
            var dataStart = 30 + nameLength + extraLength;
            Assert.That(dataStart + 2, Is.LessThan(bytes.Length));
            bytes[dataStart + 2] ^= 0x01;
            File.WriteAllBytes(source, bytes);

            var job = Guid.NewGuid();
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(destination), Is.False);
            Assert.That(Directory.Exists(TransferPartialPathPolicy.Create(destination, job)),
                Is.False);
            Assert.That(File.Exists(source), Is.True,
                "A corrupt user-supplied ZIP source must never be deleted.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OverlappingOrOutOfBoundsLocalZipMembersRefuseBeforeStage(bool extendsIntoCentral)
        {
            WriteArchive(zip =>
            {
                AddFile(zip, "first.txt", "same sample bytes");
                AddFile(zip, "other.txt", "same sample bytes");
            });
            var bytes = File.ReadAllBytes(source);
            // Fixtures have no EOCD comment. Locate the two central entries
            // through their actual header lengths rather than scanning payload.
            var eocd = bytes.Length - 22;
            Assert.That(BitConverter.ToUInt32(bytes, eocd), Is.EqualTo(0x06054B50u));
            var central = (int)BitConverter.ToUInt32(bytes, eocd + 16);
            Assert.That(BitConverter.ToUInt32(bytes, central), Is.EqualTo(0x02014B50u));
            var second = central + 46 + BitConverter.ToUInt16(bytes, central + 28)
                + BitConverter.ToUInt16(bytes, central + 30)
                + BitConverter.ToUInt16(bytes, central + 32);
            Assert.That(BitConverter.ToUInt32(bytes, second), Is.EqualTo(0x02014B50u));
            if (extendsIntoCentral)
            {
                Buffer.BlockCopy(BitConverter.GetBytes((uint)central), 0, bytes, second + 20, 4);
            }
            else
            {
                // Two distinct central names refer to the same local record.
                Buffer.BlockCopy(bytes, central + 42, bytes, second + 42, 4);
            }
            File.WriteAllBytes(source, bytes);
            var job = Guid.NewGuid();
            var result = new ZipPackageStagingExtractor().ExtractApprovedZip(
                source, cache, destination, job, CancellationToken.None);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(Directory.Exists(destination), Is.False);
            Assert.That(Directory.Exists(TransferPartialPathPolicy.Create(destination, job)), Is.False);
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(bytes));
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
