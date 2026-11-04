using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    // Parser-neutral data. A SharpCompress reader or another archive backend
    // must map its metadata into these fields. No entry is authorized to write.
    public sealed class MultiFormatArchiveEntry
    {
        public string Path { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsEncrypted { get; set; }
        public bool IsComplete { get; set; }
        public bool IsSplitAfter { get; set; }
        public bool IsAntiItem { get; set; }
        public string LinkTarget { get; set; }
        public int? Attributes { get; set; }
        public long UncompressedBytes { get; set; }
        public long CompressedBytes { get; set; }
        public bool IsSolid { get; set; }
    }

    public sealed class MultiFormatArchiveReview
    {
        public bool Passed { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
        public int FileCount { get; internal set; }
        public int EntryCount { get; internal set; }
        public long TotalUncompressedBytes { get; internal set; }
        // Read-only policy does not authorize extraction, execution, receipt
        // creation, install or deletion, even when all checks pass.
        public bool CanExtractUnattended => false;
        public bool AuthorizesFilesystemWrite => false;
    }

    // A strict common policy shared by production and the isolated net462
    // decoder probe. Refuse unsupported metadata rather than guessing.
    public sealed class MultiFormatArchivePreflightPolicy
    {
        public const int MaximumEntries = 4096;
        public const long MaximumTotalBytes = 8L * 1024 * 1024 * 1024;
        public const long MaximumFileBytes = 2L * 1024 * 1024 * 1024;
        public const long MaximumExpansionRatio = 1000;

        public MultiFormatArchiveReview Inspect(IEnumerable<MultiFormatArchiveEntry> entries,
            long archiveSizeBytes, int archiveVolumeCount = 1)
        {
            // SharpCompress Entry.VolumeIndexFirst/Last are indexes of entry
            // file parts, NOT proof of separate physical volumes. The caller
            // must instead use IArchive.Volumes.Count() here.
            if (entries == null || archiveSizeBytes <= 0 ||
                archiveVolumeCount != 1)
                return Reject("Archive metadata is unavailable or spans multiple volumes.");

            var observations = new List<ArchiveEntryObservation>();
            long total = 0;
            var files = 0;
            foreach (var item in entries)
            {
                if (observations.Count >= MaximumEntries)
                    return Reject("Archive contains too many entries.");
                if (item == null || !item.IsComplete ||
                    item.IsEncrypted || item.IsSplitAfter || item.IsAntiItem ||
                    item.LinkTarget != null)
                    return Reject("Archive has incomplete, encrypted, split or linked members.");
                if (item.UncompressedBytes < 0 || item.CompressedBytes < 0 ||
                    item.UncompressedBytes > MaximumFileBytes)
                    return Reject("Archive entry size exceeds the bounded limit.");
                if ((item.Attributes.HasValue && UnsafeFileAttributes(item.Attributes.Value)))
                    return Reject("Archive contains special or linked filesystem metadata.");
                if (string.IsNullOrWhiteSpace(item.Path) ||
                    item.Path.Any(character => character < 32 || character == 127))
                    return Reject("Archive member path contains control characters.");
                if (item.IsDirectory && item.UncompressedBytes != 0)
                    return Reject("Archive directory declares file data.");
                if (!item.IsDirectory)
                {
                    files++;
                    if (total > MaximumTotalBytes - item.UncompressedBytes)
                        return Reject("Archive uncompressed byte budget exceeded.");
                    total += item.UncompressedBytes;
                    // Solid compression shares bytes across entries. Do not
                    // invent per-entry compressed lengths; check the entire
                    // archive's size and decoded totals instead.
                    if (!item.IsSolid && item.UncompressedBytes > 0 &&
                        (item.CompressedBytes == 0 ||
                         item.UncompressedBytes / item.CompressedBytes > MaximumExpansionRatio))
                        return Reject("Archive entry compression ratio is unsafe.");
                }
                observations.Add(new ArchiveEntryObservation
                {
                    EntryPath = item.Path,
                    IsDirectory = item.IsDirectory,
                    IsLink = item.LinkTarget != null,
                    UncompressedBytes = item.UncompressedBytes,
                    // For solid files, this pure metadata stage cannot assign
                    // shared compressed bytes to individual entries. Use the
                    // overall archive budget as a conservative placeholder
                    // while still verifying actual output in the decoder.
                    CompressedBytes = item.IsSolid && !item.IsDirectory
                        ? archiveSizeBytes : item.CompressedBytes
                });
            }

            if (files == 0)
                return Reject("Archive contains no regular files.");
            if (archiveSizeBytes < MaximumTotalBytes / MaximumExpansionRatio &&
                total > archiveSizeBytes * MaximumExpansionRatio)
                return Reject("Archive total expansion ratio is unsafe.");

            var common = new ArchiveInspectionPolicy().Inspect(observations);
            if (!common.AllowedForStagedExtraction)
                return Reject(common.Reason);
            return new MultiFormatArchiveReview
            {
                Passed = true, FileCount = files, EntryCount = observations.Count,
                TotalUncompressedBytes = total,
                Reason = "Read-only archive metadata passed. Explicit user confirmation and verified staging are still required."
            };
        }

        private static bool UnsafeFileAttributes(int value)
        {
            // Windows FILE_ATTRIBUTE_REPARSE_POINT (0x400), and Unix file
            // types in the upper 16 bits on formats that expose POSIX mode.
            if ((value & 0x400) != 0) return true;
            var type = (value >> 16) & 0xF000;
            return type != 0 && type != 0x8000 && type != 0x4000;
        }

        private static MultiFormatArchiveReview Reject(string message)
        {
            return new MultiFormatArchiveReview { Reason = message };
        }
    }
}
