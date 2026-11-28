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

        [TestCase(@"C:\fixture.zip")]
        [TestCase(@"\\fixture-host\fixture-share\fixture.zip")]
        public void Create_RootChildRetainsCanonicalParentWithoutFilesystemAccess(string destination)
        {
            var id = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, id);
            Assert.That(partial, Is.EqualTo(destination + ".pcls-partial-" + id.ToString("N")));
            Assert.That(PathBoundary.Normalize(Path.GetDirectoryName(partial)),
                Is.EqualTo(PathBoundary.Normalize(Path.GetDirectoryName(destination))));
        }

        [Test]
        public void PublicationAncestorGuardRefusesOrdinaryDirectoryRenamesUntilReleased()
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-AncestorGuardTests", Guid.NewGuid().ToString("N"));
            var grandparent = Path.Combine(root, "grandparent");
            var parent = Path.Combine(grandparent, "parent");
            var movedParent = Path.Combine(grandparent, "moved-parent");
            var movedGrandparent = Path.Combine(root, "moved-grandparent");
            Directory.CreateDirectory(parent);
            try
            {
                var method = typeof(TransferPartialPathPolicy).GetMethod("PinPublicationAncestors",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Assert.That(method, Is.Not.Null);
                using (var guard = (IDisposable)method.Invoke(null, new object[] { parent }))
                {
                    Assert.Catch(() => Directory.Move(parent, movedParent));
                    Assert.Catch(() => Directory.Move(grandparent, movedGrandparent));
                    Assert.That(Directory.Exists(parent), Is.True);
                    Assert.That(Directory.Exists(movedParent), Is.False);
                    Assert.That(Directory.Exists(movedGrandparent), Is.False);
                    // Ordinary child creation is not blocked by the guard.
                    File.WriteAllText(Path.Combine(parent, "fixture.txt"), "fixture");
                }
                Directory.Move(parent, movedParent);
                Assert.That(File.ReadAllText(Path.Combine(movedParent, "fixture.txt")), Is.EqualTo("fixture"));
                Directory.Move(grandparent, movedGrandparent);
                Assert.That(Directory.Exists(Path.Combine(movedGrandparent, "moved-parent")), Is.True);
            }
            finally { Directory.Delete(root, true); }
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
                        backup, validate, "invalid original", 1024 }));
                Assert.That(failure.InnerException, occupiedBackup
                    ? Is.TypeOf<IOException>() : Is.TypeOf<InvalidOperationException>());
                Assert.That(File.ReadAllText(primary), Is.EqualTo("original"));
                Assert.That(File.Exists(stage), Is.False);
                if (occupiedBackup) Assert.That(File.ReadAllText(backup), Is.EqualTo("unrelated backup"));
                else Assert.That(File.Exists(backup), Is.False);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void ReplacementRetainsVerifiedStageIdentityAndOriginalBackupIdentity()
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-RecordIdentityTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var primary = Path.Combine(root, "record.json");
                var stage = Path.Combine(root, "stage.tmp");
                var backup = Path.Combine(root, "backup.json");
                File.WriteAllText(primary, "original");
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                var capture = typeof(TransferPartialPathPolicy).GetMethod("CapturePrivateFileIdentity", flags);
                var same = typeof(TransferPartialPathPolicy).GetMethod("SameIdentity", flags);
                var replace = typeof(TransferPartialPathPolicy).GetMethod("ReplacePrivateRecord", flags);
                Assert.That(capture, Is.Not.Null);
                Assert.That(same, Is.Not.Null);
                Assert.That(replace, Is.Not.Null);
                Func<string, object> identity = path =>
                {
                    using (var stream = File.OpenRead(path))
                        return capture.Invoke(null, new object[] { stream.SafeFileHandle });
                };
                var originalIdentity = identity(primary);
                object stageIdentity = null;
                Func<bool> validate = () =>
                {
                    if (File.Exists(stage)) stageIdentity = identity(stage);
                    return true;
                };
                var payload = System.Text.Encoding.UTF8.GetBytes("replacement");
                replace.Invoke(null, new object[] { primary, payload, stage, backup, validate, "invalid", 1024 });
                Assert.That(stageIdentity, Is.Not.Null);
                Assert.That(same.Invoke(null, new[] { stageIdentity, identity(primary) }), Is.EqualTo(true));
                Assert.That(same.Invoke(null, new[] { originalIdentity, identity(backup) }), Is.EqualTo(true));
                Assert.That(File.ReadAllText(primary), Is.EqualTo("replacement"));
                Assert.That(File.ReadAllText(backup), Is.EqualTo("original"));
                Assert.That(File.Exists(stage), Is.False);
            }
            finally { Directory.Delete(root, true); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancellationRemovesOnlyVerifiedUnpublishedPrivateStage(bool changed)
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-CancelRecord-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var stage = Path.Combine(root, "stage.tmp");
                var target = Path.Combine(root, "record.json");
                var unrelated = Path.Combine(root, "keep.txt");
                File.WriteAllText(target, "existing target");
                File.WriteAllText(unrelated, "unrelated");
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
                var write = typeof(TransferPartialPathPolicy).GetMethod("WriteRecordStage", flags);
                var complete = typeof(TransferPartialPathPolicy).GetMethod("CompletePrivateRecordStage", flags);
                Assert.That(write, Is.Not.Null);
                Assert.That(complete, Is.Not.Null);
                var payload = new byte[] { 1, 2, 3, 4 };
                var identity = write.Invoke(null, new object[] { stage, payload });
                if (changed) File.WriteAllBytes(stage, new byte[] { 4, 3, 2, 1 });
                using (var stop = new System.Threading.CancellationTokenSource())
                {
                    stop.Cancel();
                    var failure = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                        complete.Invoke(null, new object[] { stage, target, identity, payload, stop.Token }));
                    Assert.That(failure.InnerException, changed
                        ? Is.TypeOf<IOException>() : Is.TypeOf<OperationCanceledException>());
                }
                Assert.That(File.Exists(stage), Is.EqualTo(changed));
                if (changed) Assert.That(File.ReadAllBytes(stage), Is.EqualTo(new byte[] { 4, 3, 2, 1 }));
                Assert.That(File.ReadAllText(target), Is.EqualTo("existing target"));
                Assert.That(File.ReadAllText(unrelated), Is.EqualTo("unrelated"));
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
