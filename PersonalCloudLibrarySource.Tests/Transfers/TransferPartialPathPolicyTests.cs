using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class TransferPartialPathPolicyTests
    {
        [Test]
        public void Create_UsesUniqueSiblingOnDestinationVolume()
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-PartialPolicyTests", Guid.NewGuid().ToString("N"));
            var destination = Path.Combine(root, "cache", "game.zip");
            var jobId = Guid.NewGuid();

            var partial = TransferPartialPathPolicy.Create(destination, jobId);

            Assert.That(Path.GetDirectoryName(partial), Is.EqualTo(Path.GetDirectoryName(Path.GetFullPath(destination))));
            Assert.That(Path.GetFileName(partial), Does.Contain(jobId.ToString("N")));
            Assert.That(partial, Does.StartWith(Path.GetFullPath(destination) + ".pcls-partial-"));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void Create_RejectsMissingDestination(string destination)
        {
            Assert.Throws<ArgumentException>(() => TransferPartialPathPolicy.Create(destination, Guid.NewGuid()));
        }

        [Test]
        public void Create_RejectsEmptyJobIdentity()
        {
            Assert.Throws<ArgumentException>(() => TransferPartialPathPolicy.Create("destination", Guid.Empty));
        }
    }
}
