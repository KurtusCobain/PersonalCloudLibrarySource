using NUnit.Framework;
using Playnite.SDK.Models;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class ZipMutableDataBaselineStoreTests
    {
        private string root;
        private string cache;
        private string archive;
        private ZipManagedExtractionPlan extraction;
        private CacheOwnershipReceiptStore receipts;
        private ZipMutableDataBaselineStore baselines;
        private ZipLauncherApprovalService approvals;
        private PersonalCloudLibraryItem item;
        private PersonalCloudLibrarySourceSettings settings;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-SaveBaseline",
                Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            archive = Path.Combine(root, "game.zip");
            using (var stream = new FileStream(archive, FileMode.CreateNew,
                FileAccess.ReadWrite))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                Add(zip, "Game.exe", "original game executable");
                Add(zip, "support.dll", "original game library");
                Add(zip, "original.sav", "bundled original save");
                Add(zip, "data.pak", "original game data");
            }

            receipts = new CacheOwnershipReceiptStore(Path.Combine(root, "receipts"));
            baselines = new ZipMutableDataBaselineStore(Path.Combine(root, "baselines"));
            approvals = new ZipLauncherApprovalService(Path.Combine(root, "approvals"));
            item = new PersonalCloudLibraryItem
            {
                Id = "game", Title = "Game", SourceType = "file",
                SourcePath = archive, CachePath = "downloads/game.zip"
            };
            settings = new PersonalCloudLibrarySourceSettings
            {
                Enabled = true, AllowDownloads = true,
                LocalCacheFolder = cache,
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = Path.Combine(root, "manifest.json")
            };

            var inspector = new ZipPackageReviewService();
            var reviewed = inspector.Inspect(archive, item.Title);
            Assert.That(reviewed.PassedPreflight, Is.True, reviewed.Reason);
            var extractor = new ZipManagedExtractionService();
            extraction = extractor.Prepare(reviewed, archive, cache, item.Id, receipts);
            Assert.That(extraction, Is.Not.Null);
            var result = extractor.ExecuteApproved(extraction, true, receipts,
                CancellationToken.None, null, baselines);
            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.ReceiptRecorded, Is.True, result.Reason);
            Assert.That(result.MutableDataBaselineRecorded, Is.True,
                "New managed ZIP extractions must capture original paths.");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private static void Add(ZipArchive zip, string name, string text)
        {
            using (var output = zip.CreateEntry(name).Open())
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                output.Write(bytes, 0, bytes.Length);
            }
        }

        private bool Verified(out int extraSaves)
        {
            return baselines.TryVerify(cache, item.Id, extraction.Destination,
                receipts, CancellationToken.None, out extraSaves);
        }

        private GameMetadata Game() => new GameMetadata
        {
            GameId = item.Id, Name = item.Title, IsInstalled = false
        };

        private GameAction ApproveAndGetAction()
        {
            string reason;
            Assert.That(approvals.TryApprove(item, settings, "Game.exe",
                receipts, true, CancellationToken.None, out reason, baselines),
                Is.True, reason);
            var metadata = Game();
            Assert.That(approvals.TryApply(item, settings, metadata,
                receipts, CancellationToken.None, baselines), Is.True);
            return metadata.GameActions.Single();
        }

        [Test]
        public void OriginalFilesAreVerifiedExactlyAndBaselineIsCreateOnly()
        {
            int extras;
            Assert.That(Verified(out extras), Is.True);
            Assert.That(extras, Is.Zero);
            CacheOwnershipReceipt receipt;
            Assert.That(receipts.TryRead(cache, extraction.ReceiptKey, out receipt),
                Is.True);
            Assert.That(receipt.Version, Is.EqualTo(2));
            Assert.That(receipt.InventoryFileCount, Is.EqualTo(4));
            Assert.That(File.ReadAllText(Path.Combine(extraction.Destination,
                "original.sav")), Is.EqualTo("bundled original save"));

            var snapshot = new CacheTreeInventoryScanner().Scan(cache,
                extraction.Destination);
            Assert.That(baselines.TryRecord(cache, item.Id, extraction.Destination,
                receipts, snapshot), Is.False, "Baseline must never overwrite.");
            Assert.That(Verified(out extras), Is.True);
        }

        [Test]
        public void NewlyAddedRootSaveFilesDoNotInvalidateExecutableAttestation()
        {
            var action = ApproveAndGetAction();
            File.WriteAllText(Path.Combine(extraction.Destination, "progress.sav"),
                "valuable progress");
            File.WriteAllText(Path.Combine(extraction.Destination, "backup.SAVE"),
                "valuable backup");
            int extras;
            Assert.That(Verified(out extras), Is.True);
            Assert.That(extras, Is.EqualTo(2));

            var metadata = Game();
            Assert.That(approvals.TryApply(item, settings, metadata,
                receipts, CancellationToken.None, baselines), Is.True);
            Assert.That(metadata.GameActions.Single().Path, Is.EqualTo(action.Path));

            // Legacy strict receipt comparison still considers the new data
            // a change; this does not weaken maintenance/delete policy.
            bool strict;
            Assert.That(receipts.TryCompareInventory(cache, extraction.ReceiptKey,
                extraction.Destination, out strict), Is.True);
            Assert.That(strict, Is.False);
        }

        [Test]
        public void NewSavesAndSavegamesDirectoriesAllowOnlySaveData()
        {
            Directory.CreateDirectory(Path.Combine(extraction.Destination,
                "saves", "slot1"));
            File.WriteAllText(Path.Combine(extraction.Destination,
                "saves", "slot1", "game.sav"), "data");
            Directory.CreateDirectory(Path.Combine(extraction.Destination,
                "savegames"));
            File.WriteAllText(Path.Combine(extraction.Destination,
                "savegames", "autosave.save"), "data");
            int extras;
            Assert.That(Verified(out extras), Is.True);
            Assert.That(extras, Is.EqualTo(2));
        }

        [Test]
        public void TooManyNewSaveDirectoriesFailClosedWithoutChangingFiles()
        {
            var saves = Path.Combine(extraction.Destination, "saves");
            Directory.CreateDirectory(saves);
            for (var i = 0; i < 257; i++)
                Directory.CreateDirectory(Path.Combine(saves,
                    "slot-" + i.ToString("D3")));
            int extras;
            Assert.That(Verified(out extras), Is.False,
                "Mutable directories must not bypass the inventory entry bound.");
            Assert.That(File.ReadAllText(Path.Combine(extraction.Destination,
                "Game.exe")), Is.EqualTo("original game executable"));
            Assert.That(Directory.Exists(Path.Combine(saves, "slot-256")), Is.True);
        }

        [Test]
        public void NewlyAddedExecutableCannotBeIgnoredEvenUnderSaveDirectory()
        {
            var action = ApproveAndGetAction();
            Directory.CreateDirectory(Path.Combine(extraction.Destination, "saves"));
            var injected = Path.Combine(extraction.Destination, "saves", "helper.exe");
            File.WriteAllText(injected, "injected executable");
            int extras;
            Assert.That(Verified(out extras), Is.False);
            Assert.That(approvals.TryApply(item, settings, Game(),
                receipts, CancellationToken.None, baselines), Is.False);
            Assert.That(File.Exists(action.Path), Is.True);
            Assert.That(File.ReadAllText(injected),
                Is.EqualTo("injected executable"), "Verification never deletes content.");
        }

        [Test]
        public void NewDllScriptAndUnlistedDirectoryAreRejected()
        {
            foreach (var filename in new[] { "evil.dll", "run.cmd", "mod.ps1", "launch.bat" })
            {
                var path = Path.Combine(extraction.Destination, filename);
                File.WriteAllText(path, "not permitted");
                int extras;
                Assert.That(Verified(out extras), Is.False, filename);
                File.Delete(path);
            }
            var directory = Path.Combine(extraction.Destination, "plugins");
            Directory.CreateDirectory(directory);
            int after;
            Assert.That(Verified(out after), Is.False);
        }

        [Test]
        public void ModifiedOriginalExecutableOrDllAlwaysFails()
        {
            foreach (var name in new[] { "Game.exe", "support.dll" })
            {
                var path = Path.Combine(extraction.Destination, name);
                var before = File.ReadAllBytes(path);
                File.WriteAllText(path, "tampered bytes");
                int extras;
                Assert.That(Verified(out extras), Is.False, name);
                File.WriteAllBytes(path, before);
            }
            int safe;
            Assert.That(Verified(out safe), Is.True);
        }

        [Test]
        public void OriginalBundledSaveAndOtherDataFilesRemainImmutable()
        {
            foreach (var name in new[] { "original.sav", "data.pak" })
            {
                var path = Path.Combine(extraction.Destination, name);
                var before = File.ReadAllBytes(path);
                File.WriteAllText(path, "modified bundled data");
                int extras;
                Assert.That(Verified(out extras), Is.False, name);
                File.WriteAllBytes(path, before);
            }
        }

        [Test]
        public void NoBaselineNeverAutomaticallyAdoptsNewSaveContent()
        {
            var missing = new ZipMutableDataBaselineStore(
                Path.Combine(root, "missing-baselines"));
            File.WriteAllText(Path.Combine(extraction.Destination, "progress.sav"), "data");
            int extras;
            Assert.That(missing.TryVerify(cache, item.Id, extraction.Destination,
                receipts, CancellationToken.None, out extras), Is.False);
            Assert.That(new ZipExtractedLauncherReviewService().Inspect(
                cache, item.Id, item.Title, receipts,
                CancellationToken.None, missing).InventoryVerified, Is.False);
        }

        [Test]
        public void CorruptBaselineCannotAuthorizeMutableData()
        {
            var file = Directory.GetFiles(Path.Combine(root, "baselines"), "*.json")
                .Single();
            File.WriteAllText(file, "{ corrupt");
            File.WriteAllText(Path.Combine(extraction.Destination, "latest.sav"), "data");
            int extras;
            Assert.That(Verified(out extras), Is.False);
            Assert.That(approvals.TryApply(item, settings, Game(),
                receipts, CancellationToken.None, baselines), Is.False);
            Assert.That(File.Exists(Path.Combine(extraction.Destination, "latest.sav")),
                Is.True);
        }

        [Test]
        public void DifferentGameAndCacheCannotBorrowBaseline()
        {
            int extras;
            Assert.That(baselines.TryVerify(cache, "other-game",
                extraction.Destination, receipts, CancellationToken.None,
                out extras), Is.False);
            var otherCache = Path.Combine(root, "other-cache");
            Directory.CreateDirectory(otherCache);
            Assert.That(baselines.TryVerify(otherCache, item.Id,
                extraction.Destination, receipts, CancellationToken.None,
                out extras), Is.False);
        }

        [Test]
        public void CancelledVerificationFailsClosedAndPreservesFiles()
        {
            using (var stop = new CancellationTokenSource())
            {
                stop.Cancel();
                int extras;
                Assert.That(baselines.TryVerify(cache, item.Id, extraction.Destination,
                    receipts, stop.Token, out extras), Is.False);
            }
            Assert.That(File.Exists(Path.Combine(extraction.Destination, "Game.exe")),
                Is.True);
        }

        [Test]
        public void LiveLaunchGateAllowsOnlyOriginalFilesPlusNewSaves()
        {
            var play = ApproveAndGetAction();
            File.WriteAllText(Path.Combine(extraction.Destination, "progress.sav"),
                "data");
            var id = Guid.NewGuid();
            var game = new Game("Game") { PluginId = id, GameId = item.Id };
            var gate = new ZipManagedLaunchGate();
            var decision = gate.Evaluate(id, game, play, item,
                settings, receipts, approvals, CancellationToken.None, baselines);
            Assert.That(decision.Applies, Is.True);
            Assert.That(decision.Allowed, Is.True, decision.Reason);

            File.WriteAllText(Path.Combine(extraction.Destination, "helper.dll"),
                "untrusted");
            Assert.That(gate.Evaluate(id, game, play, item, settings,
                receipts, approvals, CancellationToken.None,
                baselines).Allowed, Is.False);
        }

        [Test]
        public void LaunchGateCancelledBeforeVerificationNeverPermitsPlay()
        {
            var play = ApproveAndGetAction();
            var id = Guid.NewGuid();
            var game = new Game("Game") { PluginId = id, GameId = item.Id };
            using (var stop = new CancellationTokenSource())
            {
                stop.Cancel();
                var decision = new ZipManagedLaunchGate().Evaluate(id, game,
                    play, item, settings, receipts, approvals, stop.Token, baselines);
                Assert.That(decision.Applies, Is.True);
                Assert.That(decision.Allowed, Is.False);
            }
        }

        [Test]
        public void AddedNonSaveUnderRootNeverBecomesTrustedByRepeatedScans()
        {
            var executable = Path.Combine(extraction.Destination, "unexpected.exe");
            File.WriteAllText(executable, "untrusted");
            for (var i = 0; i < 3; i++)
            {
                int extras;
                Assert.That(Verified(out extras), Is.False);
            }
            Assert.That(File.Exists(executable), Is.True);
        }
    }
}
