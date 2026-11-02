using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    // Classification is advisory: it does not open, extract or execute input.
    public enum PackageCandidateKind
    {
        DirectFile,
        ArchiveNeedsReview,
        ExecutableNeedsReview,
        Unsupported
    }

    public sealed class PackageCandidate
    {
        public PackageCandidateKind Kind { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
        public bool CanInstallUnattended => false;
    }

    public sealed class PackageDiscoveryPolicy
    {
        private static readonly HashSet<string> Archives =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".zip", ".7z", ".rar" };
        private static readonly HashSet<string> DirectFiles =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".nes", ".sfc", ".smc", ".n64", ".z64", ".v64", ".gb",
              ".gbc", ".gba", ".iso", ".chd", ".cso", ".rvz", ".gcz",
              ".pbp", ".xci", ".nsp", ".3ds" };
        private static readonly HashSet<string> Executables =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".exe", ".bat", ".cmd", ".msi" };

        public PackageCandidate Classify(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return New(PackageCandidateKind.Unsupported, "Package filename is required.");
            var extension = Path.GetExtension(filename);
            if (Archives.Contains(extension))
                return New(PackageCandidateKind.ArchiveNeedsReview,
                    "Archive must be inspected first; ZIP may be an emulator-ready ROM and must not be blindly extracted.");
            if (Executables.Contains(extension))
                return New(PackageCandidateKind.ExecutableNeedsReview,
                    "An executable may be a game launcher or an installer; never run it without an explicit user decision.");
            if (DirectFiles.Contains(extension))
                return New(PackageCandidateKind.DirectFile,
                    "A recognized single-file format. Its Playnite emulator/profile must still be validated.");
            return New(PackageCandidateKind.Unsupported,
                "Unknown package type; keep as catalog-only until explicitly configured.");
        }

        private static PackageCandidate New(PackageCandidateKind kind, string reason)
            => new PackageCandidate { Kind = kind, Reason = reason };
    }

    public sealed class ArchiveEntryObservation
    {
        // Taken from an archive parser; not a trusted destination on disk.
        public string EntryPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public bool IsLink { get; set; }
        public long UncompressedBytes { get; set; }
        public long CompressedBytes { get; set; }
    }

    public sealed class ArchiveInspectionResult
    {
        public bool AllowedForStagedExtraction { get; internal set; }
        public long TotalUncompressedBytes { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
        // Approval for extraction still requires a separate user choice,
        // staging, cancellation, collision-safe filesystem publishing and verification.
        public bool AuthorizesFilesystemWrite => false;
    }

    // Pure, fail-closed planning gate. NO filesystem reads/writes or extraction.
    // Archive handlers must inspect parser metadata then enforce these checks
    // again during extraction and on the actual destination handles.
    public sealed class ArchiveInspectionPolicy
    {
        public const int MaxEntries = 100000;
        public const long MaxTotalBytes = 32L * 1024 * 1024 * 1024;
        public const long MaxEntryBytes = 8L * 1024 * 1024 * 1024;
        public const int MaxPathCharacters = 240;
        public const long MaxExpansionRatio = 1000;

        public ArchiveInspectionResult Inspect(IEnumerable<ArchiveEntryObservation> input)
        {
            if (input == null)
                return Refuse("Archive entry list is unavailable.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            var count = 0;
            foreach (var entry in input)
            {
                if (++count > MaxEntries) return Refuse("Archive contains too many entries.");
                if (entry == null || entry.IsLink)
                    return Refuse("Archive has a missing or linked entry.");
                var path = (entry.EntryPath ?? string.Empty).Replace('/', '\\');
                // ZIP directory entries conventionally end with one separator.
                if (entry.IsDirectory && path.EndsWith("\\", StringComparison.Ordinal))
                    path = path.Substring(0, path.Length - 1);
                if (path.Length == 0 || path.Length > MaxPathCharacters ||
                    Path.IsPathRooted(path) || path.StartsWith("\\", StringComparison.Ordinal) ||
                    path.Contains(":") || path.Contains("\0"))
                    return Refuse("Archive contains an unsafe or absolute entry path.");

                var segments = path.Split('\\');
                if (segments.Any(segment =>
                    string.IsNullOrEmpty(segment) || segment == "." || segment == ".." ||
                    segment.EndsWith(" ", StringComparison.Ordinal) ||
                    segment.EndsWith(".", StringComparison.Ordinal) ||
                    segment.IndexOfAny(new[] { '<', '>', '"', '|', '?', '*' }) >= 0 ||
                    IsReservedWindowsDevice(segment)))
                    return Refuse("Archive contains traversal, reserved names or ambiguous path segments.");

                if (!seen.Add(path.TrimEnd('\\')))
                    return Refuse("Archive has colliding entry paths on Windows.");
                if (entry.UncompressedBytes < 0 || entry.CompressedBytes < 0 ||
                    entry.UncompressedBytes > MaxEntryBytes)
                    return Refuse("Archive entry size is invalid or unbounded.");
                if (entry.IsDirectory && entry.UncompressedBytes != 0)
                    return Refuse("Archive directory entry has unexpected file bytes.");
                if (!entry.IsDirectory)
                {
                    if (entry.UncompressedBytes > 0 &&
                        (entry.CompressedBytes == 0 ||
                         entry.UncompressedBytes / entry.CompressedBytes > MaxExpansionRatio))
                        return Refuse("Archive expands excessively compared with compressed bytes.");
                    if (total > MaxTotalBytes - entry.UncompressedBytes)
                        return Refuse("Archive uncompressed size exceeds the configured limit.");
                    total += entry.UncompressedBytes;
                }
            }
            if (count == 0) return Refuse("Archive has no entries.");
            return new ArchiveInspectionResult
            {
                AllowedForStagedExtraction = true,
                TotalUncompressedBytes = total,
                Reason = "Archive metadata passed a read-only preflight; extraction is not authorized."
            };
        }

        private static bool IsReservedWindowsDevice(string segment)
        {
            var name = segment.Split('.')[0];
            if (name.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("NUL", StringComparison.OrdinalIgnoreCase))
                return true;
            if (name.Length == 4 &&
                (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                 name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                name[3] >= '1' && name[3] <= '9')
                return true;
            return false;
        }

        private static ArchiveInspectionResult Refuse(string reason)
            => new ArchiveInspectionResult { Reason = reason };
    }
}
