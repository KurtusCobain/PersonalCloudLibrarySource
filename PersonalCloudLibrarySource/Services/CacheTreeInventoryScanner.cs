using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    // Read-only snapshot for future provenance comparisons. Never authorizes deletion.
    public sealed class CacheTreeInventory
    {
        public int FileCount { get; set; }
        public int DirectoryCount { get; set; }
        public string Fingerprint { get; set; } = string.Empty;
        // Paths present in the original snapshot. Never a deletion grant.
        public IReadOnlyList<string> EntryPaths { get; set; } = new string[0];
        public int IgnoredAddedSaveCount { get; set; }
        public int IgnoredAddedDirectoryCount { get; set; }
    }

    public sealed class CacheTreeInventoryScanner
    {
        public const int MaximumEntries = 150000;
        public const long MaximumFileBytes = 4L * 1024 * 1024 * 1024;

        public CacheTreeInventory Scan(string cacheRoot, string target)
        {
            return Scan(cacheRoot, target, CancellationToken.None);
        }

        public CacheTreeInventory Scan(string cacheRoot, string target, CancellationToken cancellationToken)
        {
            return ScanCore(cacheRoot, target, null, cancellationToken);
        }

        // A baseline may permit ONLY new .sav/.save data files. It can never
        // suppress original files or executables from the receipt fingerprint.
        public CacheTreeInventory ScanWithAddedSaves(string cacheRoot, string target,
            ISet<string> originalEntryPaths, CancellationToken cancellationToken)
        {
            if (originalEntryPaths == null)
                throw new ArgumentNullException(nameof(originalEntryPaths));
            return ScanCore(cacheRoot, target, originalEntryPaths, cancellationToken);
        }

        private CacheTreeInventory ScanCore(string cacheRoot, string target,
            ISet<string> originalEntryPaths, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(cacheRoot) ||
                string.IsNullOrWhiteSpace(target) ||
                !PathBoundary.IsContained(cacheRoot, target) ||
                string.Equals(PathBoundary.Normalize(cacheRoot),
                    PathBoundary.Normalize(target), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Inventory target must be inside the cache root.");

            var authorization = new SafeCacheDeletionPolicy().Authorize(cacheRoot, target, false);
            if (!authorization.Allowed)
                throw new IOException("Unsafe inventory target: " + authorization.Reason);
            if (!Directory.Exists(target))
                throw new DirectoryNotFoundException("Inventory target directory does not exist.");

            var root = PathBoundary.Normalize(target);
            var result = new CacheTreeInventory();
            var entries = new List<string>();
            var entryPaths = new List<string>();
            var pending = new Stack<string>();
            var visitedEntries = 0;
            pending.Push(root);
            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var current = pending.Pop();
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Inventory refuses a linked directory.");
                foreach (var entry in Directory.EnumerateFileSystemEntries(current, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Count ALL observed entries, not only ones included in
                    // the immutable fingerprint. Ignored save directories must
                    // never bypass the scanner's resource bound.
                    if (++visitedEntries > MaximumEntries)
                        throw new IOException("Inventory entry limit exceeded.");
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Inventory refuses linked files and directories.");
                    var relative = entry.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
                    var isDirectory = (attributes & FileAttributes.Directory) != 0;
                    var identifier = (isDirectory ? "D:" : "F:") + relative;
                    var original = originalEntryPaths == null ||
                        originalEntryPaths.Contains(identifier);
                    if (isDirectory)
                    {
                        if (!original)
                        {
                            if (!IsAllowedNewSaveDirectory(relative) ||
                                result.IgnoredAddedDirectoryCount >= 256)
                                throw new IOException("Unrecorded directory cannot be trusted.");
                            result.IgnoredAddedDirectoryCount++;
                        }
                        if (original)
                        {
                            result.DirectoryCount++;
                            entries.Add(identifier);
                            entryPaths.Add(identifier);
                        }
                        pending.Push(entry);
                    }
                    else
                    {
                        var info = new FileInfo(entry);
                        if (info.Length > MaximumFileBytes)
                            throw new IOException("Inventory file exceeds size limit.");
                        if (!original)
                        {
                            if (!IsAllowedNewSaveFile(relative) ||
                                info.Length > 64L * 1024 * 1024 ||
                                result.IgnoredAddedSaveCount >= 1024)
                                throw new IOException("Unrecorded file is not permitted save data.");
                            result.IgnoredAddedSaveCount++;
                            continue;
                        }
                        result.FileCount++;
                        entryPaths.Add(identifier);
                        using (var sha = SHA256.Create())
                        using (var stream = new FileStream(entry, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var buffer = new byte[1024 * 1024];
                            int count;
                            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                sha.TransformBlock(buffer, 0, count, buffer, 0);
                            }
                            cancellationToken.ThrowIfCancellationRequested();
                            sha.TransformFinalBlock(new byte[0], 0, 0);
                            var digest = BitConverter.ToString(sha.Hash).Replace("-", "");
                            entries.Add("F:" + relative + ":" + info.Length + ":" + digest);
                        }
                    }
                }
            }
            entries.Sort(StringComparer.Ordinal);
            entryPaths.Sort(StringComparer.Ordinal);
            result.EntryPaths = entryPaths.AsReadOnly();
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(string.Join("\n", entries));
                result.Fingerprint = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            }
            return result;
        }

        private static bool IsAllowedNewSaveFile(string relative)
        {
            var extension = Path.GetExtension(relative);
            return string.Equals(extension, ".sav", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".save", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAllowedNewSaveDirectory(string relative)
        {
            var components = relative.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            return Array.Exists(components, segment =>
                string.Equals(segment, "saves", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "savegames", StringComparison.OrdinalIgnoreCase));
        }
    }
}
