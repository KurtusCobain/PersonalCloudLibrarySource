using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PersonalCloudLibrarySource
{
    // Receipt storage is separate from the user cache tree. Call RecordCompleted
    // only after a transfer has committed a destination that did not pre-exist.
    // This store does NOT itself authorize deletion or adopt legacy content.
    public sealed class CacheOwnershipReceipt
    {
        public int Version { get; set; }
        public string ItemId { get; set; }
        public string CacheRoot { get; set; }
        public string Destination { get; set; }
        public string InventoryFingerprint { get; set; }
        public int InventoryFileCount { get; set; }
        public int InventoryDirectoryCount { get; set; }
    }

    public sealed class CacheOwnershipReceiptStore
    {
        private readonly string storeRoot;
        public CacheOwnershipReceiptStore(string storeRoot)
        {
            if (string.IsNullOrWhiteSpace(storeRoot) || !Path.IsPathRooted(storeRoot))
                throw new ArgumentException("Receipt store must use a rooted path.", nameof(storeRoot));
            this.storeRoot = Path.GetFullPath(storeRoot);
        }

        private string ReceiptFile(string cacheRoot, string itemId)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot) || string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("Cache root and item ID are required.");
            var identity = PathBoundary.Normalize(cacheRoot).ToUpperInvariant() + "\n" + itemId.ToUpperInvariant();
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
                return Path.Combine(storeRoot, BitConverter.ToString(hash).Replace("-", "") + ".json");
            }
        }

        public bool TryRead(string cacheRoot, string itemId, out CacheOwnershipReceipt receipt)
        {
            receipt = null;
            try
            {
                var path = ReceiptFile(cacheRoot, itemId);
                if (!File.Exists(path)) return false;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > 4096) return false;
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        var parsed = new JavaScriptSerializer { MaxJsonLength = 4096 }
                            .Deserialize<CacheOwnershipReceipt>(reader.ReadToEnd());
                        if (parsed == null || (parsed.Version != 1 && parsed.Version != 2) ||
                            !string.Equals(parsed.ItemId, itemId, StringComparison.Ordinal) ||
                            !string.Equals(PathBoundary.Normalize(parsed.CacheRoot),
                                PathBoundary.Normalize(cacheRoot), StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(parsed.Destination) ||
                            !PathBoundary.IsContained(cacheRoot, parsed.Destination) ||
                            string.Equals(PathBoundary.Normalize(parsed.Destination),
                                PathBoundary.Normalize(cacheRoot), StringComparison.OrdinalIgnoreCase) ||
                            (parsed.Version == 2 && !IsValidV2Inventory(parsed)))
                            return false;
                        receipt = parsed;
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsValidV2Inventory(CacheOwnershipReceipt receipt)
        {
            if (receipt.InventoryFileCount < 0 || receipt.InventoryDirectoryCount < 0 ||
                receipt.InventoryFileCount + (long)receipt.InventoryDirectoryCount >
                    CacheTreeInventoryScanner.MaximumEntries ||
                string.IsNullOrWhiteSpace(receipt.InventoryFingerprint) ||
                receipt.InventoryFingerprint.Length != 64)
                return false;
            foreach (var character in receipt.InventoryFingerprint)
            {
                if (!((character >= '0' && character <= '9') ||
                      (character >= 'A' && character <= 'F')))
                    return false;
            }
            return true;
        }

        // Validate a receipt against the destination derived from the current
        // manifest, not a caller-supplied receipt path. This remains informational:
        // a matching receipt does not prove the directory contents are unchanged.
        public CacheOwnershipAssessment AssessCurrentDestination(
            string cacheRoot, string itemId, string expectedDestination)
        {
            var policy = new CacheOwnershipPolicy();
            CacheOwnershipReceipt receipt;
            if (!TryRead(cacheRoot, itemId, out receipt))
                return policy.Assess(cacheRoot, expectedDestination, null, null, itemId);
            return policy.Assess(cacheRoot, expectedDestination,
                receipt.Destination, receipt.ItemId, itemId);
        }

        // Inventory comparison is strictly diagnostic. V1 receipts remain valid
        // for legacy display but never gain a fingerprint by inference.
        public bool TryCompareInventory(string cacheRoot, string itemId,
            string destination, out bool matches)
        {
            matches = false;
            try
            {
                CacheOwnershipReceipt receipt;
                if (!TryRead(cacheRoot, itemId, out receipt) ||
                    receipt.Version != 2 ||
                    string.IsNullOrWhiteSpace(receipt.InventoryFingerprint) ||
                    string.IsNullOrWhiteSpace(destination) ||
                    string.IsNullOrWhiteSpace(receipt.Destination) ||
                    !string.Equals(PathBoundary.Normalize(destination),
                        PathBoundary.Normalize(receipt.Destination), StringComparison.OrdinalIgnoreCase))
                    return false;
                var current = new CacheTreeInventoryScanner().Scan(cacheRoot, destination);
                matches = current.FileCount == receipt.InventoryFileCount &&
                    current.DirectoryCount == receipt.InventoryDirectoryCount &&
                    string.Equals(current.Fingerprint, receipt.InventoryFingerprint,
                        StringComparison.Ordinal);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void RecordCompletedWithInventory(string cacheRoot, string itemId,
            string destination)
        {
            RecordCompletedWithInventory(cacheRoot, itemId, destination, CancellationToken.None);
        }

        public void RecordCompletedWithInventory(string cacheRoot, string itemId,
            string destination, CancellationToken cancellationToken)
        {
            // A cancelled scan must not leave a new ownership record.
            var snapshot = new CacheTreeInventoryScanner().Scan(cacheRoot, destination, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            RecordCompletedCore(cacheRoot, itemId, destination, snapshot, cancellationToken);
        }

        public void RecordCompleted(string cacheRoot, string itemId, string destination)
        {
            RecordCompletedCore(cacheRoot, itemId, destination, null, CancellationToken.None);
        }

        private void RecordCompletedCore(string cacheRoot, string itemId,
            string destination, CacheTreeInventory inventory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (PathBoundary.IsContained(cacheRoot, storeRoot) ||
                PathBoundary.IsContained(storeRoot, cacheRoot))
                throw new InvalidOperationException("Ownership receipts must be stored outside the cache tree.");
            if (string.IsNullOrWhiteSpace(destination) ||
                !PathBoundary.IsContained(cacheRoot, destination) ||
                string.Equals(PathBoundary.Normalize(cacheRoot),
                    PathBoundary.Normalize(destination), StringComparison.OrdinalIgnoreCase) ||
                (!File.Exists(destination) && !Directory.Exists(destination)))
                throw new InvalidOperationException("Completed destination must exist inside the cache root.");

            var receipt = new CacheOwnershipReceipt
            {
                Version = inventory == null ? 1 : 2, ItemId = itemId,
                CacheRoot = PathBoundary.Normalize(cacheRoot),
                Destination = PathBoundary.Normalize(destination),
                InventoryFingerprint = inventory?.Fingerprint,
                InventoryFileCount = inventory?.FileCount ?? 0,
                InventoryDirectoryCount = inventory?.DirectoryCount ?? 0
            };
            var path = ReceiptFile(cacheRoot, itemId);
            Directory.CreateDirectory(storeRoot);
            // CreateNew refuses to overwrite a prior ownership claim. A unique
            // staging name and rename avoid exposing truncated receipt contents.
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    writer.Write(new JavaScriptSerializer().Serialize(receipt));
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(path))
                    throw new IOException("An ownership receipt already exists for this item.");
                File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
