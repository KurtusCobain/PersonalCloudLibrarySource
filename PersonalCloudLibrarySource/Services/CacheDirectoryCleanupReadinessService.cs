using System;
using System.IO;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    // Diagnostic-only preview for a future folder cleanup design.
    // A matching V2 receipt is neither authenticated nor deletion authorization.
    public enum CacheDirectoryCleanupReadinessKind
    {
        UnsafeScope,
        MissingDirectory,
        UnverifiedReceipt,
        LegacyReceipt,
        ReceiptMismatch,
        InventoryChanged,
        InventoryMatchesButUntrusted,
        InventoryUnavailable,
        Cancelled
    }

    public sealed class CacheDirectoryCleanupReadiness
    {
        public CacheDirectoryCleanupReadinessKind Kind { get; set; }
        public string Reason { get; set; } = string.Empty;

        // Deliberate invariant: no caller can interpret this read-only result
        // as permission to recursively delete any file or directory.
        public bool CanDeleteAutomatically => false;
    }

    public sealed class CacheDirectoryCleanupReadinessService
    {
        private readonly CacheOwnershipReceiptStore receipts;

        public CacheDirectoryCleanupReadinessService(CacheOwnershipReceiptStore receipts)
        {
            this.receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
        }

        public CacheDirectoryCleanupReadiness Inspect(
            string cacheRoot, string itemId, string expectedDirectory,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
                return Result(CacheDirectoryCleanupReadinessKind.Cancelled,
                    "Read-only inventory review was cancelled.");

            if (string.IsNullOrWhiteSpace(cacheRoot) ||
                string.IsNullOrWhiteSpace(itemId) ||
                string.IsNullOrWhiteSpace(expectedDirectory))
                return Result(CacheDirectoryCleanupReadinessKind.UnsafeScope,
                    "Cache root, item ID and expected directory are required.");

            // This policy is used for path validation only. No Delete operation
            // or filesystem mutation is reachable from this service.
            var scope = new SafeCacheDeletionPolicy().Authorize(
                cacheRoot, expectedDirectory, false);
            if (!scope.Allowed)
                return Result(CacheDirectoryCleanupReadinessKind.UnsafeScope,
                    "Directory is outside the safe cache-review scope: " + scope.Reason);

            try
            {
                if (!Directory.Exists(scope.TargetPath))
                    return Result(CacheDirectoryCleanupReadinessKind.MissingDirectory,
                        "The expected cached game directory is missing.");

                CacheOwnershipReceipt receipt;
                if (!receipts.TryRead(cacheRoot, itemId, out receipt))
                    return Result(CacheDirectoryCleanupReadinessKind.UnverifiedReceipt,
                        "No valid ownership receipt exists for this game and cache root.");

                if (!string.Equals(
                    PathBoundary.Normalize(scope.TargetPath),
                    PathBoundary.Normalize(receipt.Destination),
                    StringComparison.OrdinalIgnoreCase))
                    return Result(CacheDirectoryCleanupReadinessKind.ReceiptMismatch,
                        "The receipt refers to a different cache destination.");

                if (receipt.Version != 2)
                    return Result(CacheDirectoryCleanupReadinessKind.LegacyReceipt,
                        "A legacy receipt has no complete directory inventory.");

                var snapshot = new CacheTreeInventoryScanner().Scan(
                    cacheRoot, scope.TargetPath, cancellationToken);
                if (snapshot.FileCount != receipt.InventoryFileCount ||
                    snapshot.DirectoryCount != receipt.InventoryDirectoryCount ||
                    !string.Equals(snapshot.Fingerprint,
                        receipt.InventoryFingerprint, StringComparison.Ordinal))
                    return Result(CacheDirectoryCleanupReadinessKind.InventoryChanged,
                        "Cached folder contents differ from the recorded inventory.");

                return Result(CacheDirectoryCleanupReadinessKind.InventoryMatchesButUntrusted,
                    "The current inventory matches, but the receipt is not authenticated " +
                    "and cannot authorize deletion.");
            }
            catch (OperationCanceledException)
            {
                return Result(CacheDirectoryCleanupReadinessKind.Cancelled,
                    "Read-only inventory review was cancelled.");
            }
            catch (IOException)
            {
                return Result(CacheDirectoryCleanupReadinessKind.InventoryUnavailable,
                    "The cached folder could not be inspected safely.");
            }
            catch (UnauthorizedAccessException)
            {
                return Result(CacheDirectoryCleanupReadinessKind.InventoryUnavailable,
                    "Access to the cached folder was denied during inspection.");
            }
            catch (InvalidOperationException)
            {
                return Result(CacheDirectoryCleanupReadinessKind.InventoryUnavailable,
                    "The cached folder or receipt failed validation.");
            }
        }

        private static CacheDirectoryCleanupReadiness Result(
            CacheDirectoryCleanupReadinessKind kind, string reason)
        {
            return new CacheDirectoryCleanupReadiness { Kind = kind, Reason = reason };
        }
    }
}
