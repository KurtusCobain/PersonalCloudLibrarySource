using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public enum ZipPackageReviewKind
    {
        Rejected,
        KeepArchiveCandidate,
        ExtractCandidate,
        Ambiguous
    }

    // A read-only, source-file-bound review, not authorization to extract.
    public sealed class ZipPackageReview
    {
        internal ZipPackageReview() { }
        public bool PassedPreflight { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
        public ZipPackageReviewKind Kind { get; internal set; } = ZipPackageReviewKind.Rejected;
        public int FileCount { get; internal set; }
        public long TotalUncompressedBytes { get; internal set; }
        public IReadOnlyList<string> MemberPaths { get; internal set; }
            = new string[0];
        public LauncherSelectionPreview Launchers { get; internal set; }
            = new LauncherSelectionPreview();
        public bool RequiresUserChoice => true;
        public bool CanExtractUnattended => false;

        // Bind an explicit user choice to the inspected bytes and destination.
        // Do not disclose these values as user-visible logs or error messages.
        internal string NormalizedArchivePath { get; set; }
        internal string ArchiveSha256 { get; set; }
    }

    // The Playnite UI must obtain the user's *explicit* choice of extraction.
    // This service never presents a dialog, launches software or writes files.
    public sealed class ZipPackageReviewService
    {
        public ZipPackageReview Inspect(string path, string gameTitle,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var rejected = new ZipPackageReview
            {
                Reason = "ZIP requires a validated local archive and explicit review."
            };
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(path) ||
                    !string.Equals(Path.GetExtension(path), ".zip",
                        StringComparison.OrdinalIgnoreCase))
                    return rejected;
                var normalized = Path.GetFullPath(path);
                if (!File.Exists(normalized) ||
                    (File.GetAttributes(normalized) & FileAttributes.ReparsePoint) != 0)
                    return rejected;

                using (var source = TransferPartialPathPolicy.OpenArchiveSource(normalized))
                {
                    var central = ZipCentralDirectoryInspector.Read(source);
                    cancellationToken.ThrowIfCancellationRequested();
                    source.Position = 0;
                    using (var archive = new ZipArchive(source, ZipArchiveMode.Read, true))
                    {
                        if (central.Count != archive.Entries.Count)
                            return Fail("ZIP entry count disagrees with its central directory.");
                        var observations = new List<ArchiveEntryObservation>();
                        var memberPaths = new List<string>();
                        for (var index = 0; index < archive.Entries.Count; index++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var entry = archive.Entries[index];
                            var header = central[index];
                            if (!string.Equals(entry.FullName, header.Name,
                                StringComparison.Ordinal))
                                return Fail("ZIP metadata contains inconsistent filenames.");
                            var directory = entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                                entry.FullName.EndsWith("\\", StringComparison.Ordinal);
                            observations.Add(new ArchiveEntryObservation
                            {
                                EntryPath = entry.FullName,
                                IsDirectory = directory,
                                IsLink = header.IsUnsafeLinkOrSpecialFile,
                                CompressedBytes = entry.CompressedLength,
                                UncompressedBytes = entry.Length
                            });
                            if (!directory)
                                memberPaths.Add(entry.FullName);
                        }
                        var inspection = new ArchiveInspectionPolicy().Inspect(observations);
                        if (!inspection.AllowedForStagedExtraction)
                            return Fail(inspection.Reason);
                        if (memberPaths.Count == 0)
                            return Fail("ZIP has no regular game payload files.");

                        var launchers = new GameLauncherCandidatePolicy()
                            .Preview(memberPaths, gameTitle);
                        // ROM inside ZIP is not proof the Playnite emulator accepts ZIP.
                        // Never convert the heuristic into unattended handling.
                        var direct = new PackageDiscoveryPolicy();
                        var romFiles = memberPaths.Count(name =>
                            direct.Classify(name).Kind == PackageCandidateKind.DirectFile);
                        var kind = memberPaths.Count == 1 && romFiles == 1
                            ? ZipPackageReviewKind.KeepArchiveCandidate
                            : launchers.Candidates.Count > 0
                                ? ZipPackageReviewKind.ExtractCandidate
                                : ZipPackageReviewKind.Ambiguous;

                        // Hash the same read-only handle that was inspected.
                        // Revalidated by the approval-bound entrypoint.
                        source.Position = 0;
                        var digest = HashSource(source, cancellationToken);

                        return new ZipPackageReview
                        {
                            PassedPreflight = true,
                            Kind = kind,
                            FileCount = memberPaths.Count,
                            TotalUncompressedBytes = inspection.TotalUncompressedBytes,
                            MemberPaths = memberPaths.AsReadOnly(),
                            Launchers = launchers,
                            Reason = kind == ZipPackageReviewKind.KeepArchiveCandidate
                                ? "This ZIP may be loadable directly by an emulator; choose keep or extract."
                                : "Archive passed metadata review; explicitly choose whether to extract.",
                            NormalizedArchivePath = normalized,
                            ArchiveSha256 = digest
                        };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return Fail("ZIP review was cancelled.");
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException ||
                ex is UnauthorizedAccessException || ex is ArgumentException ||
                ex is NotSupportedException)
            {
                // Never return private source paths or exception details.
                return Fail("ZIP cannot be safely reviewed or uses unsupported features.");
            }
        }

        public CloudTransferExecutionResult ExtractAfterExplicitChoice(
            ZipPackageReview reviewed, bool userConfirmedExtract,
            string archivePath, string cacheRoot, string destinationDirectory,
            Guid jobId, CancellationToken cancellationToken,
            Action<long, long?> progress = null)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!userConfirmedExtract || reviewed == null ||
                    !reviewed.PassedPreflight || reviewed.Kind == ZipPackageReviewKind.Rejected ||
                    string.IsNullOrWhiteSpace(reviewed.NormalizedArchivePath) ||
                    string.IsNullOrWhiteSpace(reviewed.ArchiveSha256))
                    return CloudTransferExecutionResult.Failure(
                        "Explicit extraction choice and verified ZIP review are required.");
                if (string.IsNullOrWhiteSpace(archivePath) ||
                    !string.Equals(Path.GetFullPath(archivePath),
                        reviewed.NormalizedArchivePath, StringComparison.OrdinalIgnoreCase))
                    return CloudTransferExecutionResult.Failure(
                        "The selected ZIP differs from the reviewed source.");
                if (!File.Exists(archivePath) ||
                    (File.GetAttributes(archivePath) & FileAttributes.ReparsePoint) != 0)
                    return CloudTransferExecutionResult.Failure(
                        "ZIP source is unavailable or linked.");
                // Keep the same read-only, no-write/no-delete-shared handle
                // open through the extractor's full read. On Windows this
                // prevents a normal concurrent writer/replacement between
                // final digest verification and staged extraction.
                using (var input = TransferPartialPathPolicy.OpenArchiveSource(archivePath))
                {
                    var current = HashSource(input, cancellationToken);
                    if (!string.Equals(current, reviewed.ArchiveSha256,
                        StringComparison.Ordinal))
                        return CloudTransferExecutionResult.Failure(
                            "ZIP changed after review; inspect it again.");
                    cancellationToken.ThrowIfCancellationRequested();
                    // Consume the SAME inspected source stream, not a second
                    // lookup of an archive path that may have changed.
                    input.Position = 0;
                    return new ZipPackageStagingExtractor().ExtractVerifiedZip(
                        archivePath, cacheRoot, destinationDirectory,
                        jobId, cancellationToken, progress, input);
                }
            }
            catch (OperationCanceledException)
            {
                return CloudTransferExecutionResult.CancelledResult();
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is NotSupportedException)
            {
                return CloudTransferExecutionResult.Failure(
                    "Approved ZIP is unavailable or could not be verified.");
            }
        }

        private static string HashSource(Stream source, CancellationToken token)
        {
            var remaining = source.Length - source.Position;
            if (remaining < 0) throw new IOException("Archive position is invalid.");
            using (var sha = SHA256.Create())
            {
                var buffer = new byte[65536];
                while (remaining > 0)
                {
                    token.ThrowIfCancellationRequested();
                    var count = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (count == 0) throw new IOException("Archive changed during hashing.");
                    sha.TransformBlock(buffer, 0, count, buffer, 0);
                    remaining -= count;
                }
                token.ThrowIfCancellationRequested();
                if (source.ReadByte() != -1) throw new IOException("Archive grew during hashing.");
                sha.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha.Hash).Replace("-", "");
            }
        }

        private static ZipPackageReview Fail(string reason)
            => new ZipPackageReview { Reason = reason };
    }
}
