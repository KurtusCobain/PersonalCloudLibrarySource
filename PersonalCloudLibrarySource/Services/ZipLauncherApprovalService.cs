using Playnite.SDK.Models;
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
    public sealed class ZipLauncherApproval
    {
        public int Version { get; set; }
        public string GameId { get; set; }
        public string CacheRoot { get; set; }
        public string Destination { get; set; }
        public string RelativeLauncher { get; set; }
        public string InventoryFingerprint { get; set; }
        public string SourceBinding { get; set; }
    }

    // Persisted approval is explicit user intent, not a security credential.
    // Every import independently rechecks current manifest binding, receipt,
    // actual directory inventory and the selected executable before using it.
    public sealed class ZipLauncherApprovalService
    {
        private const int MaxBytes = 8192;
        private readonly string storeRoot;

        public ZipLauncherApprovalService(string storeRoot)
        {
            if (string.IsNullOrWhiteSpace(storeRoot) || !Path.IsPathRooted(storeRoot))
                throw new ArgumentException("Approval store must be rooted.");
            this.storeRoot = Path.GetFullPath(storeRoot);
        }

        // Bound candidate discovery so a large manifest does not trigger
        // thousands of filesystem probes on every Playnite library refresh.
        // IDs from these files are untrusted until TryApply revalidates them.
        public IReadOnlyCollection<string> ApprovedGameIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                if (!Directory.Exists(storeRoot)) return ids;
                var files = Directory.EnumerateFiles(storeRoot, "*.json",
                    SearchOption.TopDirectoryOnly).Take(257).ToList();
                if (files.Count > 256) return ids;
                foreach (var file in files)
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                        continue;
                    using (var input = new FileStream(file, FileMode.Open,
                        FileAccess.Read, FileShare.Read))
                    {
                        if (input.Length <= 0 || input.Length > MaxBytes) continue;
                        using (var reader = new StreamReader(input, Encoding.UTF8))
                        {
                            var record = new JavaScriptSerializer
                            {
                                MaxJsonLength = MaxBytes
                            }.Deserialize<ZipLauncherApproval>(reader.ReadToEnd());
                            if (record != null && record.Version == 1 &&
                                !string.IsNullOrWhiteSpace(record.GameId) &&
                                !string.IsNullOrWhiteSpace(
                                    ZipManagedExtractionService.ReceiptKeyForGame(record.GameId)))
                                ids.Add(record.GameId);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is ArgumentException || ex is NotSupportedException)
            {
                // Partial or unreadable approval stores grant no authority.
                return new string[0];
            }
            return ids;
        }

        // A stale approval remains revocable after the manifest/source changes.
        // This checks only the exact game's approval identity; it does not
        // adopt, modify, or inventory files under the game cache.
        public bool HasApproval(string gameId, string cacheRoot)
        {
            try
            {
                ZipLauncherApproval saved;
                return TryReadApproval(gameId, cacheRoot, out saved);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Revoke is a distinct, explicitly confirmed action. It deletes ONLY
        // one validated approval record in the plugin data directory.
        // Extracted files, receipts, saves and the original ZIP are preserved.
        public bool TryRevoke(string gameId, string cacheRoot,
            bool userConfirmed, out string reason)
        {
            reason = "Explicit confirmation is required to revoke the launcher approval.";
            if (!userConfirmed)
                return false;
            try
            {
                ZipLauncherApproval saved;
                if (!TryReadApproval(gameId, cacheRoot, out saved))
                {
                    reason = "No valid launcher approval was found for this game and cache.";
                    return false;
                }
                var recordPath = RecordPath(gameId, cacheRoot);
                // The validated record is deliberately outside the cache.
                // Never delete a link or use an approval path from JSON.
                if ((File.GetAttributes(recordPath) & FileAttributes.ReparsePoint) != 0)
                    return false;
                File.Delete(recordPath);
                reason = "Launcher approval revoked. Update Game Library to remove the managed ZIP Play action. Extracted files and ownership records were preserved.";
                return true;
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is ArgumentException || ex is NotSupportedException)
            {
                reason = "Launcher approval could not be safely revoked.";
                return false;
            }
        }

        // Source binding is intentionally not checked here: a source change
        // must not trap an obsolete approval that the user wants to revoke.
        private bool TryReadApproval(string gameId, string cacheRoot,
            out ZipLauncherApproval saved)
        {
            saved = null;
            if (string.IsNullOrWhiteSpace(gameId) ||
                string.IsNullOrWhiteSpace(
                    ZipManagedExtractionService.ReceiptKeyForGame(gameId)) ||
                !SafeSeparateStore(cacheRoot))
                return false;
            var path = RecordPath(gameId, cacheRoot);
            if (!File.Exists(path) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                return false;
            using (var stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > MaxBytes)
                    return false;
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    saved = new JavaScriptSerializer { MaxJsonLength = MaxBytes }
                        .Deserialize<ZipLauncherApproval>(reader.ReadToEnd());
            }
            return saved != null && saved.Version == 1 &&
                string.Equals(saved.GameId, gameId, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(saved.CacheRoot) &&
                string.Equals(PathBoundary.Normalize(saved.CacheRoot),
                    PathBoundary.Normalize(cacheRoot), StringComparison.OrdinalIgnoreCase);
        }

        public bool TryApprove(PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings, string relativeLauncher,
            CacheOwnershipReceiptStore receipts, bool userConfirmed,
            CancellationToken cancellationToken, out string reason,
            ZipMutableDataBaselineStore mutableBaseline = null)
        {
            reason = "Explicit approval and verified extracted files are required.";
            if (!userConfirmed || !Eligible(item, settings) || receipts == null ||
                string.IsNullOrWhiteSpace(relativeLauncher))
                return false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!SafeSeparateStore(settings.LocalCacheFolder))
                    return false;
                string recordPath = RecordPath(item.Id, settings.LocalCacheFolder);
                // No implicit overwrite/reapproval. A future revoke/replace UI
                // must be a distinct explicit action.
                if (File.Exists(recordPath))
                {
                    reason = "A launcher approval already exists for this game.";
                    return false;
                }
                var review = new ZipExtractedLauncherReviewService().Inspect(
                    settings.LocalCacheFolder, item.Id, item.Title,
                    receipts, cancellationToken, mutableBaseline);
                if (!review.InventoryVerified)
                {
                    reason = "Extracted files are unverified or have changed.";
                    return false;
                }
                var choice = review.Launchers.Candidates.FirstOrDefault(c =>
                    string.Equals(c.RelativePath, relativeLauncher,
                        StringComparison.OrdinalIgnoreCase) &&
                    c.Kind != LauncherCandidateKind.InstallerOrHelper &&
                    string.Equals(Path.GetExtension(c.RelativePath), ".exe",
                        StringComparison.OrdinalIgnoreCase));
                if (choice == null)
                {
                    reason = "The selected file is not an approved game executable candidate.";
                    return false;
                }

                var key = ZipManagedExtractionService.ReceiptKeyForGame(item.Id);
                CacheOwnershipReceipt receipt;
                if (!receipts.TryRead(settings.LocalCacheFolder, key, out receipt) ||
                    receipt.Version != 2 ||
                    !string.Equals(PathBoundary.Normalize(receipt.Destination),
                        PathBoundary.Normalize(review.ExtractedDirectory),
                        StringComparison.OrdinalIgnoreCase))
                    return false;
                var document = new ZipLauncherApproval
                {
                    Version = 1,
                    GameId = item.Id,
                    CacheRoot = PathBoundary.Normalize(settings.LocalCacheFolder),
                    Destination = PathBoundary.Normalize(review.ExtractedDirectory),
                    RelativeLauncher = choice.RelativePath,
                    InventoryFingerprint = receipt.InventoryFingerprint,
                    SourceBinding = SourceBinding(item, settings)
                };
                cancellationToken.ThrowIfCancellationRequested();

                // Create-only write in the plugin data directory; no game files
                // are edited and no pre-existing approval is overwritten.
                Directory.CreateDirectory(storeRoot);
                var temp = Path.Combine(storeRoot,
                    Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var output = new FileStream(temp, FileMode.CreateNew,
                        FileAccess.Write, FileShare.None))
                    using (var writer = new StreamWriter(output,
                        new UTF8Encoding(false)))
                        writer.Write(new JavaScriptSerializer().Serialize(document));
                    cancellationToken.ThrowIfCancellationRequested();
                    if (File.Exists(recordPath))
                        return false;
                    File.Move(temp, recordPath);
                    reason = "Launcher approved. Update Game Library to refresh Playnite.";
                    return true;
                }
                finally
                {
                    if (File.Exists(temp)) File.Delete(temp);
                }
            }
            catch (OperationCanceledException)
            {
                reason = "Launcher approval was cancelled.";
                return false;
            }
            catch (Exception ex) when (ex is IOException ||
                ex is UnauthorizedAccessException || ex is InvalidOperationException ||
                ex is ArgumentException || ex is NotSupportedException)
            {
                reason = "Launcher approval could not be safely recorded.";
                return false;
            }
        }

        // The only activation path: metadata is never changed for unknown,
        // existing installed, or already-playable manifest games.
        public bool TryApply(PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings, GameMetadata game,
            CacheOwnershipReceiptStore receipts,
            CancellationToken cancellationToken = default(CancellationToken),
            ZipMutableDataBaselineStore mutableBaseline = null)
        {
            if (!Eligible(item, settings) || game == null || receipts == null ||
                game.IsInstalled ||
                (game.GameActions != null && game.GameActions.Count > 0) ||
                !string.Equals(item.Id, game.GameId, StringComparison.Ordinal))
                return false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!SafeSeparateStore(settings.LocalCacheFolder)) return false;
                var path = RecordPath(item.Id, settings.LocalCacheFolder);
                if (!File.Exists(path)) return false;
                ZipLauncherApproval saved;
                using (var input = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                {
                    if (input.Length <= 0 || input.Length > MaxBytes)
                        return false;
                    using (var reader = new StreamReader(input, Encoding.UTF8))
                        saved = new JavaScriptSerializer { MaxJsonLength = MaxBytes }
                            .Deserialize<ZipLauncherApproval>(reader.ReadToEnd());
                }
                if (saved == null || saved.Version != 1 ||
                    !string.Equals(saved.GameId, item.Id, StringComparison.Ordinal) ||
                    !string.Equals(saved.SourceBinding,
                        SourceBinding(item, settings), StringComparison.Ordinal) ||
                    !string.Equals(PathBoundary.Normalize(saved.CacheRoot),
                        PathBoundary.Normalize(settings.LocalCacheFolder),
                        StringComparison.OrdinalIgnoreCase))
                    return false;

                var key = ZipManagedExtractionService.ReceiptKeyForGame(item.Id);
                var expected = Path.Combine(PathBoundary.Normalize(settings.LocalCacheFolder), key);
                if (!string.Equals(PathBoundary.Normalize(saved.Destination),
                    PathBoundary.Normalize(expected), StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetExtension(saved.RelativeLauncher),
                        ".exe", StringComparison.OrdinalIgnoreCase))
                    return false;

                CacheOwnershipReceipt receipt;
                if (!receipts.TryRead(settings.LocalCacheFolder, key, out receipt) ||
                    receipt.Version != 2 ||
                    !string.Equals(receipt.InventoryFingerprint,
                        saved.InventoryFingerprint, StringComparison.Ordinal) ||
                    !string.Equals(PathBoundary.Normalize(receipt.Destination),
                        PathBoundary.Normalize(expected), StringComparison.OrdinalIgnoreCase))
                    return false;
                var review = new ZipExtractedLauncherReviewService().Inspect(
                    settings.LocalCacheFolder, item.Id, item.Title,
                    receipts, cancellationToken, mutableBaseline);
                if (!review.InventoryVerified ||
                    !string.Equals(PathBoundary.Normalize(review.ExtractedDirectory),
                        PathBoundary.Normalize(expected), StringComparison.OrdinalIgnoreCase))
                    return false;
                var candidate = review.Launchers.Candidates.FirstOrDefault(c =>
                    string.Equals(c.RelativePath, saved.RelativeLauncher,
                        StringComparison.OrdinalIgnoreCase) &&
                    c.Kind != LauncherCandidateKind.InstallerOrHelper);
                if (candidate == null) return false;
                // This is a fresh non-reparse containment check, not solely a
                // filename suffix or a persisted record's asserted path.
                var launcher = Path.GetFullPath(Path.Combine(expected,
                    candidate.RelativePath.Replace('\\', Path.DirectorySeparatorChar)));
                if (!PathBoundary.IsContained(expected, launcher) ||
                    !File.Exists(launcher) ||
                    !new SafeCacheDeletionPolicy()
                        .Authorize(expected, launcher, false).Allowed)
                    return false;

                cancellationToken.ThrowIfCancellationRequested();
                game.IsInstalled = true;
                game.InstallDirectory = expected;
                game.GameActions = new List<GameAction>
                {
                    new GameAction
                    {
                        Name = "Play",
                        Type = GameActionType.File,
                        Path = launcher,
                        WorkingDir = expected,
                        IsPlayAction = true
                    }
                };
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

        private bool SafeSeparateStore(string cacheRoot)
        {
            if (string.IsNullOrWhiteSpace(cacheRoot)) return false;
            // The approval directory must never be a cache descendant or parent.
            return !PathBoundary.IsContained(cacheRoot, storeRoot) &&
                !PathBoundary.IsContained(storeRoot, cacheRoot);
        }

        private string RecordPath(string gameId, string cacheRoot)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(
                    PathBoundary.Normalize(cacheRoot).ToUpperInvariant() +
                    "\n" + gameId.ToUpperInvariant());
                return Path.Combine(storeRoot,
                    BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "") + ".json");
            }
        }

        private static bool Eligible(PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings)
        {
            if (item == null || settings == null || !settings.Enabled ||
                string.IsNullOrWhiteSpace(item.Id) ||
                string.IsNullOrWhiteSpace(settings.LocalCacheFolder) ||
                PersonalCloudLibrarySource.IsDirectoryItem(item) ||
                string.IsNullOrWhiteSpace(
                    ZipManagedExtractionService.ReceiptKeyForGame(item.Id)))
                return false;
            try
            {
                var source = PersonalCloudLibrarySource.GetItemSourcePath(item) ?? "";
                return string.Equals(Path.GetExtension(source.Replace('\\', '/')),
                    ".zip", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetExtension(item.CachePath ?? ""),
                        ".zip", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Path.GetExtension(item.LocalPath ?? ""),
                        ".zip", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static string SourceBinding(PersonalCloudLibraryItem item,
            PersonalCloudLibrarySourceSettings settings)
        {
            var text = new StringBuilder("pcls-zip-launcher-v1:");
            Append(text, item.Id);
            Append(text, item.SourcePath);
            Append(text, item.RemotePath);
            Append(text, item.CachePath);
            Append(text, item.LocalPath);
            Append(text, item.InstallDirectory);
            Append(text, item.LaunchFile);
            Append(text, PersonalCloudLibrarySource.GetProviderType(settings));
            Append(text, settings.LocalManifestPath);
            Append(text, settings.LocalLibraryRoot);
            Append(text, settings.RcloneRemoteName);
            Append(text, settings.RcloneContentRoot);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
        }

        private static void Append(StringBuilder builder, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? "");
            builder.Append(bytes.Length).Append(':')
                .Append(Convert.ToBase64String(bytes)).Append(';');
        }
    }
}
