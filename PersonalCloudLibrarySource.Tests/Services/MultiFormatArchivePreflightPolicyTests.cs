using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class MultiFormatArchivePreflightPolicyTests
    {
        private static MultiFormatArchiveEntry File(string path = "Game/play.exe")
        {
            return new MultiFormatArchiveEntry
            {
                Path = path, IsComplete = true, UncompressedBytes = 100,
                CompressedBytes = 70
            };
        }

        private static MultiFormatArchiveReview Check(params MultiFormatArchiveEntry[] entries)
        {
            return new MultiFormatArchivePreflightPolicy()
                .Inspect(entries, 512);
        }

        [Test]
        public void ValidSmallArchivePassesButCannotAuthorizeAnyFilesystemWrite()
        {
            var valid = Check(new MultiFormatArchiveEntry
            {
                Path = "Game/", IsDirectory = true, IsComplete = true
            }, File(), File("Game/data.bin"));
            Assert.That(valid.Passed, Is.True, valid.Reason);
            Assert.That(valid.EntryCount, Is.EqualTo(3));
            Assert.That(valid.FileCount, Is.EqualTo(2));
            Assert.That(valid.TotalUncompressedBytes, Is.EqualTo(200));
            Assert.That(valid.AuthorizesFilesystemWrite, Is.False);
            Assert.That(valid.CanExtractUnattended, Is.False);
        }

        [TestCase("Encrypted")]
        [TestCase("Incomplete")]
        [TestCase("Split")]
        [TestCase("Anti")]
        [TestCase("Link")]
        [TestCase("Reparse")]
        [TestCase("UnixLink")]
        [TestCase("Device")]
        public void UnsupportedOrDangerousEntryMetadataFailsClosed(string state)
        {
            var item = File();
            switch (state)
            {
                case "Encrypted": item.IsEncrypted = true; break;
                case "Incomplete": item.IsComplete = false; break;
                case "Split": item.IsSplitAfter = true; break;
                case "Anti": item.IsAntiItem = true; break;
                case "Link": item.LinkTarget = "../../victim"; break;
                case "Reparse": item.Attributes = 0x400; break;
                case "UnixLink": item.Attributes = unchecked((int)0xA0000000); break;
                case "Device": item.Attributes = unchecked((int)0x20000000); break;
            }
            var result = Check(item);
            Assert.That(result.Passed, Is.False, state);
            Assert.That(result.AuthorizesFilesystemWrite, Is.False);
        }

        [TestCase("../escape.exe")]
        [TestCase("Games/../../escape.exe")]
        [TestCase(@"Games\..\escape.exe")]
        [TestCase("C:/Windows/escape.exe")]
        [TestCase("/root/file.exe")]
        [TestCase("Game//file.exe")]
        [TestCase("game/./dot.exe")]
        [TestCase("game/CON.txt")]
        [TestCase("game/trailed./file.exe")]
        [TestCase("game/file.exe:stream")]
        public void UnsafePathIsRejectedBeforeAnyExtraction(string path)
        {
            Assert.That(Check(File(path)).Passed, Is.False, path);
        }

        [Test]
        public void ControlCharacterInArchivePathIsRejectedWithoutPuttingItInNUnitXml()
        {
            var unsafePath = "game/sub" + (char)1 + "/file.exe";
            Assert.That(Check(File(unsafePath)).Passed, Is.False);
        }

        [Test]
        public void WindowsCaseCollisionsAndParentFileConflictsFail()
        {
            Assert.That(Check(File("Game/Play.exe"), File("game/play.exe"))
                .Passed, Is.False);
            Assert.That(Check(File("Game"), File("Game/data.bin"))
                .Passed, Is.False);
        }

        [Test]
        public void SolidCompressionCannotUseFakePerEntryCompressedBytesAsAuthority()
        {
            var entry = File();
            entry.IsSolid = true;
            entry.CompressedBytes = 0;
            var reviewed = Check(entry);
            Assert.That(reviewed.Passed, Is.True, reviewed.Reason);
            Assert.That(reviewed.AuthorizesFilesystemWrite, Is.False);
        }

        [Test]
        public void SolidArchiveWithExcessiveTotalExpansionFails()
        {
            var entry = File();
            entry.IsSolid = true;
            entry.UncompressedBytes = 2 * 1024 * 1024;
            entry.CompressedBytes = 0;
            var reviewed = new MultiFormatArchivePreflightPolicy().Inspect(
                new[] { entry }, 100);
            Assert.That(reviewed.Passed, Is.False);
        }

        [Test]
        public void ActualMultiVolumeArchiveIsRefusedEvenIfEntriesLookComplete()
        {
            var policy = new MultiFormatArchivePreflightPolicy();
            Assert.That(policy.Inspect(new[] { File() }, 512, 2).Passed, Is.False);
            Assert.That(policy.Inspect(new[] { File() }, 512, 0).Passed, Is.False);
            Assert.That(policy.Inspect(new[] { File() }, 512, 1).Passed, Is.True);
        }

        [Test]
        public void MissingMetadataAndImpossibleSizesFail()
        {
            Assert.That(new MultiFormatArchivePreflightPolicy().Inspect(null, 10)
                .Passed, Is.False);
            Assert.That(Check().Passed, Is.False);
            Assert.That(new MultiFormatArchivePreflightPolicy()
                .Inspect(new[] { File() }, 0).Passed, Is.False);
            var negative = File();
            negative.UncompressedBytes = -1;
            Assert.That(Check(negative).Passed, Is.False);
            var huge = File();
            huge.UncompressedBytes = MultiFormatArchivePreflightPolicy.MaximumFileBytes + 1;
            Assert.That(Check(huge).Passed, Is.False);
        }

        [Test]
        public void EntryLimitIsBoundedAndFailsClosed()
        {
            var files = Enumerable.Range(0,
                MultiFormatArchivePreflightPolicy.MaximumEntries + 1)
                .Select(n => File("game/" + n + ".dat")).ToArray();
            Assert.That(new MultiFormatArchivePreflightPolicy()
                .Inspect(files, 4 * 1024 * 1024).Passed, Is.False);
        }

        [Test]
        public void MissingOrUnsafeDirectoriesRefuse()
        {
            var directoryWithBytes = new MultiFormatArchiveEntry
            {
                Path = "game/", IsDirectory = true,
                IsComplete = true, UncompressedBytes = 1
            };
            Assert.That(Check(directoryWithBytes).Passed, Is.False);
            Assert.That(Check(new MultiFormatArchiveEntry
            {
                Path = "game/", IsDirectory = true, IsComplete = true
            }).Passed, Is.False, "Archive requires a regular payload.");
        }
    }
}
