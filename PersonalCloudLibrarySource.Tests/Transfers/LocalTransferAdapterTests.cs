using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class LocalTransferAdapterTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-LocalTransferTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, true);
            }
        }

        [TestCase(100L, 95L, true)]
        [TestCase(100L, 100L, false)]
        [TestCase(100L, 101L, false)]
        [TestCase(1000L, 990L, false)]
        [TestCase(0L, 0L, true)]
        public void DiskSpacePreflight_UsesSmallReserveWithoutOverflow(long free, long required, bool expected)
        {
            var method = typeof(LocalTransferAdapter).GetMethod(
                "HasRequiredSpace",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That((bool)method.Invoke(null, new object[] { free, required }), Is.EqualTo(expected));
        }

        [Test]
        public void DiskSpacePreflight_QueriesExistingAncestorBeforeCreatingDestination()
        {
            var destination = Path.Combine(testRoot, "not-created", "game.bin");
            var method = typeof(LocalTransferAdapter).GetMethod("CheckDiskSpace",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That((string)method.Invoke(null, new object[] { destination, 0L }), Is.Empty);
            Assert.That((string)method.Invoke(null, new object[] { destination, long.MaxValue }),
                Does.Contain("Not enough free space"));
            Assert.That(Directory.Exists(Path.GetDirectoryName(destination)), Is.False);
        }

        [Test]
        public void CopyDirectory_ManySmallFilesQualifiesDigestAndInventoryPasses()
        {
            const int count = 256;
            var source = Path.Combine(testRoot, "many-source");
            var destination = Path.Combine(testRoot, "many-destination");
            var payload = new byte[16 * 1024];
            long expectedBytes = 0;
            for (var i = 0; i < count; i++)
            {
                var folder = Path.Combine(source, (i / 16).ToString("D2"));
                Directory.CreateDirectory(folder);
                payload[0] = (byte)i;
                var length = i % 4 == 0 ? 0 : payload.Length;
                using (var stream = File.Create(Path.Combine(folder, i.ToString("D3") + ".bin")))
                    stream.Write(payload, 0, length);
                expectedBytes += length;
            }
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var result = new LocalTransferAdapter().CopyDirectory(source, destination,
                Guid.NewGuid(), CancellationToken.None, null);
            clock.Stop();
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.BytesTransferred, Is.EqualTo(expectedBytes));
            var files = Directory.GetFiles(destination, "*.bin", SearchOption.AllDirectories);
            Assert.That(files.Length, Is.EqualTo(count));
            foreach (var file in files)
            {
                var index = int.Parse(Path.GetFileNameWithoutExtension(file));
                var bytes = File.ReadAllBytes(file);
                Assert.That(bytes.Length, Is.EqualTo(index % 4 == 0 ? 0 : payload.Length));
                if (bytes.Length > 0)
                {
                    Assert.That(bytes[0], Is.EqualTo((byte)index));
                    Assert.That(bytes[bytes.Length - 1], Is.Zero);
                }
            }
            TestContext.WriteLine("PCLS_LOCAL_SMALL_TREE files={0} bytes={1} elapsed_ms={2:F3}",
                count, expectedBytes, clock.Elapsed.TotalMilliseconds);
        }

        [Test]
        public void CopyDirectory_LargerMixedTreeQualifiesMembershipAndContent()
        {
            const int count = 1024;
            var source = Path.Combine(testRoot, "mixed-scale-source");
            var destination = Path.Combine(testRoot, "mixed-scale-destination");
            var payload = new byte[4 * 1024 * 1024];
            long expectedBytes = 0;
            for (var i = 0; i < count; i++)
            {
                var folder = Path.Combine(source, (i / 32).ToString("D2"));
                Directory.CreateDirectory(folder);
                payload[0] = (byte)i;
                payload[1] = (byte)(i >> 8);
                var length = i % 64 == 0 ? payload.Length : i % 4 == 0 ? 0 : 8192;
                using (var stream = File.Create(Path.Combine(folder, i.ToString("D4") + ".bin")))
                    stream.Write(payload, 0, length);
                expectedBytes += length;
            }
            Directory.CreateDirectory(Path.Combine(source, "empty"));
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var result = new LocalTransferAdapter().CopyDirectory(source, destination,
                Guid.NewGuid(), CancellationToken.None, null);
            timer.Stop();
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.BytesTransferred, Is.EqualTo(expectedBytes));
            Assert.That(Directory.Exists(Path.Combine(destination, "empty")), Is.True);
            var files = Directory.GetFiles(destination, "*.bin", SearchOption.AllDirectories);
            Assert.That(files.Length, Is.EqualTo(count));
            var buffer = new byte[65536];
            foreach (var file in files)
            {
                var i = int.Parse(Path.GetFileNameWithoutExtension(file));
                var length = i % 64 == 0 ? payload.Length : i % 4 == 0 ? 0 : 8192;
                Assert.That(Path.GetFileName(Path.GetDirectoryName(file)), Is.EqualTo((i / 32).ToString("D2")));
                using (var input = File.OpenRead(file))
                {
                    Assert.That(input.Length, Is.EqualTo(length));
                    long position = 0;
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        // Independently validate all bytes, not just size or
                        // the adapter's own digest. Avoid per-byte NUnit calls.
                        for (var n = 0; n < read; n++, position++)
                        {
                            var expected = position == 0 ? (byte)i : position == 1 ? (byte)(i >> 8) : (byte)0;
                            if (buffer[n] != expected)
                                Assert.Fail("Copied payload mismatch at fixture {0}, offset {1}.", i, position);
                        }
                    }
                    Assert.That(position, Is.EqualTo(length));
                }
            }
            TestContext.WriteLine("PCLS_LOCAL_MIXED_TREE files={0} bytes={1} elapsed_ms={2:F3}",
                count, expectedBytes, timer.Elapsed.TotalMilliseconds);
        }

        [Test]
        public void CopyDirectory_MixedFileSizesAndEmptyDirectoryPreserveContent()
        {
            var source = Path.Combine(testRoot, "source");
            var destination = Path.Combine(testRoot, "cache", "game");
            Directory.CreateDirectory(Path.Combine(source, "empty"));
            var large = new byte[1024 * 1024 + 31];
            large[large.Length - 1] = 123;
            File.WriteAllBytes(Path.Combine(source, "large.bin"), large);
            File.WriteAllText(Path.Combine(source, "small.txt"), "abc");
            File.WriteAllBytes(Path.Combine(source, "zero.bin"), new byte[0]);
            var result = new LocalTransferAdapter().CopyDirectory(source, destination,
                Guid.NewGuid(), CancellationToken.None, null);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.TotalBytes, Is.EqualTo(large.Length + 3L));
            Assert.That(File.ReadAllBytes(Path.Combine(destination, "large.bin")), Is.EqualTo(large));
            Assert.That(File.ReadAllText(Path.Combine(destination, "small.txt")), Is.EqualTo("abc"));
            Assert.That(new FileInfo(Path.Combine(destination, "zero.bin")).Length, Is.Zero);
            Assert.That(Directory.Exists(Path.Combine(destination, "empty")), Is.True);
        }

        [Test]
        public void CopyFile_UsesPartialFileAndMovesVerifiedResultIntoPlace()
        {
            var source = Path.Combine(testRoot, "source.bin");
            var destination = Path.Combine(testRoot, "cache", "game.bin");
            File.WriteAllBytes(source, Encoding.UTF8.GetBytes("owned test content"));
            long lastTransferred = 0;
            long? lastTotal = null;

            var result = new LocalTransferAdapter().CopyFile(
                source,
                destination,
                CancellationToken.None,
                (transferred, total) =>
                {
                    lastTransferred = transferred;
                    lastTotal = total;
                });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Cancelled, Is.False);
            Assert.That(File.Exists(destination), Is.True);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("owned test content"));
            Assert.That(File.Exists(destination + ".pcls-partial"), Is.False);
            Assert.That(lastTransferred, Is.EqualTo(new FileInfo(source).Length));
            Assert.That(lastTotal, Is.EqualTo(new FileInfo(source).Length));
        }

        [TestCase(FileAttributes.Directory, true)]
        [TestCase(FileAttributes.Normal, true)]
        [TestCase(FileAttributes.Directory | FileAttributes.ReparsePoint, false)]
        [TestCase(FileAttributes.ReparsePoint, false)]
        public void CopyDirectory_RejectsJunctionsAndLinkedSourceEntries(
            FileAttributes attributes, bool allowed)
        {
            var method = typeof(LocalTransferAdapter).GetMethod(
                "IsSafeCopyEntry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That((bool)method.Invoke(null, new object[] { attributes }), Is.EqualTo(allowed));
        }

        [Test]
        public void CopyDirectory_CacheInsideSource_RefusesWithoutCreatingPartialOrCopying()
        {
            var source = Path.Combine(testRoot, "game-folder");
            Directory.CreateDirectory(source);
            var original = Path.Combine(source, "play.exe");
            File.WriteAllText(original, "original fixture");
            var destination = Path.Combine(source, "cache", "game-folder");

            var result = new LocalTransferAdapter().CopyDirectory(
                source, destination, CancellationToken.None, null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("overlaps"));
            Assert.That(File.ReadAllText(original), Is.EqualTo("original fixture"));
            Assert.That(Directory.Exists(destination), Is.False);
            Assert.That(Directory.Exists(destination + ".pcls-partial"), Is.False);
        }

        [Test]
        public void CopyDirectory_PreservesNestedStructureAndRemovesPartialDirectory()
        {
            var source = Path.Combine(testRoot, "source-folder");
            var destination = Path.Combine(testRoot, "cache", "game-folder");
            Directory.CreateDirectory(Path.Combine(source, "nested"));
            File.WriteAllText(Path.Combine(source, "root.txt"), "root");
            File.WriteAllText(Path.Combine(source, "nested", "child.txt"), "child");

            var result = new LocalTransferAdapter().CopyDirectory(
                source,
                destination,
                CancellationToken.None,
                null);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.ReadAllText(Path.Combine(destination, "root.txt")), Is.EqualTo("root"));
            Assert.That(File.ReadAllText(Path.Combine(destination, "nested", "child.txt")), Is.EqualTo("child"));
            Assert.That(Directory.Exists(destination + ".pcls-partial"), Is.False);
        }

        [Test]
        public void CopyFile_Cancellation_RemovesPartialDataAndLeavesDestinationMissing()
        {
            var source = Path.Combine(testRoot, "large.bin");
            var destination = Path.Combine(testRoot, "cache", "large.bin");
            File.WriteAllBytes(source, new byte[3 * 1024 * 1024]);
            var cancellation = new CancellationTokenSource();

            var result = new LocalTransferAdapter(64 * 1024).CopyFile(
                source,
                destination,
                cancellation.Token,
                (transferred, total) =>
                {
                    if (transferred > 0)
                    {
                        cancellation.Cancel();
                    }
                });

            Assert.That(result.Cancelled, Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(File.Exists(destination), Is.False);
            Assert.That(File.Exists(destination + ".pcls-partial"), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Copy_PreexistingStagingIsPreserved(bool directory)
        {
            var source = Path.Combine(testRoot, "source");
            var destination = Path.Combine(testRoot, "destination");
            var id = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, id);
            var sentinel = directory ? Path.Combine(partial, "keep.txt") : partial;
            if (directory) { Directory.CreateDirectory(source); Directory.CreateDirectory(partial); }
            else File.WriteAllText(source, "source");
            File.WriteAllText(sentinel, "preserve");
            var adapter = new LocalTransferAdapter();
            var result = directory
                ? adapter.CopyDirectory(source, destination, id, CancellationToken.None, null)
                : adapter.CopyFile(source, destination, id, CancellationToken.None, null);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(File.ReadAllText(sentinel), Is.EqualTo("preserve"));
            Assert.That(File.Exists(destination) || Directory.Exists(destination), Is.False);
        }

        [Test]
        public void NativeHandlePublicationPublishesVerifiedSourceToVacantSibling()
        {
            var staging = Path.Combine(testRoot, "staging.bin");
            var destination = Path.Combine(testRoot, "destination.bin");
            File.WriteAllText(staging, "owned");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var open = typeof(TransferPartialPathPolicy).GetMethod("OpenPrivateFileForReview", flags);
            var publish = typeof(TransferPartialPathPolicy).GetMethod("PublishVerifiedHandle", flags);
            using (var stream = (FileStream)open.Invoke(null, new object[] { staging, true }))
            {
                // Unwrapped native failure details remain visible in CI.
                try { publish.Invoke(null, new object[] { stream.SafeFileHandle, staging, destination }); }
                catch (System.Reflection.TargetInvocationException ex) { throw ex.InnerException; }
            }
            Assert.That(File.Exists(staging), Is.False);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("owned"));
        }

        [Test]
        public void NativeHandlePublicationRefusesUnrelatedSourceHandle()
        {
            var actual = Path.Combine(testRoot, "actual.bin");
            var staging = Path.Combine(testRoot, "staging.bin");
            var destination = Path.Combine(testRoot, "destination.bin");
            File.WriteAllText(actual, "unrelated");
            File.WriteAllText(staging, "staging");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var open = typeof(TransferPartialPathPolicy).GetMethod("OpenPrivateFileForReview", flags);
            var publish = typeof(TransferPartialPathPolicy).GetMethod("PublishVerifiedHandle", flags);
            using (var stream = (FileStream)open.Invoke(null, new object[] { actual, true }))
            {
                var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                    publish.Invoke(null, new object[] { stream.SafeFileHandle, staging, destination }));
                Assert.That(exception.InnerException, Is.TypeOf<IOException>());
            }
            Assert.That(File.ReadAllText(actual), Is.EqualTo("unrelated"));
            Assert.That(File.ReadAllText(staging), Is.EqualTo("staging"));
            Assert.That(File.Exists(destination), Is.False);
        }

        [Test]
        public void NativeHandlePublicationRefusesExistingTargetWithoutReplacingEitherFile()
        {
            var staging = Path.Combine(testRoot, "staging.bin");
            var destination = Path.Combine(testRoot, "destination.bin");
            File.WriteAllText(staging, "owned");
            File.WriteAllText(destination, "foreign");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var open = typeof(TransferPartialPathPolicy).GetMethod("OpenPrivateFileForReview", flags);
            var publish = typeof(TransferPartialPathPolicy).GetMethod("PublishVerifiedHandle", flags);
            Assert.That(open, Is.Not.Null);
            Assert.That(publish, Is.Not.Null);
            using (var stream = (FileStream)open.Invoke(null, new object[] { staging, true }))
            {
                var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                    publish.Invoke(null, new object[] { stream.SafeFileHandle, staging, destination }));
                Assert.That(exception.InnerException, Is.TypeOf<IOException>());
            }
            Assert.That(File.ReadAllText(staging), Is.EqualTo("owned"));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("foreign"));
        }

        [Test]
        public void ExclusiveStagingCreationNeverAdoptsExistingDirectory()
        {
            var path = Path.Combine(testRoot, "exclusive");
            var create = typeof(TransferPartialPathPolicy).GetMethod("CreateExclusiveDirectory",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(create, Is.Not.Null);
            create.Invoke(null, new object[] { path });
            var sentinel = Path.Combine(path, "keep.txt");
            File.WriteAllText(sentinel, "preserve");
            var failure = Assert.Throws<System.Reflection.TargetInvocationException>(
                () => create.Invoke(null, new object[] { path }));
            Assert.That(failure.InnerException, Is.TypeOf<IOException>());
            Assert.That(File.ReadAllText(sentinel), Is.EqualTo("preserve"));
        }

        [Test]
        public void CopyFile_DestinationAppearsAtFinalizationPreservesBothPaths()
        {
            var source = Path.Combine(testRoot, "source.txt");
            var destination = Path.Combine(testRoot, "destination.txt");
            File.WriteAllText(source, "original");
            var jobId = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, jobId);
            var result = new LocalTransferAdapter().CopyFile(source, destination, jobId,
                CancellationToken.None, null, state =>
                {
                    if (state == CloudTransferState.Finalizing) File.WriteAllText(destination, "foreign");
                });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RequiresManualRecovery, Is.True);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("foreign"));
            Assert.That(File.ReadAllText(partial), Is.EqualTo("original"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CopyDirectory_CancellationDoesNotRecursivelyDeleteUnexpectedContent(bool inject)
        {
            var source = Path.Combine(testRoot, "source");
            var destination = Path.Combine(testRoot, "destination");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "file.txt"), "owned");
            var jobId = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, jobId);
            var foreign = Path.Combine(partial, "foreign.txt");
            using (var cancellation = new CancellationTokenSource())
            {
                var result = new LocalTransferAdapter().CopyDirectory(source, destination, jobId,
                    cancellation.Token, (bytes, total) =>
                    {
                        if (inject) File.WriteAllText(foreign, "preserve");
                        cancellation.Cancel();
                    });
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Cancelled, Is.EqualTo(!inject));
                Assert.That(result.RequiresManualRecovery, Is.EqualTo(inject));
                Assert.That(Directory.Exists(destination), Is.False);
                Assert.That(File.Exists(Path.Combine(partial, "file.txt")), Is.False);
                if (inject) Assert.That(File.ReadAllText(foreign), Is.EqualTo("preserve"));
                else Assert.That(Directory.Exists(partial), Is.False);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CopyDirectory_ReplacedEmptyDirectoryIsPreserved(bool cancel)
        {
            var source = Path.Combine(testRoot, "source");
            var destination = Path.Combine(testRoot, "destination");
            Directory.CreateDirectory(Path.Combine(source, "empty"));
            File.WriteAllText(Path.Combine(source, "file.txt"), "owned");
            var jobId = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, jobId);
            var empty = Path.Combine(partial, "empty");
            var moved = Path.Combine(testRoot, "original-empty");
            using (var cancellation = new CancellationTokenSource())
            {
                var result = new LocalTransferAdapter().CopyDirectory(source, destination, jobId,
                    cancellation.Token, null, state =>
                    {
                        if (state != CloudTransferState.Finalizing) return;
                        Directory.Move(empty, moved);
                        Directory.CreateDirectory(empty);
                        if (cancel) cancellation.Cancel();
                    });
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.RequiresManualRecovery, Is.True);
                Assert.That(Directory.Exists(empty), Is.True);
                Assert.That(Directory.Exists(moved), Is.True);
                Assert.That(Directory.Exists(destination), Is.False);
            }
        }

        [Test]
        public void CopyDirectory_UnexpectedEmptyFileAtFinalizationCannotPublish()
        {
            var source = Path.Combine(testRoot, "source");
            var destination = Path.Combine(testRoot, "destination");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "file.txt"), "owned");
            var jobId = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, jobId);
            var foreign = Path.Combine(partial, "foreign.txt");
            var result = new LocalTransferAdapter().CopyDirectory(source, destination, jobId,
                CancellationToken.None, null, state =>
                {
                    if (state == CloudTransferState.Finalizing) File.WriteAllBytes(foreign, new byte[0]);
                });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RequiresManualRecovery, Is.True);
            Assert.That(File.Exists(foreign), Is.True);
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [Test]
        public void CopyDirectory_UnexpectedFileDuringCopyIsPreserved()
        {
            var source = Path.Combine(testRoot, "source");
            var destination = Path.Combine(testRoot, "destination");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "first.txt"), "first");
            File.WriteAllText(Path.Combine(source, "second.txt"), "second");
            var jobId = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, jobId);
            string foreign = null;
            var result = new LocalTransferAdapter().CopyDirectory(source, destination, jobId,
                CancellationToken.None, (bytes, total) =>
                {
                    if (foreign != null) return;
                    foreign = File.Exists(Path.Combine(partial, "first.txt"))
                        ? Path.Combine(partial, "second.txt") : Path.Combine(partial, "first.txt");
                    File.WriteAllText(foreign, "foreign");
                });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RequiresManualRecovery, Is.True);
            Assert.That(File.ReadAllText(foreign), Is.EqualTo("foreign"));
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CopyFile_SameLengthModificationIsPreserved(bool cancel)
        {
            var source = Path.Combine(testRoot, "source.txt");
            var destination = Path.Combine(testRoot, "destination.txt");
            File.WriteAllText(source, "original");
            var jobId = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, jobId);
            using (var cancellation = new CancellationTokenSource())
            {
                var result = new LocalTransferAdapter().CopyFile(source, destination, jobId,
                    cancellation.Token, null, state =>
                    {
                        if (state != CloudTransferState.Finalizing) return;
                        File.WriteAllText(partial, "modified");
                        if (cancel) cancellation.Cancel();
                    });
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.RequiresManualRecovery, Is.True);
                Assert.That(File.ReadAllText(partial), Is.EqualTo("modified"));
                Assert.That(File.Exists(destination), Is.False);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CopyFile_SameByteReplacementIsPreserved(bool cancel)
        {
            var source = Path.Combine(testRoot, "source.txt");
            var destination = Path.Combine(testRoot, "destination.txt");
            File.WriteAllText(source, "original");
            var jobId = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, jobId);
            var moved = Path.Combine(testRoot, "original-staging.txt");
            using (var cancellation = new CancellationTokenSource())
            {
                var result = new LocalTransferAdapter().CopyFile(source, destination, jobId,
                    cancellation.Token, null, state =>
                    {
                        if (state != CloudTransferState.Finalizing) return;
                        File.Move(partial, moved);
                        File.Copy(moved, partial);
                        if (cancel) cancellation.Cancel();
                    });
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.RequiresManualRecovery, Is.True);
                Assert.That(File.ReadAllText(moved), Is.EqualTo("original"));
                Assert.That(File.ReadAllText(partial), Is.EqualTo("original"));
                Assert.That(File.Exists(destination), Is.False);
            }
        }

        [Test]
        public void CopyFile_ExistingDestination_FailsWithoutOverwriting()
        {
            var source = Path.Combine(testRoot, "source.txt");
            var destination = Path.Combine(testRoot, "destination.txt");
            File.WriteAllText(source, "new");
            File.WriteAllText(destination, "existing");

            var result = new LocalTransferAdapter().CopyFile(
                source,
                destination,
                CancellationToken.None,
                null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("already exists"));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("existing"));
        }

        [Test]
        public void CopyFile_JobOwnedPartialPathIsUniqueAndRemovedAfterAtomicMove()
        {
            var source = Path.Combine(testRoot, "source-owned.txt");
            var destination = Path.Combine(testRoot, "cache", "owned.txt");
            var jobId = Guid.NewGuid();
            File.WriteAllText(source, "owned");

            var result = new LocalTransferAdapter().CopyFile(
                source,
                destination,
                jobId,
                CancellationToken.None,
                null);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(destination), Is.True);
            Assert.That(File.Exists(TransferPartialPathPolicy.Create(destination, jobId)), Is.False);
        }
    }
}
