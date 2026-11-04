using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PersonalCloudLibrarySource
{
    // A supplemental path listing bound to the independently recorded V2
    // whole-tree fingerprint. It is NOT an ownership or delete authorization.
    public sealed class ZipMutableDataBaseline
    {
        public int Version { get; set; }
        public string GameId { get; set; }
        public string CacheRoot { get; set; }
        public string Destination { get; set; }
        public string OriginalFingerprint { get; set; }
        public List<string> OriginalEntries { get; set; }
    }

    public sealed class ZipMutableDataBaselineStore
    {
        private const int MaxBaselineBytes = 2 * 1024 * 1024;
        private const int MaxOriginalEntries = 12000;
        private readonly string storeRoot;

        public ZipMutableDataBaselineStore(string storeRoot)
        {
            if (string.IsNullOrWhiteSpace(storeRoot) || !Path.IsPathRooted(storeRoot))
                throw new ArgumentException("ZIP baseline storage must be rooted.");
            this.storeRoot = Path.GetFullPath(storeRoot);
        }

        // Only called after the original V2 receipt has been atomically issued.
        // If capture fails, legacy strict inventory comparison remains in force.
        public bool TryRecord(string cacheRoot, string gameId, string destination,
            CacheOwnershipReceiptStore receipts, CacheTreeInventory original)
        {
            try
            {
                if (!SafeStore(cacheRoot) || receipts == null || original == null ||
                    string.IsNullOrWhiteSpace(
                        ZipManagedExtractionService.ReceiptKeyForGame(gameId)) ||
                    original.EntryPaths == null ||
                    original.EntryPaths.Count == 0 ||
                    original.EntryPaths.Count > MaxOriginalEntries)
                    return false;

                CacheOwnershipReceipt receipt;
                var key = ZipManagedExtractionService.ReceiptKeyForGame(gameId);
                if (!receipts.TryRead(cacheRoot, key, out receipt) ||
                    receipt.Version != 2 ||
                    !string.Equals(PathBoundary.Normalize(destination),
                        PathBoundary.Normalize(receipt.Destination),
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(original.Fingerprint,
                        receipt.InventoryFingerprint, StringComparison.Ordinal) ||
                    original.FileCount != receipt.InventoryFileCount ||
                    original.DirectoryCount != receipt.InventoryDirectoryCount)
                    return false;

                var entries = original.EntryPaths.ToList();
                if (!ValidEntries(entries, original.FileCount,
                    original.DirectoryCount))
                    return false;
                var document = new ZipMutableDataBaseline
                {
                    Version = 1,
                    GameId = gameId,
                    CacheRoot = PathBoundary.Normalize(cacheRoot),
                    Destination = PathBoundary.Normalize(destination),
                    OriginalFingerprint = receipt.InventoryFingerprint,
                    OriginalEntries = entries
                };
                var data = new JavaScriptSerializer
                {
                    MaxJsonLength = MaxBaselineBytes
                }.Serialize(document);
                if (Encoding.UTF8.GetByteCount(data) > MaxBaselineBytes)
                    return false;
                Directory.CreateDirectory(storeRoot);
                var path = RecordPath(cacheRoot, gameId);
                if (File.Exists(path)) return false;
                var temporary = Path.Combine(storeRoot,
                    Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew,
                        FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                        writer.Write(data);
                    if (File.Exists(path)) return false;
                    File.Move(temporary, path);
                    return true;
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is ArgumentException || ex is NotSupportedException)
            {
                return false;
            }
        }

        // Absence/corruption is NOT permission to ignore any file.
        // An original file is always accounted for in the existing V2 hash;
        // only bounded *new* .sav/.save files and saves/ folders are ignored.
        public bool TryVerify(string cacheRoot, string gameId,
            string destination, CacheOwnershipReceiptStore receipts,
            CancellationToken cancellationToken, out int addedSaveFiles)
        {
            addedSaveFiles = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!SafeStore(cacheRoot) || receipts == null)
                    return false;
                var key = ZipManagedExtractionService.ReceiptKeyForGame(gameId);
                if (string.IsNullOrEmpty(key)) return false;
                CacheOwnershipReceipt receipt;
                if (!receipts.TryRead(cacheRoot, key, out receipt) ||
                    receipt.Version != 2 ||
                    !string.Equals(PathBoundary.Normalize(destination),
                        PathBoundary.Normalize(receipt.Destination),
                        StringComparison.OrdinalIgnoreCase))
                    return false;

                var path = RecordPath(cacheRoot, gameId);
                if (!File.Exists(path) ||
                    (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    return false;
                ZipMutableDataBaseline saved;
                using (var input = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                {
                    if (input.Length <= 0 || input.Length > MaxBaselineBytes)
                        return false;
                    using (var reader = new StreamReader(input, Encoding.UTF8))
                        saved = new JavaScriptSerializer
                        {
                            MaxJsonLength = MaxBaselineBytes
                        }.Deserialize<ZipMutableDataBaseline>(reader.ReadToEnd());
                }
                if (saved == null || saved.Version != 1 ||
                    !string.Equals(saved.GameId, gameId, StringComparison.Ordinal) ||
                    !string.Equals(PathBoundary.Normalize(saved.CacheRoot),
                        PathBoundary.Normalize(cacheRoot), StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(PathBoundary.Normalize(saved.Destination),
                        PathBoundary.Normalize(destination), StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(saved.OriginalFingerprint,
                        receipt.InventoryFingerprint, StringComparison.Ordinal) ||
                    !ValidEntries(saved.OriginalEntries, receipt.InventoryFileCount,
                        receipt.InventoryDirectoryCount))
                    return false;

                cancellationToken.ThrowIfCancellationRequested();
                var originalPaths = new HashSet<string>(
                    saved.OriginalEntries, StringComparer.Ordinal);
                var current = new CacheTreeInventoryScanner().ScanWithAddedSaves(
                    cacheRoot, destination, originalPaths, cancellationToken);
                if (current.FileCount != receipt.InventoryFileCount ||
                    current.DirectoryCount != receipt.InventoryDirectoryCount ||
                    !string.Equals(current.Fingerprint,
                        receipt.InventoryFingerprint, StringComparison.Ordinal))
                    return false;
                addedSaveFiles = current.IgnoredAddedSaveCount;
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is ArgumentException || ex is NotSupportedException)
            {
                return false;
            }
        }

        private static bool ValidEntries(IReadOnlyCollection<string> entries,
            int fileCount, int directoryCount)
        {
            if (entries == null || entries.Count != fileCount + directoryCount ||
                entries.Count == 0 || entries.Count > MaxOriginalEntries)
                return false;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            var files = 0;
            var dirs = 0;
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry) ||
                    entry.Length > 1024 || entry.Length < 3 ||
                    (entry[0] != 'F' && entry[0] != 'D') ||
                    entry[1] != ':' || !unique.Add(entry))
                    return false;
                if (entry[0] == 'F') files++; else dirs++;
            }
            return files == fileCount && dirs == directoryCount;
        }

        private bool SafeStore(string cacheRoot)
        {
            return !string.IsNullOrWhiteSpace(cacheRoot) &&
                !PathBoundary.IsContained(cacheRoot, storeRoot) &&
                !PathBoundary.IsContained(storeRoot, cacheRoot);
        }

        private string RecordPath(string cacheRoot, string gameId)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(
                    PathBoundary.Normalize(cacheRoot).ToUpperInvariant() + "\n" +
                    gameId.ToUpperInvariant());
                return Path.Combine(storeRoot,
                    BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "") + ".json");
            }
        }
    }
}
