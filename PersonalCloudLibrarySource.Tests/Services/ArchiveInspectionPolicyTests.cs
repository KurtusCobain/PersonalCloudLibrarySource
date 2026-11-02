using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class ArchiveInspectionPolicyTests
    {
        private static ArchiveEntryObservation File(string path, long size = 10, long compressed = 9)
            => new ArchiveEntryObservation
            {
                EntryPath = path, UncompressedBytes = size, CompressedBytes = compressed
            };

        [Test]
        public void ClassifiesFormatsWithoutGuessingInstallerOrZipRom()
        {
            var policy = new PackageDiscoveryPolicy();
            Assert.That(policy.Classify("game.zip").Kind, Is.EqualTo(PackageCandidateKind.ArchiveNeedsReview));
            Assert.That(policy.Classify("game.7z").Kind, Is.EqualTo(PackageCandidateKind.ArchiveNeedsReview));
            Assert.That(policy.Classify("game.rar").Kind, Is.EqualTo(PackageCandidateKind.ArchiveNeedsReview));
            Assert.That(policy.Classify("setup.exe").Kind, Is.EqualTo(PackageCandidateKind.ExecutableNeedsReview));
            Assert.That(policy.Classify("game.nes").Kind, Is.EqualTo(PackageCandidateKind.DirectFile));
            Assert.That(policy.Classify("unknown.blob").Kind, Is.EqualTo(PackageCandidateKind.Unsupported));
            Assert.That(policy.Classify("rom.zip").CanInstallUnattended, Is.False);
            Assert.That(policy.Classify("setup.exe").CanInstallUnattended, Is.False);
        }

        [Test]
        public void SafeRelativeEntriesOnlyPassReadOnlyGate()
        {
            var inspection = new ArchiveInspectionPolicy().Inspect(new[]
            {
                new ArchiveEntryObservation { EntryPath = "MyGame/", IsDirectory = true },
                File("MyGame/play.exe"),
                File("MyGame/assets/config.ini")
            });
            Assert.That(inspection.AllowedForStagedExtraction, Is.True, inspection.Reason);
            Assert.That(inspection.TotalUncompressedBytes, Is.EqualTo(20));
            Assert.That(inspection.AuthorizesFilesystemWrite, Is.False);
        }

        [TestCase("../escape.exe")]
        [TestCase("game/../../escape.exe")]
        [TestCase("C:/Windows/escape.exe")]
        [TestCase("/absolute.exe")]
        [TestCase("game//double.exe")]
        [TestCase("game/./dot.exe")]
        [TestCase("game/CON.txt")]
        [TestCase("LPT1/file.exe")]
        [TestCase("game/trailing./file.exe")]
        [TestCase("game/trailing /file.exe")]
        [TestCase("game/file.exe:stream")]
        [TestCase("game/file?.exe")]
        public void UnsafeArchivePathFailsClosed(string entryPath)
        {
            var result = new ArchiveInspectionPolicy().Inspect(new[] { File(entryPath) });
            Assert.That(result.AllowedForStagedExtraction, Is.False, entryPath);
            Assert.That(result.AuthorizesFilesystemWrite, Is.False);
        }

        [Test]
        public void WindowsCaseInsensitiveCollisionsAreRejected()
        {
            var result = new ArchiveInspectionPolicy().Inspect(new[]
            {
                File("Game/Play.exe"), File("game/play.exe")
            });
            Assert.That(result.AllowedForStagedExtraction, Is.False);
        }

        [Test]
        public void SymlinksAndAmbiguousSizeAreRejected()
        {
            var linked = File("shortcut");
            linked.IsLink = true;
            Assert.That(new ArchiveInspectionPolicy().Inspect(new[] { linked })
                .AllowedForStagedExtraction, Is.False);
            Assert.That(new ArchiveInspectionPolicy().Inspect(new[] { File("x", -1, 1) })
                .AllowedForStagedExtraction, Is.False);
            Assert.That(new ArchiveInspectionPolicy().Inspect(new[] { File("x", 20000, 1) })
                .AllowedForStagedExtraction, Is.False);
            Assert.That(new ArchiveInspectionPolicy().Inspect(new[] { File("x", 1, 0) })
                .AllowedForStagedExtraction, Is.False);
        }

        [Test]
        public void EntryCountLimitAndEmptyArchivesRefuse()
        {
            var policy = new ArchiveInspectionPolicy();
            Assert.That(policy.Inspect(new ArchiveEntryObservation[0])
                .AllowedForStagedExtraction, Is.False);
            var repeated = new List<ArchiveEntryObservation>();
            for (var i = 0; i <= ArchiveInspectionPolicy.MaxEntries; i++)
                repeated.Add(File("file" + i));
            Assert.That(policy.Inspect(repeated).AllowedForStagedExtraction, Is.False);
        }

        [Test]
        public void NoArchivePreflightEverAllowsUnattendedExtractionOrWrites()
        {
            var result = new ArchiveInspectionPolicy().Inspect(new[] { File("game.exe") });
            Assert.That(result.AllowedForStagedExtraction, Is.True);
            Assert.That(result.AuthorizesFilesystemWrite, Is.False);
        }
    }
}
