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
    }
}
