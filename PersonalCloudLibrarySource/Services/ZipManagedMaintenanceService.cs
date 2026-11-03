using System;
using System.IO;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public enum ZipManagedMaintenanceStatus
    {
        InvalidRequest,
        NoExtraction,
        UntrackedContent,
        MissingExtractedFiles,
        UnsafeExtraction,
        ModifiedFiles,
        ActiveLauncherApproval,
        CleanNeedsManualRemoval
    }

    // A read-only diagnostic. "Clean" never grants delete permission.
    public sealed class ZipManagedMaintenanceAssessment
    {
        public ZipManagedMaintenanceStatus Status { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
        public string Destination { get; internal set; } = string.Empty;
        public int RecordedFileCount { get; internal set; }
        public int RecordedDirectoryCount { get; internal set; }
        public bool HasVerifiedInventory { get; internal set; }
        public bool CanDeleteUnattended => false;
        public bool CanRepairUnattended => false;
        public bool CanAuthorizeLegacyUninstall => false;
    }

    // Deliberately NOT a deletion controller. The ordinary manifest uninstall
    // cannot adopt the managed extracted ZIP tree, even if there is a receipt.
    public sealed class ZipManagedMaintenanceService
    {
        public bool HasManagedFootprint(string cacheRoot, string gameId,
            CacheOwnershipReceiptStore receipts)
        {
            try
            {
                string destination, receiptId;
                if (!TryLocation(cacheRoot, gameId, out destination, out receiptId))
                    return false;
                return Directory.Exists(destination) || File.Exists(destination) ||
                    (receipts != null &&
                        receipts.HasReceiptFootprint(cacheRoot, receiptId));
            }
            catch (Exception)
            {
                // A malformed cache location alone is not evidence of a ZIP
                // extraction. Explicitly fail closed in Assess instead.
                return false;
            }
        }

        public ZipManagedMaintenanceAssessment Assess(string cacheRoot,
            string gameId, CacheOwnershipReceiptStore receipts,
            ZipLauncherApprovalService approvals,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = new ZipManagedMaintenanceAssessment
            {
                Status = ZipManagedMaintenanceStatus.InvalidRequest,
                Reason = "A valid managed ZIP cache identity is required."
            };
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string destination, receiptId;
                if (!TryLocation(cacheRoot, gameId, out destination, out receiptId) ||
                    receipts == null)
                    return result;
                result.Destination = destination;

                var safe = new SafeCacheDeletionPolicy()
                    .Authorize(cacheRoot, destination, false);
                if (!safe.Allowed)
                {
                    result.Status = ZipManagedMaintenanceStatus.UnsafeExtraction;
                    result.Reason = "The managed ZIP destination is linked or otherwise unsafe.";
                    return result;
                }

                CacheOwnershipReceipt record;
                var hasReceipt = receipts.TryRead(cacheRoot, receiptId, out record);
                if (!hasReceipt)
                {
                    // A stored but unreadable receipt must not become "not
                    // managed" when its directory is also missing.
                    if (receipts.HasReceiptFootprint(cacheRoot, receiptId))
                    {
                        result.Status = ZipManagedMaintenanceStatus.UnsafeExtraction;
                        result.Reason = "An unreadable or corrupt managed ZIP receipt exists; preserve files and review manually.";
                        return result;
                    }
                    result.Status = Directory.Exists(destination) ||
                                    File.Exists(destination)
                        ? ZipManagedMaintenanceStatus.UntrackedContent
                        : ZipManagedMaintenanceStatus.NoExtraction;
                    result.Reason = result.Status == ZipManagedMaintenanceStatus.UntrackedContent
                        ? "Untracked files occupy the managed ZIP destination; preserve them."
                        : "No managed ZIP extraction was found.";
                    return result;
                }
                if (record.Version != 2 ||
                    !string.Equals(PathBoundary.Normalize(record.Destination),
                        PathBoundary.Normalize(destination),
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.Status = ZipManagedMaintenanceStatus.UnsafeExtraction;
                    result.Reason = "The extraction receipt does not match the expected folder.";
                    return result;
                }

                result.RecordedFileCount = record.InventoryFileCount;
                result.RecordedDirectoryCount = record.InventoryDirectoryCount;
                if (!Directory.Exists(destination))
                {
                    result.Status = ZipManagedMaintenanceStatus.MissingExtractedFiles;
                    result.Reason = "An extraction receipt exists but its directory is missing.";
                    return result;
                }
                cancellationToken.ThrowIfCancellationRequested();

                bool matched;
                if (!receipts.TryCompareInventory(cacheRoot, receiptId,
                    destination, out matched) || !matched)
                {
                    result.Status = ZipManagedMaintenanceStatus.ModifiedFiles;
                    result.Reason = "The extracted inventory differs from its recorded state. " +
                        "Saves, mods, or modified game files may be present; do not remove it automatically.";
                    return result;
                }
                result.HasVerifiedInventory = true;
                cancellationToken.ThrowIfCancellationRequested();

                if (approvals != null && approvals.HasApproval(gameId, cacheRoot))
                {
                    result.Status = ZipManagedMaintenanceStatus.ActiveLauncherApproval;
                    result.Reason = "A launcher approval still exists. Revoke it and refresh " +
                        "the Playnite library before considering manual removal.";
                    return result;
                }
                result.Status = ZipManagedMaintenanceStatus.CleanNeedsManualRemoval;
                result.Reason = "Extracted files match the recorded inventory and there is no " +
                    "launcher approval. Automatic recursive removal remains disabled.";
                return result;
            }
            catch (OperationCanceledException)
            {
                result.Status = ZipManagedMaintenanceStatus.InvalidRequest;
                result.HasVerifiedInventory = false;
                result.Reason = "Maintenance review was cancelled.";
                return result;
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is ArgumentException || ex is NotSupportedException)
            {
                result.Status = ZipManagedMaintenanceStatus.UnsafeExtraction;
                result.HasVerifiedInventory = false;
                result.Reason = "The extraction state could not be verified safely.";
                return result;
            }
        }

        private static bool TryLocation(string cacheRoot, string gameId,
            out string destination, out string receiptId)
        {
            destination = string.Empty;
            receiptId = ZipManagedExtractionService.ReceiptKeyForGame(gameId);
            if (string.IsNullOrWhiteSpace(receiptId) ||
                string.IsNullOrWhiteSpace(cacheRoot))
                return false;
            var root = PathBoundary.Normalize(cacheRoot);
            if (!Directory.Exists(root))
                return false;
            var child = Path.GetFullPath(Path.Combine(root, receiptId));
            if (!PathBoundary.IsContained(root, child) ||
                string.Equals(root, child, StringComparison.OrdinalIgnoreCase))
                return false;
            destination = child;
            return true;
        }
    }
}
