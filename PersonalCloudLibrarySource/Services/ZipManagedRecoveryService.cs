using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PersonalCloudLibrarySource
{
    public enum ZipRecoveryState
    {
        NoJournal,
        Prepared,
        Quarantined,
        Restored,
        CleanupPending,
        Purged,
        Ambiguous
    }

    public sealed class ZipRecoveryAssessment
    {
        public ZipRecoveryState State { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
        public string OriginalDirectory { get; internal set; } = string.Empty;
        public string RecoveryDirectory { get; internal set; } = string.Empty;
        public string TransactionId { get; internal set; } = string.Empty;
        public bool CanDeleteUnattended => false;
    }

    public sealed class ZipRecoveryJournalEntry
    {
        public int Version { get; set; }
        public string GameId { get; set; }
        public string CacheRoot { get; set; }
        public string OriginalDirectory { get; set; }
        public string RecoveryDirectory { get; set; }
        public string InventoryFingerprint { get; set; }
        public string TransactionId { get; set; }
    }

    // Reversible, same-cache-volume relocation only. No recursive deletion,
    // re-extraction, executable launch, receipt adoption, or metadata mutation.
    // Its immutable write-ahead journal survives a crash between preparation
    // and Directory.Move, and every recovery action requires explicit consent.
    public sealed class ZipManagedRecoveryService
    {
        private const int MaxJournalBytes = 8192;
        private readonly string storeRoot;

        public ZipManagedRecoveryService(string storeRoot)
        {
            if (string.IsNullOrWhiteSpace(storeRoot) || !Path.IsPathRooted(storeRoot))
                throw new ArgumentException("Recovery journal store must be rooted.");
            this.storeRoot = Path.GetFullPath(storeRoot);
        }

        public bool HasJournalFootprint(string cacheRoot, string gameId)
        {
            try
            {
                string root, original, recovery, journal;
                if (!TryLocations(cacheRoot, gameId, out root, out original,
                    out recovery, out journal)) return false;
                return File.Exists(journal) || Directory.Exists(journal);
            }
            catch (Exception) { return false; }
        }

        public ZipRecoveryAssessment Inspect(string cacheRoot, string gameId)
        {
            var result = new ZipRecoveryAssessment
            {
                State = ZipRecoveryState.Ambiguous,
                Reason = "Managed ZIP recovery identity or journal is unsafe."
            };
            try
            {
                string root, original, recovery, journal;
                if (!TryLocations(cacheRoot, gameId, out root, out original,
                    out recovery, out journal)) return result;
                result.OriginalDirectory = original;
                result.RecoveryDirectory = recovery;
                var recordExists = File.Exists(journal) || Directory.Exists(journal);
                var recoveryExists = File.Exists(recovery) || Directory.Exists(recovery);
                if (!recordExists)
                {
                    result.State = recoveryExists
                        ? ZipRecoveryState.Ambiguous : ZipRecoveryState.NoJournal;
                    result.Reason = recoveryExists
                        ? "A recovery folder exists without a valid journal; preserve it."
                        : "No recovery transaction was recorded.";
                    return result;
                }
                ZipRecoveryJournalEntry entry;
                if (!TryReadJournal(cacheRoot, gameId, out entry))
                    return result;
                result.TransactionId = entry.TransactionId;
                var purgeFootprint = journal + ".purge.json";
                var purgedMarker = journal + ".purged";
                if (File.Exists(purgedMarker) || Directory.Exists(purgedMarker))
                {
                    if (!TryReadRestoredMarker(purgedMarker, entry.TransactionId) ||
                        File.Exists(original) || Directory.Exists(original) ||
                        File.Exists(recovery) || Directory.Exists(recovery))
                    {
                        result.Reason = "Permanent cleanup completion marker conflicts with folder state.";
                        return result;
                    }
                    result.State = ZipRecoveryState.Purged;
                    result.Reason = "All recorded managed ZIP files were permanently removed and cleanup finalized.";
                    return result;
                }
                if (File.Exists(purgeFootprint) || Directory.Exists(purgeFootprint))
                {
                    if (File.Exists(original) || Directory.Exists(original) ||
                        File.Exists(recovery) ||
                        File.Exists(journal + ".restored") ||
                        Directory.Exists(journal + ".restored"))
                    {
                        result.Reason = "Cleanup journal conflicts with an original or unexpected recovery location. Preserve both paths.";
                        return result;
                    }
                    result.State = ZipRecoveryState.CleanupPending;
                    result.Reason = "Permanent cleanup has a durable progress manifest; use the cleanup action to resume or inspect it.";
                    return result;
                }
                var marker = journal + ".restored";
                var markerExists = File.Exists(marker) || Directory.Exists(marker);
                if (markerExists && !TryReadRestoredMarker(marker, entry.TransactionId))
                {
                    result.Reason = "Recovery finalization marker is invalid; preserve both folders.";
                    return result;
                }
                var originalExists = File.Exists(original) || Directory.Exists(original);
                if (File.Exists(original) || File.Exists(recovery) ||
                    (originalExists && recoveryExists))
                {
                    result.Reason = "Recovery locations conflict or contain unexpected files.";
                    return result;
                }
                if (Directory.Exists(recovery))
                {
                    if (markerExists)
                    {
                        result.Reason = "Restored marker conflicts with a quarantined folder.";
                        return result;
                    }
                    result.State = ZipRecoveryState.Quarantined;
                    result.Reason = "Managed ZIP folder is in recoverable quarantine; no data was deleted.";
                }
                else if (Directory.Exists(original))
                {
                    // A completed restore has its own create-only marker.
                    // Crashes before marker creation remain conservative:
                    // the original folder must pass a new full verification.
                    result.State = markerExists
                        ? ZipRecoveryState.Restored : ZipRecoveryState.Prepared;
                    result.Reason = markerExists
                        ? "Managed ZIP was restored and finalization recorded."
                        : "Original folder exists and recovery folder is absent. An explicit retry may be possible.";
                }
                else
                {
                    result.Reason = "Both managed recovery locations are missing; preserve the journal.";
                }
                return result;
            }
            catch (Exception) { return result; }
        }

        public bool TryQuarantine(string cacheRoot, string gameId,
            CacheOwnershipReceiptStore receipts,
            ZipLauncherApprovalService approvals, bool userConfirmed,
            CancellationToken cancellationToken, out string reason)
        {
            reason = "Explicit confirmation and an unmodified, unapproved managed ZIP are required.";
            if (!userConfirmed || receipts == null || approvals == null)
                return false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string root, original, recovery, journal;
                if (!TryLocations(cacheRoot, gameId, out root, out original,
                    out recovery, out journal)) return false;
                if (File.Exists(journal) || Directory.Exists(journal) ||
                    File.Exists(recovery) || Directory.Exists(recovery) ||
                    !Directory.Exists(original) ||
                    approvals.HasApprovalFootprint(gameId, cacheRoot))
                    return false;

                var report = new ZipManagedMaintenanceService().Assess(
                    cacheRoot, gameId, receipts, approvals, cancellationToken);
                if (report.Status != ZipManagedMaintenanceStatus.CleanNeedsManualRemoval ||
                    !report.HasVerifiedInventory)
                    return false;
                CacheOwnershipReceipt receipt;
                var key = ZipManagedExtractionService.ReceiptKeyForGame(gameId);
                if (!receipts.TryRead(cacheRoot, key, out receipt) ||
                    receipt.Version != 2 ||
                    !string.Equals(PathBoundary.Normalize(receipt.Destination),
                        original, StringComparison.OrdinalIgnoreCase))
                    return false;
                cancellationToken.ThrowIfCancellationRequested();

                // Re-check before committing a write-ahead record. A folder
                // that changes later will still be MOVED, never deleted.
                if (!SafeFolder(cacheRoot, original) ||
                    File.Exists(recovery) || Directory.Exists(recovery) ||
                    approvals.HasApprovalFootprint(gameId, cacheRoot))
                    return false;
                var entry = new ZipRecoveryJournalEntry
                {
                    Version = 1, GameId = gameId, CacheRoot = root,
                    OriginalDirectory = original, RecoveryDirectory = recovery,
                    InventoryFingerprint = receipt.InventoryFingerprint,
                    TransactionId = Guid.NewGuid().ToString("N")
                };
                if (!TryCreateJournal(journal, entry))
                {
                    reason = "Recovery journal could not be recorded; no folder was moved.";
                    return false;
                }

                // Crash after journal and before rename is recognizable as
                // Prepared. The move is on the same filesystem, is reversible,
                // and refuses replacing an occupied destination.
                cancellationToken.ThrowIfCancellationRequested();
                if (!SafeFolder(cacheRoot, original) ||
                    File.Exists(recovery) || Directory.Exists(recovery) ||
                    approvals.HasApprovalFootprint(gameId, cacheRoot))
                {
                    reason = "Journal saved but folder conditions changed. Review recovery state before retrying.";
                    return false;
                }
                Directory.Move(original, recovery);
                reason = "Managed ZIP moved to recovery quarantine without deleting files. Update Game Library; use Restore Managed ZIP Recovery to undo.";
                return true;
            }
            catch (OperationCanceledException)
            {
                reason = "Recovery operation cancelled. Inspect the journal state; existing files were not deleted.";
                return false;
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is ArgumentException || ex is NotSupportedException)
            {
                reason = "Quarantine could not complete. Inspect the journal and both folders; no recursive deletion was attempted.";
                return false;
            }
        }

        public bool TryResumePrepared(string cacheRoot, string gameId,
            CacheOwnershipReceiptStore receipts, ZipLauncherApprovalService approvals,
            bool userConfirmed, CancellationToken cancellationToken, out string reason)
        {
            reason = "An explicit resume choice and valid prepared journal are required.";
            if (!userConfirmed || receipts == null || approvals == null)
                return false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string root, original, recovery, journal;
                if (!TryLocations(cacheRoot, gameId, out root, out original,
                    out recovery, out journal)) return false;
                ZipRecoveryJournalEntry entry;
                if (!TryReadJournal(cacheRoot, gameId, out entry) ||
                    File.Exists(journal + ".purge.json") ||
                    Directory.Exists(journal + ".purge.json") ||
                    File.Exists(journal + ".purged") ||
                    File.Exists(journal + ".restored") ||
                    Directory.Exists(journal + ".restored") ||
                    !Directory.Exists(original) || File.Exists(recovery) ||
                    Directory.Exists(recovery) ||
                    approvals.HasApprovalFootprint(gameId, cacheRoot))
                    return false;
                CacheOwnershipReceipt receipt;
                if (!MatchingReceipt(cacheRoot, gameId, receipts, entry,
                    out receipt) || !SafeFolder(cacheRoot, original))
                    return false;
                bool matches;
                if (!receipts.TryCompareInventory(cacheRoot,
                    ZipManagedExtractionService.ReceiptKeyForGame(gameId),
                    original, out matches, cancellationToken) || !matches)
                    return false;
                cancellationToken.ThrowIfCancellationRequested();
                if (!SafeFolder(cacheRoot, original) ||
                    File.Exists(recovery) || Directory.Exists(recovery) ||
                    approvals.HasApprovalFootprint(gameId, cacheRoot))
                    return false;
                Directory.Move(original, recovery);
                reason = "Prepared recovery move completed; original files preserved in quarantine.";
                return true;
            }
            catch (OperationCanceledException)
            {
                reason = "Recovery resume cancelled; no files were deleted.";
                return false;
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is ArgumentException || ex is NotSupportedException)
            {
                reason = "Prepared recovery move could not be safely completed.";
                return false;
            }
        }

        public bool TryRestore(string cacheRoot, string gameId,
            CacheOwnershipReceiptStore receipts, ZipLauncherApprovalService approvals,
            bool userConfirmed, CancellationToken cancellationToken, out string reason)
        {
            reason = "Explicit consent and a uniquely identified recovery folder are required.";
            if (!userConfirmed || receipts == null || approvals == null)
                return false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string root, original, recovery, journal;
                if (!TryLocations(cacheRoot, gameId, out root, out original,
                    out recovery, out journal)) return false;
                ZipRecoveryJournalEntry entry;
                if (!TryReadJournal(cacheRoot, gameId, out entry) ||
                    File.Exists(journal + ".restored") ||
                    Directory.Exists(journal + ".restored") ||
                    File.Exists(original) || Directory.Exists(original) ||
                    !Directory.Exists(recovery) ||
                    approvals.HasApprovalFootprint(gameId, cacheRoot))
                    return false;
                CacheOwnershipReceipt receipt;
                if (!MatchingReceipt(cacheRoot, gameId, receipts, entry,
                    out receipt) || !SafeFolder(cacheRoot, recovery))
                    return false;

                // Do not adopt added/modified user content as owned. A strict
                // match is required for automated recovery. Nonmatching
                // content must be preserved for manual recovery.
                var snapshot = new CacheTreeInventoryScanner().Scan(
                    cacheRoot, recovery, cancellationToken);
                if (snapshot.FileCount != receipt.InventoryFileCount ||
                    snapshot.DirectoryCount != receipt.InventoryDirectoryCount ||
                    !string.Equals(snapshot.Fingerprint,
                        receipt.InventoryFingerprint, StringComparison.Ordinal))
                    return false;
                cancellationToken.ThrowIfCancellationRequested();
                if (!SafeFolder(cacheRoot, recovery) ||
                    File.Exists(original) || Directory.Exists(original) ||
                    approvals.HasApprovalFootprint(gameId, cacheRoot))
                    return false;
                Directory.Move(recovery, original);
                var finalized = TryWriteRestoredMarker(
                    journal + ".restored", entry.TransactionId);
                reason = finalized
                    ? "Managed ZIP restored and recorded. No files were deleted or launcher approval created."
                    : "Managed ZIP files were restored safely, but its finalization marker could not be saved. Review recovery state manually.";
                return true;
            }
            catch (OperationCanceledException)
            {
                reason = "Recovery restore cancelled; files were preserved.";
                return false;
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is ArgumentException || ex is NotSupportedException)
            {
                reason = "Recovery restore could not be completed safely. Preserve the journal and files.";
                return false;
            }
        }

        private bool MatchingReceipt(string cacheRoot, string gameId,
            CacheOwnershipReceiptStore receipts, ZipRecoveryJournalEntry journal,
            out CacheOwnershipReceipt receipt)
        {
            receipt = null;
            var key = ZipManagedExtractionService.ReceiptKeyForGame(gameId);
            return receipts.TryRead(cacheRoot, key, out receipt) &&
                receipt.Version == 2 &&
                string.Equals(PathBoundary.Normalize(receipt.Destination),
                    journal.OriginalDirectory, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(receipt.InventoryFingerprint,
                    journal.InventoryFingerprint, StringComparison.Ordinal);
        }

        private static bool SafeFolder(string root, string directory)
        {
            return Directory.Exists(directory) &&
                new SafeCacheDeletionPolicy().Authorize(root, directory, false).Allowed &&
                string.IsNullOrEmpty(
                    SafeCacheDeletionExecutor.GetUnsafeDescendantReason(directory));
        }

        private bool TryReadRestoredMarker(string path, string transactionId)
        {
            if (!File.Exists(path) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return false;
            var info = new FileInfo(path);
            return info.Length > 0 && info.Length <= 64 &&
                string.Equals(File.ReadAllText(path, Encoding.ASCII),
                    transactionId, StringComparison.Ordinal);
        }

        private bool TryWriteRestoredMarker(string path, string transactionId)
        {
            try
            {
                if (File.Exists(path) || Directory.Exists(path))
                    return false;
                var temp = Path.Combine(storeRoot,
                    Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var output = new FileStream(temp,
                        FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(output, Encoding.ASCII))
                        writer.Write(transactionId);
                    if (File.Exists(path) || Directory.Exists(path))
                        return false;
                    File.Move(temp, path);
                    return true;
                }
                finally
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is ArgumentException ||
                ex is NotSupportedException)
            {
                return false;
            }
        }

        private bool TryCreateJournal(string journal, ZipRecoveryJournalEntry entry)
        {
            if (File.Exists(journal) || Directory.Exists(journal)) return false;
            var serialized = new JavaScriptSerializer
            {
                MaxJsonLength = MaxJournalBytes
            }.Serialize(entry);
            if (Encoding.UTF8.GetByteCount(serialized) > MaxJournalBytes)
                return false;
            Directory.CreateDirectory(storeRoot);
            var temp = Path.Combine(storeRoot,
                Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var output = new FileStream(temp, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(output, new UTF8Encoding(false)))
                    writer.Write(serialized);
                if (File.Exists(journal) || Directory.Exists(journal))
                    return false;
                File.Move(temp, journal);
                return true;
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private bool TryReadJournal(string cacheRoot, string gameId,
            out ZipRecoveryJournalEntry entry)
        {
            entry = null;
            string root, original, recovery, journal;
            if (!TryLocations(cacheRoot, gameId, out root, out original,
                out recovery, out journal) ||
                !File.Exists(journal) ||
                (File.GetAttributes(journal) & FileAttributes.ReparsePoint) != 0)
                return false;
            using (var stream = new FileStream(journal,
                FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > MaxJournalBytes)
                    return false;
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    entry = new JavaScriptSerializer
                    {
                        MaxJsonLength = MaxJournalBytes
                    }.Deserialize<ZipRecoveryJournalEntry>(reader.ReadToEnd());
            }
            return entry != null && entry.Version == 1 &&
                string.Equals(entry.GameId, gameId, StringComparison.Ordinal) &&
                string.Equals(entry.CacheRoot, root, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.OriginalDirectory, original,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.RecoveryDirectory, recovery,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(entry.TransactionId) &&
                entry.TransactionId.Length == 32 &&
                !string.IsNullOrWhiteSpace(entry.InventoryFingerprint) &&
                entry.InventoryFingerprint.Length == 64;
        }

        private bool TryLocations(string cacheRoot, string gameId,
            out string root, out string original, out string recovery,
            out string journal)
        {
            root = original = recovery = journal = string.Empty;
            var key = ZipManagedExtractionService.ReceiptKeyForGame(gameId);
            if (string.IsNullOrWhiteSpace(key) ||
                string.IsNullOrWhiteSpace(cacheRoot))
                return false;
            root = PathBoundary.Normalize(cacheRoot);
            if (!Directory.Exists(root) ||
                PathBoundary.IsContained(root, storeRoot) ||
                PathBoundary.IsContained(storeRoot, root))
                return false;
            original = Path.Combine(root, key);
            recovery = Path.Combine(root,
                "pcls-recovery-" + key.Substring("pcls-zip-".Length));
            if (!new SafeCacheDeletionPolicy().Authorize(root, original, false).Allowed ||
                !new SafeCacheDeletionPolicy().Authorize(root, recovery, false).Allowed)
                return false;
            using (var sha = SHA256.Create())
            {
                var id = root.ToUpperInvariant() + "\n" + gameId.ToUpperInvariant();
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(id));
                journal = Path.Combine(storeRoot,
                    BitConverter.ToString(hash).Replace("-", "") + ".json");
            }
            return true;
        }
    }
}
