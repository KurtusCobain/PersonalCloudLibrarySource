using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using SharpCompress.Archives;
using SharpCompress.Archives.SevenZip;
using PersonalCloudLibrarySource;

namespace PclsArchiveCompatibility
{
    // Does not extract content to disk. Validates that .NET Framework 4.6.2
    // can load SharpCompress netstandard2.0 dependencies and stream 7z/RAR
    // archive entries through a bounded, fail-closed read-only inspector.
    internal static class Program
    {
        private const int MaxEntries = 512;
        private const long MaxEntry = 64L * 1024 * 1024;
        private const long MaxTotal = 256L * 1024 * 1024;

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 0)
                    throw new ArgumentException("Supply at least one .7z or .rar fixture.");
                foreach (var path in args)
                    Inspect(path);
                Console.WriteLine("PCLS archive compatibility PASS: " +
                    args.Length + " real archive(s) streamed on " +
                    Environment.Version + ".");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("PCLS archive compatibility FAIL: " +
                    ex.GetType().FullName + ": " + ex.Message);
                return 1;
            }
        }

        private static void Inspect(string path)
        {
            var extension = Path.GetExtension(path);
            if (!string.Equals(extension, ".7z", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".rar", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Only explicit .7z/.rar fixtures may be tested.");
            using (var source = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read))
            {
                var signature = new byte[8];
                var read = source.Read(signature, 0, signature.Length);
                if (read < 7 || !ValidSignature(signature, extension))
                    throw new InvalidDataException("Archive signature does not match extension.");
                source.Position = 0;
                string originalDigest;
                using (var sha = SHA256.Create())
                    originalDigest = BitConverter.ToString(
                        sha.ComputeHash(source)).Replace("-", "");
                source.Position = 0;

                using (var archive = ArchiveFactory.OpenArchive(source))
                {
                    // Entire-archive metadata preflight runs BEFORE any
                    // decompressed content is streamed, using the same pure
                    // validation policy compiled into the Playnite plugin.
                    var metadata = archive.Entries.Select(entry =>
                        new MultiFormatArchiveEntry
                        {
                            Path = entry.Key,
                            IsDirectory = entry.IsDirectory,
                            IsEncrypted = entry.IsEncrypted,
                            IsComplete = entry.IsComplete,
                            IsSplitAfter = entry.IsSplitAfter,
                            IsAntiItem = entry is SevenZipArchiveEntry &&
                                ((SevenZipArchiveEntry)entry).IsAnti,
                            LinkTarget = entry.LinkTarget,
                            FirstVolume = entry.VolumeIndexFirst,
                            LastVolume = entry.VolumeIndexLast,
                            Attributes = entry.Attrib,
                            UncompressedBytes = entry.Size,
                            CompressedBytes = entry.CompressedSize,
                            IsSolid = entry.IsSolid
                        }).ToList();
                    var review = new MultiFormatArchivePreflightPolicy()
                        .Inspect(metadata, source.Length);
                    if (!review.Passed)
                        throw new InvalidDataException(
                            "Shared preflight rejected archive: " + review.Reason);
                    Console.WriteLine("PASS shared PCLS read-only preflight: " +
                        review.EntryCount + " entries, " + review.FileCount +
                        " regular files, " + review.TotalUncompressedBytes +
                        " declared bytes; extraction remains unauthorized.");

                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var entries = 0;
                    long total = 0;
                    var files = 0;
                    foreach (var entry in archive.Entries)
                    {
                        if (++entries > MaxEntries || string.IsNullOrWhiteSpace(entry.Key))
                            throw new InvalidDataException("Archive contains too many/invalid entries.");
                        var key = entry.Key.Replace('\\', '/').TrimEnd('/');
                        var parts = key.Split('/');
                        if (parts.Any(part => part.Length == 0 || part == "." ||
                            part == ".." || part.IndexOf(':') >= 0 ||
                            part.EndsWith(" ", StringComparison.Ordinal)) ||
                            key.StartsWith("/", StringComparison.Ordinal) ||
                            !seen.Add(key))
                            throw new InvalidDataException("Unsafe archive entry name.");

                        if (entry.IsDirectory)
                            continue;
                        files++;
                        if (entry.Size < 0 || entry.Size > MaxEntry ||
                            total > MaxTotal - entry.Size)
                            throw new InvalidDataException("Uncompressed data exceeds test bounds.");

                        long written = 0;
                        using (var input = entry.OpenEntryStream())
                        using (var sha = SHA256.Create())
                        {
                            var buffer = new byte[65536];
                            int n;
                            while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                if (written > MaxEntry - n || total > MaxTotal - n)
                                    throw new InvalidDataException("Stream exceeds size budget.");
                                written += n;
                                total += n;
                                sha.TransformBlock(buffer, 0, n, buffer, 0);
                            }
                            sha.TransformFinalBlock(new byte[0], 0, 0);
                            if (written != entry.Size)
                                throw new InvalidDataException("Actual output length differs from header.");
                            Console.WriteLine("  " + extension + " " + key +
                                " bytes=" + written + " sha256=" +
                                BitConverter.ToString(sha.Hash).Replace("-", ""));
                        }
                    }
                    if (files == 0)
                        throw new InvalidDataException("Archive contains no regular files.");
                    if (files != review.FileCount ||
                        entries != review.EntryCount ||
                        total != review.TotalUncompressedBytes)
                        throw new InvalidDataException(
                            "Streamed payload disagrees with shared preflight.");
                    source.Position = 0;
                    using (var sha = SHA256.Create())
                    {
                        var finalDigest = BitConverter.ToString(
                            sha.ComputeHash(source)).Replace("-", "");
                        if (!string.Equals(finalDigest, originalDigest,
                                StringComparison.Ordinal))
                            throw new InvalidDataException(
                                "Source archive bytes changed during inspection.");
                    }
                    Console.WriteLine("PASS " + extension + " entries=" + entries +
                        " files=" + files + " total=" + total +
                        " source_sha256=" + originalDigest);
                }
            }
        }

        private static bool ValidSignature(byte[] bytes, string extension)
        {
            if (extension.Equals(".7z", StringComparison.OrdinalIgnoreCase))
                return bytes[0] == 0x37 && bytes[1] == 0x7A &&
                    bytes[2] == 0xBC && bytes[3] == 0xAF &&
                    bytes[4] == 0x27 && bytes[5] == 0x1C;
            return bytes[0] == 0x52 && bytes[1] == 0x61 &&
                bytes[2] == 0x72 && bytes[3] == 0x21 &&
                bytes[4] == 0x1A && bytes[5] == 0x07 &&
                (bytes[6] == 0x00 || (bytes[6] == 0x01 && bytes[7] == 0x00));
        }
    }
}
