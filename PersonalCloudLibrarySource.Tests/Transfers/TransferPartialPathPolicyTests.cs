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

        [TestCase(false)]
        [TestCase(true)]
        public void ReplacementRefusesInvalidOriginalOrOccupiedBackupBeforeStaging(bool occupiedBackup)
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-RecordReplaceTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var primary = Path.Combine(root, "record.json");
                var stage = Path.Combine(root, "stage.tmp");
                var backup = Path.Combine(root, "backup.json");
                File.WriteAllText(primary, "original");
                if (occupiedBackup) File.WriteAllText(backup, "unrelated backup");
                var method = typeof(TransferPartialPathPolicy).GetMethod("ReplacePrivateRecord",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Assert.That(method, Is.Not.Null);
                Func<bool> validate = () =>
                {
                    // The original must be held against an ordinary write
                    // while validation is being performed.
                    Assert.Throws<IOException>(() => File.WriteAllText(primary, "changed"));
                    return false;
                };
                var failure = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                    method.Invoke(null, new object[] { primary, new byte[] { 42 }, stage,
                        backup, validate, "invalid original" }));
                Assert.That(failure.InnerException, occupiedBackup
                    ? Is.TypeOf<IOException>() : Is.TypeOf<InvalidOperationException>());
                Assert.That(File.ReadAllText(primary), Is.EqualTo("original"));
                Assert.That(File.Exists(stage), Is.False);
                if (occupiedBackup) Assert.That(File.ReadAllText(backup), Is.EqualTo("unrelated backup"));
                else Assert.That(File.Exists(backup), Is.False);
            }
            finally { Directory.Delete(root, true); }
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
