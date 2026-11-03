using NUnit.Framework;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class ZipManagedMaintenanceServiceTests
    {
        private string root;
        private string cache;
        private string archive;
        private ZipManagedExtractionPlan extracted;
        private CacheOwnershipReceiptStore receipts;
        private ZipLauncherApprovalService approvals;
        private ZipManagedMaintenanceService maintenance;
        private PersonalCloudLibraryItem item;
        private PersonalCloudLibrarySourceSettings settings;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ZipMaintenance",
                Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            archive = Path.Combine(root, "game.zip");
            using (var stream = new FileStream(archive, FileMode.CreateNew, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(zip, "Game.exe", "game binary fixture");
                WriteEntry(zip, "readme.txt", "game readme fixture");
            }
            receipts = new CacheOwnershipReceiptStore(Path.Combine(root, "receipts"));
            approvals = new ZipLauncherApprovalService(Path.Combine(root, "approvals"));
            maintenance = new ZipManagedMaintenanceService();
            item = new PersonalCloudLibraryItem
            {
                Id = "game", Title = "Game", SourceType = "file",
                SourcePath = archive, CachePath = "cached/game.zip"
            };
            settings = new PersonalCloudLibrarySourceSettings
            {
                Enabled = true, AllowDownloads = true,
                LocalCacheFolder = cache,
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                UninstallBehavior = PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private static void WriteEntry(ZipArchive zip, string name, string value)
        {
            using (var stream = zip.CreateEntry(name).Open())
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private void Extract()
        {
            var service = new ZipManagedExtractionService();
            var review = new ZipPackageReviewService().Inspect(archive, "Game");
            Assert.That(review.PassedPreflight, Is.True, review.Reason);
            extracted = service.Prepare(review, archive, cache, item.Id, receipts);
            Assert.That(extracted, Is.Not.Null);
            var result = service.ExecuteApproved(extracted, true,
                receipts, CancellationToken.None);
            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(result.ReceiptRecorded, Is.True, result.Reason);
        }

        [Test]
        public void NoExtractionHasNoFootprintAndGrantsNoDelete()
        {
            Assert.That(maintenance.HasManagedFootprint(cache, item.Id, receipts),
                Is.False);
            var report = maintenance.Assess(cache, item.Id, receipts, approvals);
            Assert.That(report.Status, Is.EqualTo(
                ZipManagedMaintenanceStatus.NoExtraction));
            Assert.That(report.CanDeleteUnattended, Is.False);
            Assert.That(report.CanRepairUnattended, Is.False);
            Assert.That(report.CanAuthorizeLegacyUninstall, Is.False);
        }

        [Test]
        public void CleanExtractedInventoryIsDiagnosticOnlyNotDeletePermission()
        {
            Extract();
            Assert.That(maintenance.HasManagedFootprint(cache, item.Id, receipts),
                Is.True);
            var report = maintenance.Assess(cache, item.Id, receipts, approvals);
            Assert.That(report.Status, Is.EqualTo(
                ZipManagedMaintenanceStatus.CleanNeedsManualRemoval), report.Reason);
            Assert.That(report.HasVerifiedInventory, Is.True);
            Assert.That(report.RecordedFileCount, Is.EqualTo(2));
            Assert.That(report.RecordedDirectoryCount, Is.EqualTo(0));
            Assert.That(report.Destination, Is.EqualTo(extracted.Destination));
            Assert.That(report.CanDeleteUnattended, Is.False);
            Assert.That(report.CanRepairUnattended, Is.False);
            Assert.That(File.Exists(archive), Is.True);
            Assert.That(File.Exists(Path.Combine(extracted.Destination, "Game.exe")),
                Is.True);
        }

        [Test]
        public void ActiveLauncherApprovalRequiresRevocationBeforeMaintenance()
        {
            Extract();
            string reason;
            Assert.That(approvals.TryApprove(item, settings,
                "Game.exe", receipts, true, CancellationToken.None, out reason),
                Is.True, reason);
            var report = maintenance.Assess(cache, item.Id, receipts, approvals);
            Assert.That(report.Status, Is.EqualTo(
                ZipManagedMaintenanceStatus.ActiveLauncherApproval), report.Reason);
            Assert.That(report.HasVerifiedInventory, Is.True);
            Assert.That(report.CanDeleteUnattended, Is.False);
            Assert.That(approvals.TryRevoke(item.Id, cache, true, out reason),
                Is.True, reason);
            Assert.That(maintenance.Assess(cache, item.Id, receipts, approvals).Status,
                Is.EqualTo(ZipManagedMaintenanceStatus.CleanNeedsManualRemoval));
        }

        [Test]
        public void ModifiedExecutableBlocksRepairAndRemovalWithoutWriting()
        {
            Extract();
            File.WriteAllText(Path.Combine(extracted.Destination, "Game.exe"),
                "user modified executable");
            var report = maintenance.Assess(cache, item.Id, receipts, approvals);
            Assert.That(report.Status, Is.EqualTo(
                ZipManagedMaintenanceStatus.ModifiedFiles), report.Reason);
            Assert.That(report.HasVerifiedInventory, Is.False);
            Assert.That(report.CanDeleteUnattended, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(extracted.Destination, "Game.exe")),
                Is.EqualTo("user modified executable"));
        }

        [Test]
        public void AddedSaveFileIsPreservedAndBlocksCleanup()
        {
            Extract();
            var save = Path.Combine(extracted.Destination, "important.sav");
            File.WriteAllText(save, "preserve game progress");
            var report = maintenance.Assess(cache, item.Id, receipts, approvals);
            Assert.That(report.Status, Is.EqualTo(
                ZipManagedMaintenanceStatus.ModifiedFiles));
            Assert.That(File.ReadAllText(save), Is.EqualTo("preserve game progress"));
            Assert.That(report.CanDeleteUnattended, Is.False);
        }

        [Test]
        public void DeletedOriginalFileBlocksCleanAssessment()
        {
            Extract();
            File.Delete(Path.Combine(extracted.Destination, "readme.txt"));
            var report = maintenance.Assess(cache, item.Id, receipts, approvals);
            Assert.That(report.Status, Is.EqualTo(
                ZipManagedMaintenanceStatus.ModifiedFiles));
            Assert.That(report.CanDeleteUnattended, Is.False);
        }

        [Test]
        public void ExistingDirectoryWithoutReceiptIsNotAdopted()
        {
            var destination = Path.Combine(cache,
                ZipManagedExtractionService.ReceiptKeyForGame(item.Id));
            Directory.CreateDirectory(destination);
            var save = Path.Combine(destination, "user-file.sav");
            File.WriteAllText(save, "valuable");
            Assert.That(maintenance.HasManagedFootprint(cache, item.Id, receipts),
                Is.True);
            var report = maintenance.Assess(cache, item.Id, receipts, approvals);
            Assert.That(report.Status, Is.EqualTo(
                ZipManagedMaintenanceStatus.UntrackedContent));
            Assert.That(report.CanDeleteUnattended, Is.False);
            Assert.That(File.ReadAllText(save), Is.EqualTo("valuable"));
        }

        [Test]
        public void MissingDirectoryWithReceiptIsStillManagedAndBlocked()
        {
            Extract();
            Directory.Move(extracted.Destination, Path.Combine(root, "moved"));
            Assert.That(maintenance.HasManagedFootprint(cache, item.Id, receipts),
                Is.True);
            var report = maintenance.Assess(cache, item.Id, receipts, approvals);
            Assert.That(report.Status, Is.EqualTo(
                ZipManagedMaintenanceStatus.MissingExtractedFiles));
            Assert.That(report.CanDeleteUnattended, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(root, "moved", "Game.exe")),
                Is.EqualTo("game binary fixture"));
        }

        [Test]
        public void WrongIdentityNeverAdoptsAnotherGamesExtraction()
        {
            Extract();
            Assert.That(maintenance.HasManagedFootprint(cache,
                "different-game", receipts), Is.False);
            var report = maintenance.Assess(cache, "different-game", receipts, approvals);
            Assert.That(report.Status, Is.EqualTo(
                ZipManagedMaintenanceStatus.NoExtraction));
            Assert.That(report.CanDeleteUnattended, Is.False);
        }

        [Test]
        public void CancellationDoesNotAlterGameOrReceipt()
        {
            Extract();
            using (var stop = new CancellationTokenSource())
            {
                stop.Cancel();
                var report = maintenance.Assess(cache, item.Id,
                    receipts, approvals, stop.Token);
                Assert.That(report.Status, Is.EqualTo(
                    ZipManagedMaintenanceStatus.InvalidRequest));
                Assert.That(report.HasVerifiedInventory, Is.False);
            }
            CacheOwnershipReceipt record;
            Assert.That(receipts.TryRead(cache, extracted.ReceiptKey, out record),
                Is.True);
            Assert.That(File.Exists(Path.Combine(extracted.Destination, "Game.exe")),
                Is.True);
        }

        [Test]
        public void CorruptReceiptAndMissingDirectoryStillBlockLegacyUninstall()
        {
            Extract();
            var record = Directory.GetFiles(Path.Combine(root, "receipts"),
                "*.json").Single();
            var moved = Path.Combine(root, "moved-files");
            Directory.Move(extracted.Destination, moved);
            File.WriteAllText(record, "{ not valid json");
            Assert.That(maintenance.HasManagedFootprint(cache, item.Id, receipts),
                Is.True, "Invalid receipt presence must remain a conservative uninstall guard.");
            var report = maintenance.Assess(cache, item.Id, receipts, approvals);
            Assert.That(report.Status, Is.EqualTo(
                ZipManagedMaintenanceStatus.UnsafeExtraction));
            Assert.That(report.CanDeleteUnattended, Is.False);
            Assert.That(File.ReadAllText(record), Is.EqualTo("{ not valid json"));
            Assert.That(File.Exists(Path.Combine(moved, "Game.exe")), Is.True);

            var cachedFile = Path.Combine(cache, "cached", "game.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(cachedFile));
            File.WriteAllText(cachedFile, "preserve cached archive");
            var game = new Game("Game") { GameId = item.Id };
            var controller = new PersonalCloudLibraryUninstallController(
                null, game, item, settings, ownershipReceipts: receipts);
            Assert.Throws<InvalidOperationException>(
                () => controller.Uninstall(new UninstallActionArgs()));
            Assert.That(File.ReadAllText(cachedFile), Is.EqualTo("preserve cached archive"));
        }

        [Test]
        public void LegacyPlayniteUninstallControllerRefusesManagedZip()
        {
            Extract();
            var cachedFile = Path.Combine(cache, "cached", "game.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(cachedFile));
            File.WriteAllText(cachedFile, "separately cached original");
            var game = new Game("Game") { GameId = item.Id };
            var controller = new PersonalCloudLibraryUninstallController(
                null, game, item, settings, ownershipReceipts: receipts);
            var failure = Assert.Throws<InvalidOperationException>(
                () => controller.Uninstall(new UninstallActionArgs()));
            Assert.That(failure.Message, Does.Contain("Managed ZIP extraction"));
            Assert.That(File.ReadAllText(cachedFile),
                Is.EqualTo("separately cached original"));
            Assert.That(File.Exists(Path.Combine(extracted.Destination, "Game.exe")),
                Is.True);
            CacheOwnershipReceipt record;
            Assert.That(receipts.TryRead(cache, extracted.ReceiptKey, out record),
                Is.True);
        }
    }
}
