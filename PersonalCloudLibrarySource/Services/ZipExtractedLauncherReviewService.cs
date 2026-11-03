using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public sealed class ZipExtractedLauncherReview
    {
        public bool InventoryVerified { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
        public string ExtractedDirectory { get; internal set; } = string.Empty;
        public LauncherSelectionPreview Launchers { get; internal set; }
            = new LauncherSelectionPreview();
        // A suggestion is NEVER a Playnite install, Play action or execution.
        public bool CanLaunchWithoutUserConfirmation => false;
        public bool CanAuthorizeRemoval => false;
    }

    // Read-only inspection of a specifically named managed ZIP extraction.
    // Never scans an arbitrary caller-selected folder or adopts legacy files.
    public sealed class ZipExtractedLauncherReviewService
    {
        public bool HasRecordedExtraction(string cacheRoot, string gameId,
            CacheOwnershipReceiptStore receipts)
        {
            try
            {
                string key, destination;
                if (!TryLocation(cacheRoot, gameId, out key, out destination) ||
                    receipts == null)
                    return false;
                CacheOwnershipReceipt receipt;
                return receipts.TryRead(cacheRoot, key, out receipt) &&
                    receipt.Version == 2 &&
                    string.Equals(PathBoundary.Normalize(receipt.Destination),
                        destination, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(destination);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public ZipExtractedLauncherReview Inspect(string cacheRoot, string gameId,
            string gameTitle, CacheOwnershipReceiptStore receipts,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var refused = new ZipExtractedLauncherReview
            {
                Reason = "Managed ZIP extraction with a verified inventory is required."
            };
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string key, destination;
                if (!TryLocation(cacheRoot, gameId, out key, out destination) ||
                    receipts == null)
                    return refused;
                CacheOwnershipReceipt receipt;
                if (!receipts.TryRead(cacheRoot, key, out receipt) ||
                    receipt.Version != 2 ||
                    !string.Equals(PathBoundary.Normalize(receipt.Destination),
                        destination, StringComparison.OrdinalIgnoreCase) ||
                    !Directory.Exists(destination))
                    return refused;

                // Compare all contents to the original extraction inventory.
                // Any added save/mod/installer, removed file, link or changed
                // bytes quarantines the preview rather than being adopted.
                bool matched;
                if (!receipts.TryCompareInventory(cacheRoot, key, destination,
                        out matched) || !matched)
                {
                    refused.Reason = "Extracted files changed since the ownership inventory was captured.";
                    return refused;
                }
                cancellationToken.ThrowIfCancellationRequested();

                var paths = new List<string>();
                var pending = new Stack<string>();
                pending.Push(destination);
                var count = 0;
                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var current = pending.Pop();
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        return refused;
                    foreach (var file in Directory.EnumerateFileSystemEntries(current,
                        "*", SearchOption.TopDirectoryOnly))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (++count > ArchiveInspectionPolicy.MaxEntries)
                            return refused;
                        var attributes = File.GetAttributes(file);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                            return refused;
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            pending.Push(file);
                        }
                        else
                        {
                            var relative = file.Substring(destination.Length)
                                .TrimStart(Path.DirectorySeparatorChar,
                                    Path.AltDirectorySeparatorChar);
                            if (!string.IsNullOrWhiteSpace(relative))
                                paths.Add(relative);
                        }
                    }
                }
                // This is only an informational filename ranking. No
                // candidate is made executable, and no GameMetadata changes.
                return new ZipExtractedLauncherReview
                {
                    InventoryVerified = true,
                    ExtractedDirectory = destination,
                    Launchers = new GameLauncherCandidatePolicy().Preview(
                        paths, gameTitle),
                    Reason = "Extracted inventory matches the original verified ZIP files. " +
                        "No launcher has been approved or executed."
                };
            }
            catch (OperationCanceledException)
            {
                refused.Reason = "Launcher review was cancelled.";
                return refused;
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is InvalidOperationException ||
                                       ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                return refused;
            }
        }

        private static bool TryLocation(string cacheRoot, string gameId,
            out string key, out string destination)
        {
            key = ZipManagedExtractionService.ReceiptKeyForGame(gameId);
            destination = string.Empty;
            if (string.IsNullOrEmpty(key) || string.IsNullOrWhiteSpace(cacheRoot))
                return false;
            var cache = PathBoundary.Normalize(cacheRoot);
            if (!Directory.Exists(cache)) return false;
            var path = Path.GetFullPath(Path.Combine(cache, key));
            if (!PathBoundary.IsContained(cache, path) ||
                string.Equals(cache, path, StringComparison.OrdinalIgnoreCase) ||
                !new SafeCacheDeletionPolicy().Authorize(cache, path, false).Allowed)
                return false;
            destination = path;
            return true;
        }
    }
}
