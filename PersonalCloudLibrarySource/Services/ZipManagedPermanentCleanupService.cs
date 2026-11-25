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
    public sealed class ZipCleanupFile
    {
        public string RelativePath { get; set; }
        public long Length { get; set; }
        public string Sha256 { get; set; }
    }

    public sealed class ZipCleanupManifest
    {
        public int Version { get; set; }
        public string GameId { get; set; }
        public string CacheRoot { get; set; }
        public string RecoveryDirectory { get; set; }
        public string TransactionId { get; set; }
        public string InventoryFingerprint { get; set; }
        public List<ZipCleanupFile> Files { get; set; }
        public List<string> Directories { get; set; }
    }

    public sealed class ZipCleanupResult
    {
        public bool Succeeded { get; internal set; }
        public bool RequiresReview { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
        public int RemovedFiles { get; internal set; }
        public int TotalFiles { get; internal set; }
        public bool CanDeleteUnattended => false;
    }

    // A narrowly scoped FINAL step after a separately approved, verified
    // ZIP quarantine. No Directory.Delete(..., true), wildcards, or deletion
    // of unknown files. A create-only plan and per-file intent markers allow
    // an interrupted cleanup to resume without adopting later user files.
    // Intended for original ZIP package content ONLY. Requires explicit Yes.
    public sealed class ZipManagedPermanentCleanupService
    {
        private const int MaxFiles = 4096;
        private const int MaxDirectories = 4096;
        private const int MaxManifestBytes = 2 * 1024 * 1024;
        private readonly string journalRoot;
        private readonly ZipManagedRecoveryService recovery;

        public ZipManagedPermanentCleanupService(string journalRoot)
        {
            if (string.IsNullOrWhiteSpace(journalRoot) || !Path.IsPathRooted(journalRoot))
                throw new ArgumentException("Cleanup journal store must be rooted.");
            this.journalRoot = Path.GetFullPath(journalRoot);
            recovery = new ZipManagedRecoveryService(this.journalRoot);
        }

        public ZipCleanupResult Preview(string cacheRoot, string gameId,
            CacheOwnershipReceiptStore receipts, ZipLauncherApprovalService approvals,
            CancellationToken token)
        {
            var result = new ZipCleanupResult();
            try
            {
                token.ThrowIfCancellationRequested();
                if (receipts == null || approvals == null ||
                    approvals.HasApprovalFootprint(gameId, cacheRoot))
                    return Refuse(result, "Launcher approval must be revoked before permanent cleanup.");
                var state = recovery.Inspect(cacheRoot, gameId);
                if (state.State != ZipRecoveryState.Quarantined)
                    return Refuse(result, "A verified, journaled ZIP quarantine is required.");
                ZipCleanupManifest manifest;
                if (!BuildManifest(cacheRoot, gameId, state, receipts,
                    token, out manifest))
                    return Refuse(result, "Quarantined ZIP files are modified, untracked, unsafe or too large for verified cleanup.");
                result.TotalFiles = manifest.Files.Count;
                result.Reason = "Eligible original files: " + result.TotalFiles +
                    ". Final deletion requires a separate explicit confirmation.";
                return result;
            }
            catch (OperationCanceledException)
            {
                return Refuse(result, "Cleanup preview was cancelled.");
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is ArgumentException ||
                ex is InvalidOperationException || ex is NotSupportedException)
            {
                return Refuse(result, "Cleanup preview could not verify the package.");
            }
        }

        public ZipCleanupResult Execute(string cacheRoot, string gameId,
            CacheOwnershipReceiptStore receipts, ZipLauncherApprovalService approvals,
            bool userConfirmed, CancellationToken token, Action<int> onFileRemoved = null)
        {
            var result = new ZipCleanupResult();
            if (!userConfirmed)
                return Refuse(result, "Explicit permanent-deletion confirmation is required.");
            try
            {
                token.ThrowIfCancellationRequested();
                if (receipts == null || approvals == null ||
                    approvals.HasApprovalFootprint(gameId, cacheRoot))
                    return Refuse(result, "Launcher approval remains or cannot be verified absent.");

                var state = recovery.Inspect(cacheRoot, gameId);
                if (state.State != ZipRecoveryState.Quarantined &&
                    state.State != ZipRecoveryState.CleanupPending)
                    return Refuse(result, "A matching quarantine or in-progress cleanup is required.");
                if (string.IsNullOrWhiteSpace(state.TransactionId))
                    return Refuse(result, "Recovery journal is not verifiable.");
                string manifestPath;
                if (!TryManifestPath(cacheRoot, gameId, out manifestPath))
                    return Refuse(result, "Invalid cleanup journal location.");

                ZipCleanupManifest manifest;
                if (File.Exists(manifestPath))
                {
                    if (!TryReadManifest(manifestPath, cacheRoot, gameId, state,
                        receipts, out manifest))
                        return Refuse(result, "Cleanup journal is invalid; manual review is required.");
                }
                else
                {
                    if (state.State != ZipRecoveryState.Quarantined ||
                        !BuildManifest(cacheRoot, gameId, state, receipts,
                            token, out manifest) ||
                        !WriteCreateOnly(manifestPath, Serialize(manifest)))
                        return Refuse(result, "Could not create the verified cleanup journal. No files were removed.");
                }
                result.TotalFiles = manifest.Files.Count;
                token.ThrowIfCancellationRequested();

                // Before every deletion batch, require that all *present*
                // entries are in the original immutable manifest. A newly
                // added save, mod, executable, junction, or unknown folder
                // causes refusal BEFORE another file is removed.
                if (!CheckCurrentTree(manifest, manifestPath, token))
                    return Refuse(result, "Unrecognized content or unrecorded missing files: preserve the package for review.");

                var destination = manifest.RecoveryDirectory;
                var files = manifest.Files;
                for (var index = 0; index < files.Count; index++)
                {
                    token.ThrowIfCancellationRequested();
                    if (approvals.HasApprovalFootprint(gameId, cacheRoot))
                        return Refuse(result, "Launcher approval appeared during cleanup.");
                    if (!CheckCurrentTree(manifest, manifestPath, token))
                        return Refuse(result, "Package content changed during cleanup; remaining files were preserved.");

                    var expected = files[index];
                    var path = Located(destination, expected.RelativePath);
                    if (string.IsNullOrEmpty(path))
                        return Refuse(result, "Untrusted original file path in cleanup journal.");
                    var marker = IntentPath(manifestPath, index);
                    if (!File.Exists(path))
                    {
                        if (!ValidIntent(marker, manifest.TransactionId, expected))
                            return Refuse(result, "Original file is missing without its deletion-intent record.");
                        continue;
                    }
                    if (!SafeFile(cacheRoot, path))
                        return Refuse(result, "Original file is linked or unsafe.");

                    // A present file with a pre-existing intent may be a
                    // replacement or a crash before disposition. Do not replay
                    // that uncertain operation against a present pathname.
                    if (CacheOwnershipReceiptStore.ExistsOrCannotVerifyAbsence(marker))
                        return Refuse(result, "A present file has a prior deletion intent; preserve it for manual review.");
                    using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(path, true))
                    {
                        TransferPartialPathPolicy.CapturePrivateFileIdentity(input.SafeFileHandle);
                        if (input.Length != expected.Length ||
                            !string.Equals(Digest(input, token), expected.Sha256,
                                StringComparison.Ordinal))
                            return Refuse(result, "Original file content changed; no further deletions.");
                        if (!WriteCreateOnly(marker, IntentText(
                            manifest.TransactionId, expected)))
                            return Refuse(result, "Could not save file deletion intent.");
                        if (!ValidIntent(marker, manifest.TransactionId, expected))
                            return Refuse(result, "File deletion intent is corrupt.");
                        token.ThrowIfCancellationRequested();
                        if (!SafeFile(cacheRoot, path))
                            return Refuse(result, "File path became unsafe.");
                        TransferPartialPathPolicy.CapturePrivateFileIdentity(input.SafeFileHandle);
                        TransferPartialPathPolicy.DeleteVerifiedFileHandle(input.SafeFileHandle);
                    }
                    TransferPartialPathPolicy.RequireVacant(path);
                    result.RemovedFiles++;
                    onFileRemoved?.Invoke(result.RemovedFiles);
                }

                token.ThrowIfCancellationRequested();
                if (!CheckCurrentTree(manifest, manifestPath, token))
                    return Refuse(result, "Unexpected content appeared before directory cleanup.");
                foreach (var relative in manifest.Directories
                    .OrderByDescending(x => x.Length).ThenByDescending(x => x,
                        StringComparer.Ordinal))
                {
                    token.ThrowIfCancellationRequested();
                    var directory = Located(destination, relative);
                    if (string.IsNullOrEmpty(directory) ||
                        !new SafeCacheDeletionPolicy()
                            .Authorize(cacheRoot, directory, false).Allowed)
                        return Refuse(result, "Unsafe directory during cleanup.");
                    if (Directory.Exists(directory))
                        TransferPartialPathPolicy.DeleteOwnedEmptyDirectory(directory,
                            TransferPartialPathPolicy.ObserveDirectoryIdentity(directory)); // empty ONLY
                }
                if (Directory.Exists(destination))
                    TransferPartialPathPolicy.DeleteOwnedEmptyDirectory(destination,
                        TransferPartialPathPolicy.ObserveDirectoryIdentity(destination)); // empty ONLY
                if (Directory.Exists(destination) || File.Exists(destination))
                    return Refuse(result, "Cleanup folder still contains files; preserve it.");
                var finalMarker = manifestPath.Substring(0,
                    manifestPath.Length - ".purge.json".Length) + ".purged";
                if (!WriteCreateOnly(finalMarker, manifest.TransactionId))
                    return Refuse(result, "Files removed, but cleanup finalization marker failed; resume only after review.");
                result.Succeeded = true;
                result.Reason = "Verified original ZIP files permanently removed. Original ZIP source, receipt and audit journals remain.";
                return result;
            }
            catch (OperationCanceledException)
            {
                return Refuse(result, "Cleanup cancelled. A durable journal preserves resumable progress.");
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is ArgumentException || ex is NotSupportedException)
            {
                return Refuse(result, "Cleanup halted safely; inspect durable progress and remaining files.");
            }
        }

        private static ZipCleanupResult Refuse(ZipCleanupResult result, string reason)
        {
            result.RequiresReview = true;
            result.Reason = reason;
            return result;
        }

        private bool BuildManifest(string cacheRoot, string gameId,
            ZipRecoveryAssessment state, CacheOwnershipReceiptStore receipts,
            CancellationToken token, out ZipCleanupManifest manifest)
        {
            manifest = null;
            if (string.IsNullOrWhiteSpace(state.TransactionId) ||
                !SafeRoot(cacheRoot, state.RecoveryDirectory))
                return false;
            CacheOwnershipReceipt receipt;
            var key = ZipManagedExtractionService.ReceiptKeyForGame(gameId);
            if (!receipts.TryRead(cacheRoot, key, out receipt) ||
                receipt.Version != 2 ||
                !string.Equals(PathBoundary.Normalize(receipt.Destination),
                    PathBoundary.Normalize(state.OriginalDirectory),
                    StringComparison.OrdinalIgnoreCase))
                return false;
            var current = new CacheTreeInventoryScanner().ScanDetailed(cacheRoot,
                state.RecoveryDirectory, token);
            if (current.FileCount != receipt.InventoryFileCount ||
                current.DirectoryCount != receipt.InventoryDirectoryCount ||
                !string.Equals(current.Fingerprint, receipt.InventoryFingerprint,
                    StringComparison.Ordinal) ||
                current.FileCount > MaxFiles || current.DirectoryCount > MaxDirectories)
                return false;

            var files = new List<ZipCleanupFile>();
            var dirs = new List<string>();
            // Use the exact entries whose digest was compared to the receipt.
            // A separate per-file scan could adopt a transient replacement
            // even if a later whole-tree scan matched the original again.
            foreach (var entry in current.DetailedEntries)
            {
                token.ThrowIfCancellationRequested();
                if (Located(state.RecoveryDirectory, entry.RelativePath) == null) return false;
                if (entry.IsDirectory)
                    dirs.Add(entry.RelativePath);
                else
                    files.Add(new ZipCleanupFile
                    {
                        RelativePath = entry.RelativePath,
                        Length = entry.FileBytes,
                        Sha256 = entry.Sha256
                    });
            }
            manifest = new ZipCleanupManifest
            {
                Version = 1, GameId = gameId,
                CacheRoot = PathBoundary.Normalize(cacheRoot),
                RecoveryDirectory = PathBoundary.Normalize(state.RecoveryDirectory),
                TransactionId = state.TransactionId,
                InventoryFingerprint = receipt.InventoryFingerprint,
                Files = files.OrderBy(x => x.RelativePath, StringComparer.Ordinal).ToList(),
                Directories = dirs.OrderBy(x => x, StringComparer.Ordinal).ToList()
            };
            return Encoding.UTF8.GetByteCount(Serialize(manifest)) <= MaxManifestBytes;
        }

        private bool TryReadManifest(string file, string cacheRoot, string gameId,
            ZipRecoveryAssessment state, CacheOwnershipReceiptStore receipts,
            out ZipCleanupManifest manifest)
        {
            manifest = null;
            if (!File.Exists(file) ||
                (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                return false;
            using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(file, false))
                manifest = new JavaScriptSerializer
                {
                    MaxJsonLength = MaxManifestBytes
                }.Deserialize<ZipCleanupManifest>(
                    TransferPartialPathPolicy.ReadPrivateRecord(input, MaxManifestBytes));
            if (manifest == null || manifest.Version != 1 ||
                manifest.Files == null || manifest.Directories == null ||
                manifest.Files.Count > MaxFiles ||
                manifest.Directories.Count > MaxDirectories ||
                !string.Equals(manifest.GameId, gameId, StringComparison.Ordinal) ||
                !string.Equals(manifest.TransactionId, state.TransactionId,
                    StringComparison.Ordinal) ||
                !string.Equals(manifest.CacheRoot,
                    PathBoundary.Normalize(cacheRoot), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(manifest.RecoveryDirectory,
                    PathBoundary.Normalize(state.RecoveryDirectory),
                    StringComparison.OrdinalIgnoreCase))
                return false;
            CacheOwnershipReceipt receipt;
            if (!receipts.TryRead(cacheRoot,
                ZipManagedExtractionService.ReceiptKeyForGame(gameId), out receipt) ||
                receipt.Version != 2 ||
                !string.Equals(manifest.InventoryFingerprint,
                    receipt.InventoryFingerprint, StringComparison.Ordinal) ||
                manifest.Files.Count != receipt.InventoryFileCount ||
                manifest.Directories.Count != receipt.InventoryDirectoryCount)
                return false;
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.Files)
            {
                if (entry == null || entry.Length < 0 ||
                    !ValidHash(entry.Sha256) ||
                    Located(state.RecoveryDirectory, entry.RelativePath) == null ||
                    !unique.Add(entry.RelativePath))
                    return false;
            }
            foreach (var dir in manifest.Directories)
                if (Located(state.RecoveryDirectory, dir) == null ||
                    !unique.Add(dir))
                    return false;

            // A copied fingerprint field is not evidence that this manifest's
            // paths, byte counts and hashes match the original inventory.
            // Preserve canonical order because deletion-intent indexes depend on it.
            if (!manifest.Files.Select(x => x.RelativePath).SequenceEqual(
                    manifest.Files.Select(x => x.RelativePath).OrderBy(x => x, StringComparer.Ordinal)) ||
                !manifest.Directories.SequenceEqual(manifest.Directories.OrderBy(x => x, StringComparer.Ordinal)))
                return false;
            var records = manifest.Files.Select(x => "F:" + x.RelativePath + ":" + x.Length + ":" + x.Sha256)
                .Concat(manifest.Directories.Select(x => "D:" + x))
                .OrderBy(x => x, StringComparer.Ordinal);
            using (var sha = SHA256.Create())
                return string.Equals(BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(string.Join("\n", records)))).Replace("-", ""),
                    receipt.InventoryFingerprint, StringComparison.Ordinal);
        }

        private static bool CheckCurrentTree(ZipCleanupManifest manifest,
            string journalPath, CancellationToken token)
        {
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in manifest.Files) expected.Add("F:" + file.RelativePath);
            foreach (var dir in manifest.Directories) expected.Add("D:" + dir);
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            if (Directory.Exists(manifest.RecoveryDirectory))
                pending.Push(manifest.RecoveryDirectory);
            var count = 0;
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var folder = pending.Pop();
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                    return false;
                foreach (var child in Directory.EnumerateFileSystemEntries(folder))
                {
                    token.ThrowIfCancellationRequested();
                    if (++count > MaxFiles + MaxDirectories) return false;
                    var attrs = File.GetAttributes(child);
                    if ((attrs & FileAttributes.ReparsePoint) != 0) return false;
                    var relative = child.Substring(manifest.RecoveryDirectory.Length)
                        .TrimStart(Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar);
                    var type = (attrs & FileAttributes.Directory) != 0 ? "D:" : "F:";
                    if (!expected.Contains(type + relative) ||
                        !found.Add(type + relative)) return false;
                    if (type == "D:") pending.Push(child);
                }
            }
            for (var index = 0; index < manifest.Files.Count; index++)
            {
                var file = manifest.Files[index];
                var marker = IntentPath(journalPath, index);
                if (found.Contains("F:" + file.RelativePath))
                {
                    if (CacheOwnershipReceiptStore.ExistsOrCannotVerifyAbsence(marker))
                        return false;
                    continue;
                }
                if (!ValidIntent(marker, manifest.TransactionId, file))
                    return false;
            }
            // Missing original directories are possible after an interrupted
            // cleanup, but no unknown content is permitted.
            return true;
        }

        private static bool SafeRoot(string cacheRoot, string folder)
        {
            return Directory.Exists(folder) &&
                new SafeCacheDeletionPolicy()
                    .Authorize(cacheRoot, folder, false).Allowed &&
                string.IsNullOrWhiteSpace(
                    SafeCacheDeletionExecutor.GetUnsafeDescendantReason(folder));
        }

        private static bool SafeFile(string cacheRoot, string path)
        {
            return File.Exists(path) &&
                new SafeCacheDeletionPolicy().Authorize(cacheRoot, path, false).Allowed &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
        }

        private static string Located(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) ||
                relative.Length > 1024 ||
                Path.IsPathRooted(relative) ||
                relative.IndexOf(':') >= 0)
                return null;
            var components = relative.Split(new[] {
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar
            }, StringSplitOptions.None);
            if (components.Any(x => x.Length == 0 || x == "." || x == ".."))
                return null;
            var full = Path.GetFullPath(Path.Combine(root, relative));
            return PathBoundary.IsContained(root, full) ? full : null;
        }

        private bool TryManifestPath(string cacheRoot, string gameId,
            out string path)
        {
            path = string.Empty;
            if (string.IsNullOrWhiteSpace(cacheRoot) ||
                string.IsNullOrWhiteSpace(
                    ZipManagedExtractionService.ReceiptKeyForGame(gameId)) ||
                PathBoundary.IsContained(cacheRoot, journalRoot) ||
                PathBoundary.IsContained(journalRoot, cacheRoot))
                return false;
            using (var sha = SHA256.Create())
            {
                var id = PathBoundary.Normalize(cacheRoot).ToUpperInvariant() +
                    "\n" + gameId.ToUpperInvariant();
                path = Path.Combine(journalRoot,
                    BitConverter.ToString(sha.ComputeHash(
                        Encoding.UTF8.GetBytes(id))).Replace("-", "") +
                    ".json.purge.json");
            }
            return true;
        }

        private static string IntentPath(string manifestPath, int index)
        {
            return manifestPath + ".del." + index.ToString("D5");
        }

        private static string IntentText(string id, ZipCleanupFile file)
        {
            return id + "|" + file.Sha256 + "|" + file.RelativePath;
        }

        private static bool ValidIntent(string path, string id, ZipCleanupFile file)
        {
            try
            {
                using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
                    return string.Equals(TransferPartialPathPolicy.ReadPrivateRecord(input, 1200),
                        IntentText(id, file), StringComparison.Ordinal);
            }
            catch (Exception) { return false; }
        }

        private static bool ValidHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash) || hash.Length != 64)
                return false;
            return hash.All(x => (x >= '0' && x <= '9') ||
                (x >= 'A' && x <= 'F'));
        }

        private static string Digest(Stream stream, CancellationToken token)
        {
            using (var sha = SHA256.Create())
            {
                var buffer = new byte[65536];
                long remaining = stream.Length;
                if (remaining < 0 || remaining > CacheTreeInventoryScanner.MaximumFileBytes)
                    throw new IOException("Cleanup digest exceeds the inventory size limit.");
                while (remaining > 0)
                {
                    token.ThrowIfCancellationRequested();
                    var count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (count == 0) throw new IOException("Cleanup digest input was truncated.");
                    sha.TransformBlock(buffer, 0, count, buffer, 0);
                    remaining -= count;
                }
                token.ThrowIfCancellationRequested();
                if (stream.ReadByte() != -1) throw new IOException("Cleanup digest input grew.");
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "");
            }
        }

        private static string Serialize(ZipCleanupManifest manifest)
        {
            return new JavaScriptSerializer
            {
                MaxJsonLength = MaxManifestBytes
            }.Serialize(manifest);
        }

        private static bool WriteCreateOnly(string path, string value)
        {
            var limit = path.EndsWith(".purge.json", StringComparison.Ordinal) ? MaxManifestBytes :
                path.EndsWith(".purged", StringComparison.Ordinal) ? 64 : 1200;
            TransferPartialPathPolicy.WritePrivateRecord(path, value, limit, CancellationToken.None);
            return true;
        }
    }
}
