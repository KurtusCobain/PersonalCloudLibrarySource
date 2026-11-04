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
    // CI-ONLY proof of bounded 7z/RAR staging. This file is not referenced or
    // packaged by the Playnite plugin. It never publishes a game or launches
    // any archive member. Successful and cancelled stages are both removed
    // only after rechecking the exact bytes created by this invocation.
    internal static class StagingProbe
    {
        private const int MaxEntries = 512;
        private const long MaxFileBytes = 64L * 1024 * 1024;
        private const long MaxTotalBytes = 256L * 1024 * 1024;

        private sealed class OwnedFile
        {
            internal string Path;
            internal long Length;
            internal byte[] Digest;
        }

        internal enum CancellationPoint
        {
            None,
            AfterCompletedFile,
            DuringFirstFile
        }

        internal static void Run(string archivePath, string stagePath, CancellationPoint cancellationPoint)
        {
            var temp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
            if (string.IsNullOrWhiteSpace(temp))
                throw new InvalidOperationException("Staging proof is restricted to a CI runner.");
            var tempRoot = Path.GetFullPath(temp).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            var stage = Path.GetFullPath(stagePath);
            var parent = Path.GetDirectoryName(stage);
            if (!stage.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(parent) ||
                !Directory.Exists(parent) ||
                (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0 ||
                File.Exists(stage) || Directory.Exists(stage))
                throw new IOException("CI staging destination is not a new private child of the runner temporary directory.");

            var extension = Path.GetExtension(archivePath);
            if (!extension.Equals(".7z", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".rar", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Staging proof accepts only 7z and RAR fixtures.");

            var ownedFiles = new List<OwnedFile>();
            var ownedDirs = new List<string>();
            bool expectedCancellation = false;
            try
            {
                using (var source = new FileStream(archivePath, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                {
                    var signature = new byte[8];
                    if (source.Read(signature, 0, signature.Length) < 8 ||
                        !ValidSignature(signature, extension))
                        throw new InvalidDataException("Archive signature and type disagree.");
                    source.Position = 0;
                    byte[] originalDigest;
                    using (var sha = SHA256.Create())
                        originalDigest = sha.ComputeHash(source);
                    source.Position = 0;

                    using (var archive = ArchiveFactory.OpenArchive(source))
                    {
                        var volumes = archive.Volumes.Count();
                        if (!archive.IsComplete || archive.IsEncrypted || volumes != 1)
                            throw new InvalidDataException("Unsafe archive-level metadata.");
                        var entries = new List<IArchiveEntry>();
                        var observations = new List<MultiFormatArchiveEntry>();
                        foreach (var entry in archive.Entries)
                        {
                            if (entries.Count >= MaxEntries)
                                throw new InvalidDataException("Archive exceeds staging probe entry limit.");
                            entries.Add(entry);
                            observations.Add(new MultiFormatArchiveEntry
                            {
                                Path = entry.Key,
                                IsDirectory = entry.IsDirectory,
                                IsEncrypted = entry.IsEncrypted,
                                IsComplete = entry.IsComplete,
                                IsSplitAfter = entry.IsSplitAfter,
                                IsAntiItem = entry is SevenZipArchiveEntry &&
                                    ((SevenZipArchiveEntry)entry).IsAnti,
                                LinkTarget = entry.LinkTarget,
                                Attributes = entry.Attrib,
                                UncompressedBytes = entry.Size,
                                CompressedBytes = entry.CompressedSize,
                                IsSolid = entry is SevenZipArchiveEntry ||
                                    archive.IsSolid || entry.IsSolid
                            });
                        }
                        var review = new MultiFormatArchivePreflightPolicy()
                            .Inspect(observations, source.Length, volumes);
                        if (!review.Passed)
                            throw new InvalidDataException("Shared archive preflight denied stage: " + review.Reason);
                        if (review.TotalUncompressedBytes > MaxTotalBytes ||
                            observations.Any(o => !o.IsDirectory && o.UncompressedBytes > MaxFileBytes))
                            throw new InvalidDataException("Archive exceeds staging proof byte budget.");

                        // Nothing has been created before the complete metadata review.
                        Directory.CreateDirectory(stage);
                        ownedDirs.Add(stage);
                        var expectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var expectedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        long copied = 0;
                        int fileCount = 0;
                        var buffer = new byte[65536];
                        for (var i = 0; i < entries.Count; i++)
                        {
                            var item = observations[i];
                            var normalized = item.Path.Replace('\\', '/').TrimEnd('/');
                            var parts = normalized.Split('/');
                            var current = stage;
                            var directoryCount = item.IsDirectory ? parts.Length : parts.Length - 1;
                            for (var part = 0; part < directoryCount; part++)
                            {
                                current = Path.Combine(current, parts[part]);
                                var relativeDir = string.Join("/", parts.Take(part + 1));
                                expectedDirs.Add(relativeDir);
                                CreateOwnedDirectory(stage, current, ownedDirs);
                            }
                            if (item.IsDirectory)
                                continue;

                            var target = Path.Combine(current, parts[parts.Length - 1]);
                            var relativeFile = string.Join("/", parts);
                            expectedFiles.Add(relativeFile);
                            if (!WithinStage(stage, target) || File.Exists(target) ||
                                Directory.Exists(target))
                                throw new IOException("Unsafe or existing staging target.");
                            var record = new OwnedFile { Path = target };
                            using (var input = entries[i].OpenEntryStream())
                            using (var output = new FileStream(target, FileMode.CreateNew,
                                FileAccess.Write, FileShare.None))
                            using (var digest = SHA256.Create())
                            {
                                // Track a file as soon as CreateNew succeeds. If
                                // cancellation interrupts its first output chunk,
                                // cleanup must know that it was created by THIS job.
                                ownedFiles.Add(record);
                                long written = 0;
                                try
                                {
                                    int count;
                                    while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        if (written > item.UncompressedBytes - count ||
                                            written > MaxFileBytes - count ||
                                            copied > MaxTotalBytes - count)
                                            throw new InvalidDataException("Stream exceeded declared or absolute limit.");
                                        output.Write(buffer, 0, count);
                                        digest.TransformBlock(buffer, 0, count, buffer, 0);
                                        written += count;
                                        copied += count;
                                        if (cancellationPoint == CancellationPoint.DuringFirstFile &&
                                            fileCount == 0 && written > 0)
                                            throw new OperationCanceledException(
                                                "Synthetic cancellation during first file stream.");
                                    }
                                    if (written != item.UncompressedBytes)
                                        throw new InvalidDataException("Decoded size differs from inspected header.");
                                }
                                finally
                                {
                                    // Calculate a digest even when cancellation
                                    // interrupts the stream. Only exact matching
                                    // bytes can be deleted during rollback. A
                                    // short/failed write without a reliable
                                    // digest is left for manual inspection.
                                    digest.TransformFinalBlock(new byte[0], 0, 0);
                                    record.Length = written;
                                    record.Digest = digest.Hash;
                                }
                            }
                            fileCount++;
                            if (cancellationPoint == CancellationPoint.AfterCompletedFile)
                                throw new OperationCanceledException("Synthetic after-file cancellation.");
                        }
                        if (fileCount != review.FileCount || copied != review.TotalUncompressedBytes)
                            throw new InvalidDataException("Staged totals disagree with preflight.");
                        VerifyStage(stage, expectedFiles, expectedDirs, ownedFiles);
                        source.Position = 0;
                        using (var digest = SHA256.Create())
                        {
                            if (!digest.ComputeHash(source).SequenceEqual(originalDigest))
                                throw new InvalidDataException("Source archive changed while staging.");
                        }
                        Console.WriteLine("PASS CI-only staging " + extension +
                            " files=" + fileCount + " bytes=" + copied +
                            "; no publication, no launch authority.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (cancellationPoint == CancellationPoint.None || ownedFiles.Count != 1)
                    throw;
                expectedCancellation = true;
            }
            finally
            {
                // Never recursively remove a directory of uncertain origin.
                // Any changed or unowned child causes a hard failure and
                // leaves the stage for inspection rather than deleting it.
                if (ownedDirs.Count > 0)
                    CleanupVerifiedStage(stage, ownedFiles, ownedDirs);
            }
            if (cancellationPoint != CancellationPoint.None)
            {
                if (!expectedCancellation)
                    throw new InvalidDataException("Synthetic cancellation was not exercised.");
                Console.WriteLine("PASS CI-only " + cancellationPoint +
                    " cancellation; verified-owned staging cleaned.");
            }
        }

        private static bool ValidSignature(byte[] b, string extension)
        {
            if (extension.Equals(".7z", StringComparison.OrdinalIgnoreCase))
                return b[0] == 0x37 && b[1] == 0x7A && b[2] == 0xBC &&
                    b[3] == 0xAF && b[4] == 0x27 && b[5] == 0x1C;
            return b[0] == 0x52 && b[1] == 0x61 && b[2] == 0x72 &&
                b[3] == 0x21 && b[4] == 0x1A && b[5] == 0x07 &&
                (b[6] == 0x00 || (b[6] == 0x01 && b[7] == 0x00));
        }

        private static bool WithinStage(string stage, string path)
        {
            var prefix = stage.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static void CreateOwnedDirectory(string stage, string directory,
            List<string> ownedDirs)
        {
            if (!WithinStage(stage, directory))
                throw new IOException("Directory escaped stage.");
            var root = stage;
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0 ||
                File.Exists(directory))
                throw new IOException("Staging root or parent is unsafe.");
            if (Directory.Exists(directory))
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Staging directory is a reparse point.");
                return;
            }
            Directory.CreateDirectory(directory);
            ownedDirs.Add(directory);
        }

        private static void VerifyStage(string stage, HashSet<string> expectedFiles,
            HashSet<string> expectedDirs, List<OwnedFile> files)
        {
            var foundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var foundDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>();
            pending.Push(stage);
            var visited = 0;
            while (pending.Count > 0)
            {
                var parent = pending.Pop();
                if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Staging directory was replaced by a link.");
                foreach (var item in Directory.EnumerateFileSystemEntries(parent))
                {
                    if (++visited > MaxEntries * 2 || !WithinStage(stage, item))
                        throw new IOException("Staging tree exceeds allowed entries or root.");
                    var attrs = File.GetAttributes(item);
                    if ((attrs & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Staging tree contains a filesystem link.");
                    var rel = item.Substring(stage.Length).TrimStart('\\', '/')
                        .Replace('\\', '/');
                    if ((attrs & FileAttributes.Directory) != 0)
                    {
                        if (!foundDirs.Add(rel))
                            throw new IOException("Duplicate directory in staging.");
                        pending.Push(item);
                    }
                    else if (!foundFiles.Add(rel))
                        throw new IOException("Duplicate file in staging.");
                }
            }
            if (!expectedFiles.SetEquals(foundFiles) || !expectedDirs.SetEquals(foundDirs))
                throw new IOException("Staging tree differs from inspected archive.");
            foreach (var file in files)
            {
                if (!VerifiedOwnedFile(file))
                    throw new IOException("A staged file changed before publication.");
            }
        }

        private static bool VerifiedOwnedFile(OwnedFile file)
        {
            if (file.Digest == null || !File.Exists(file.Path) ||
                (File.GetAttributes(file.Path) & FileAttributes.ReparsePoint) != 0)
                return false;
            using (var input = new FileStream(file.Path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            using (var digest = SHA256.Create())
                return input.Length == file.Length &&
                    digest.ComputeHash(input).SequenceEqual(file.Digest);
        }

        private static void CleanupVerifiedStage(string stage,
            List<OwnedFile> files, List<string> dirs)
        {
            bool incomplete = false;
            foreach (var file in files.AsEnumerable().Reverse())
            {
                if (!WithinStage(stage, file.Path) || !VerifiedOwnedFile(file))
                {
                    incomplete = true;
                    continue;
                }
                try { File.Delete(file.Path); }
                catch (IOException) { incomplete = true; }
                catch (UnauthorizedAccessException) { incomplete = true; }
            }
            foreach (var dir in dirs.AsEnumerable().Reverse())
            {
                try
                {
                    if ((File.GetAttributes(dir) & FileAttributes.ReparsePoint) != 0)
                    {
                        incomplete = true;
                        continue;
                    }
                    Directory.Delete(dir, false); // Only if empty, never recursive.
                }
                catch (IOException) { incomplete = true; }
                catch (UnauthorizedAccessException) { incomplete = true; }
            }
            if (incomplete || Directory.Exists(stage))
                throw new IOException("CI stage was not verified clean; manual inspection required.");
        }
    }
}
