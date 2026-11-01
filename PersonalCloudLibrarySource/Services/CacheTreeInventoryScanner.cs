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
            var pending = new Stack<string>();
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
                    if (entries.Count >= MaximumEntries)
                        throw new IOException("Inventory entry limit exceeded.");
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Inventory refuses linked files and directories.");
                    var relative = entry.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        result.DirectoryCount++;
                        entries.Add("D:" + relative);
                        pending.Push(entry);
                    }
                    else
                    {
                        var info = new FileInfo(entry);
                        if (info.Length > MaximumFileBytes)
                            throw new IOException("Inventory file exceeds size limit.");
                        result.FileCount++;
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
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(string.Join("\n", entries));
                result.Fingerprint = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            }
            return result;
        }
    }
}
