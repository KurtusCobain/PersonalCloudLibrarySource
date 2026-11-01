using System;
using System.IO;

namespace PersonalCloudLibrarySource
{
    // Classification only. This must not authorize deletion until persistent
    // receipt writes, migration and host lifecycle are independently tested.
    public enum CacheOwnershipKind
    {
        Missing,
        LegacyUnverified,
        VerifiedReceipt,
        ReceiptMismatch
    }

    public sealed class CacheOwnershipAssessment
    {
        public CacheOwnershipKind Kind { get; set; }
        // Receipt/path matching alone never authorizes deleting user content.
        // Requires separate verified file inventory and deletion transaction.
        public bool MayDeleteAutomatically => false;
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class CacheOwnershipPolicy
    {
        // A receipt is evidence of PCLS's own completed transfer, not a claim
        // that any arbitrary folder under LocalCacheFolder belongs to PCLS.
        // Receipt storage and issuance deliberately remain outside this class.
        public CacheOwnershipAssessment Assess(
            string cacheRoot, string expectedDestination, string receiptDestination,
            string receiptItemId, string expectedItemId)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot) ||
                string.IsNullOrWhiteSpace(expectedDestination) ||
                string.IsNullOrWhiteSpace(expectedItemId) ||
                !PathBoundary.IsContained(cacheRoot, expectedDestination))
                return Result(CacheOwnershipKind.ReceiptMismatch, "Invalid cache ownership scope.");

            try
            {
                if (string.Equals(PathBoundary.Normalize(cacheRoot),
                    PathBoundary.Normalize(expectedDestination), StringComparison.OrdinalIgnoreCase))
                    return Result(CacheOwnershipKind.ReceiptMismatch, "Cache root cannot be owned by one item.");
            }
            catch (Exception)
            {
                return Result(CacheOwnershipKind.ReceiptMismatch, "Invalid cache ownership path.");
            }

            if (!File.Exists(expectedDestination) && !Directory.Exists(expectedDestination))
                return Result(CacheOwnershipKind.Missing, "Cache destination is absent.");

            if (string.IsNullOrWhiteSpace(receiptDestination) || string.IsNullOrWhiteSpace(receiptItemId))
                return Result(CacheOwnershipKind.LegacyUnverified,
                    "Existing cache has no verified PCLS installation receipt.");

            try
            {
                if (!string.Equals(PathBoundary.Normalize(expectedDestination),
                        PathBoundary.Normalize(receiptDestination), StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(expectedItemId, receiptItemId, StringComparison.Ordinal))
                    return Result(CacheOwnershipKind.ReceiptMismatch,
                        "Installation receipt does not match this item and destination.");
                return Result(CacheOwnershipKind.VerifiedReceipt, "Installation receipt matches.");
            }
            catch (Exception)
            {
                return Result(CacheOwnershipKind.ReceiptMismatch, "Installation receipt path is invalid.");
            }
        }

        private static CacheOwnershipAssessment Result(CacheOwnershipKind kind, string reason)
            => new CacheOwnershipAssessment { Kind = kind, Reason = reason };
    }
}
