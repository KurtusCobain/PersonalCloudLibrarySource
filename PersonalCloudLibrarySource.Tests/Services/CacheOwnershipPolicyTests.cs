using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class CacheOwnershipPolicyTests
    {
        private string root;
        private string destination;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-Ownership", Guid.NewGuid().ToString("N"));
            destination = Path.Combine(root, "game");
            Directory.CreateDirectory(destination);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void ExistingLegacyDirectory_IsNotAutomaticallyOwned()
        {
            var result = new CacheOwnershipPolicy().Assess(root, destination, null, null, "game-id");
            Assert.That(result.Kind, Is.EqualTo(CacheOwnershipKind.LegacyUnverified));
            Assert.That(result.MayDeleteAutomatically, Is.False);
        }

        [Test]
        public void MatchingReceipt_IsClassifiedAsVerified()
        {
            var result = new CacheOwnershipPolicy().Assess(root, destination, destination, "game-id", "game-id");
            Assert.That(result.Kind, Is.EqualTo(CacheOwnershipKind.VerifiedReceipt));
            Assert.That(result.MayDeleteAutomatically, Is.False);
        }

        [Test]
        public void ReceiptForAnotherGame_IsNotTrusted()
        {
            var result = new CacheOwnershipPolicy().Assess(root, destination, destination, "other-id", "game-id");
            Assert.That(result.Kind, Is.EqualTo(CacheOwnershipKind.ReceiptMismatch));
            Assert.That(result.MayDeleteAutomatically, Is.False);
        }

        [Test]
        public void ExistingFolderOutsideCache_IsNotOwned()
        {
            var result = new CacheOwnershipPolicy().Assess(Path.Combine(root, "other"),
                destination, destination, "game-id", "game-id");
            Assert.That(result.MayDeleteAutomatically, Is.False);
        }

        [Test]
        public void MissingDestination_IsNotOwned()
        {
            Directory.Delete(destination);
            var result = new CacheOwnershipPolicy().Assess(root, destination, destination, "game-id", "game-id");
            Assert.That(result.Kind, Is.EqualTo(CacheOwnershipKind.Missing));
        }
    }
}
