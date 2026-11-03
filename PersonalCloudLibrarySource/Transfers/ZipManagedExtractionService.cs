using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    // A stand-alone, explicitly confirmed extraction. It does not claim
    // Playnite installation, create Play actions or authorize any deletion.
    public sealed class ZipManagedExtractionPlan
    {
        internal ZipManagedExtractionPlan(ZipPackageReview review,
            string archivePath, string cacheRoot, string destination,
            string receiptKey)
        {
            Review = review;
            ArchivePath = archivePath;
            CacheRoot = cacheRoot;
            Destination = destination;
            ReceiptKey = receiptKey;
        }

        internal ZipPackageReview Review { get; }
        internal string ReceiptKey { get; }
        public string ArchivePath { get; }
        public string CacheRoot { get; }
        public string Destination { get; }
    }

    public sealed class ZipManagedExtractionResult
    {
        public bool Succeeded { get; internal set; }
        public bool Cancelled { get; internal set; }
        public bool ReceiptRecorded { get; internal set; }
        public bool NeedsManualAttention { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
        public string Destination { get; internal set; } = string.Empty;
    }

    public sealed class ZipManagedExtractionService
    {
        public ZipManagedExtractionPlan Prepare(ZipPackageReview reviewed,
            string archivePath, string cacheRoot, string gameId,
            CacheOwnershipReceiptStore receipts)
        {
            if (reviewed == null || !reviewed.PassedPreflight ||
                receipts == null || string.IsNullOrWhiteSpace(gameId) ||
                gameId.Length > 256 || gameId != gameId.Trim() ||
                gameId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                gameId == "." || gameId == ".." ||
                string.IsNullOrWhiteSpace(archivePath) ||
                string.IsNullOrWhiteSpace(cacheRoot))
                return null;
            try
            {
                var normalizedArchive = Path.GetFullPath(archivePath);
                var normalizedCache = PathBoundary.Normalize(cacheRoot);
                if (!Directory.Exists(normalizedCache) ||
                    !string.Equals(reviewed.NormalizedArchivePath,
                        normalizedArchive, StringComparison.OrdinalIgnoreCase))
                    return null;

                // The folder name is identity-derived, never a manifest path.
                // Direct child of the pre-existing cache root so extraction
                // cannot create arbitrary parents or overwrite other content.
                var key = "pcls-zip-" + Hash(gameId.ToUpperInvariant());
                var destination = Path.Combine(normalizedCache, key);
                if (!new SafeCacheDeletionPolicy()
                    .Authorize(normalizedCache, destination, false).Allowed ||
                    File.Exists(destination) || Directory.Exists(destination) ||
                    PathBoundary.IsContained(destination, normalizedArchive))
                    return null;

                CacheOwnershipReceipt existing;
                if (receipts.TryRead(normalizedCache, key, out existing))
                    return null;
                return new ZipManagedExtractionPlan(reviewed, normalizedArchive,
                    normalizedCache, destination, key);
            }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is NotSupportedException)
            {
                return null;
            }
        }

        public ZipManagedExtractionResult ExecuteApproved(
            ZipManagedExtractionPlan plan, bool userConfirmedExtract,
            CacheOwnershipReceiptStore receipts, CancellationToken cancellationToken,
            Action<long, long?> progress = null)
        {
            var refused = new ZipManagedExtractionResult
            {
                Reason = "An explicit extraction choice and a valid new cache destination are required."
            };
            if (!userConfirmedExtract || plan == null || receipts == null)
                return refused;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Never accept an arbitrary path from a controller or caller:
                // recompute the only allowable destination for the frozen ID.
                var expected = Path.Combine(plan.CacheRoot, plan.ReceiptKey);
                if (!string.Equals(PathBoundary.Normalize(plan.Destination),
                        PathBoundary.Normalize(expected), StringComparison.OrdinalIgnoreCase) ||
                    !new SafeCacheDeletionPolicy()
                        .Authorize(plan.CacheRoot, plan.Destination, false).Allowed ||
                    PathBoundary.IsContained(plan.Destination, plan.ArchivePath))
                    return refused;
                CacheOwnershipReceipt existing;
                if (receipts.TryRead(plan.CacheRoot, plan.ReceiptKey, out existing) ||
                    File.Exists(plan.Destination) || Directory.Exists(plan.Destination))
                    return new ZipManagedExtractionResult
                    {
                        Reason = "Package extraction already exists or has an ownership receipt."
                    };

                var extraction = new ZipPackageReviewService().ExtractAfterExplicitChoice(
                    plan.Review, true, plan.ArchivePath, plan.CacheRoot,
                    plan.Destination, Guid.NewGuid(), cancellationToken, progress);
                if (!extraction.Succeeded)
                    return new ZipManagedExtractionResult
                    {
                        Cancelled = extraction.Cancelled,
                        Reason = extraction.Message
                    };

                // The ZIP has already been atomically committed. From this
                // point a receipt failure must NEVER erase the new game files.
                var outcome = new ZipManagedExtractionResult
                {
                    Succeeded = true, Destination = plan.Destination,
                    Reason = "ZIP extracted to a separate cache folder. This does not install the game."
                };
                try
                {
                    // An inventory receipt is diagnostic provenance only; its
                    // presence must never authorize recursive uninstall.
                    receipts.RecordCompletedWithInventory(plan.CacheRoot,
                        plan.ReceiptKey, plan.Destination, CancellationToken.None);
                    outcome.ReceiptRecorded = true;
                }
                catch (Exception ex) when (ex is IOException ||
                                           ex is UnauthorizedAccessException ||
                                           ex is InvalidOperationException ||
                                           ex is ArgumentException)
                {
                    outcome.NeedsManualAttention = true;
                    outcome.Reason = "ZIP extracted, but its ownership inventory could not be recorded. " +
                        "Preserve the files and inspect the destination manually.";
                }
                return outcome;
            }
            catch (OperationCanceledException)
            {
                return new ZipManagedExtractionResult
                {
                    Cancelled = true, Reason = "ZIP extraction was cancelled."
                };
            }
            catch (Exception ex) when (ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is InvalidOperationException ||
                                       ex is ArgumentException ||
                                       ex is NotSupportedException)
            {
                return new ZipManagedExtractionResult
                {
                    Reason = "ZIP extraction could not be safely prepared."
                };
            }
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
    }
}
