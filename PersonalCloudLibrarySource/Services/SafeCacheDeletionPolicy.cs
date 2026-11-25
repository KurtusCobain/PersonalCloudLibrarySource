using System;
using System.IO;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource
{
    public sealed class CacheDeletionAuthorization
    {
        public bool Allowed { get; set; }
        public string TargetPath { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class SafeCacheDeletionPolicy
    {
        public CacheDeletionAuthorization Authorize(string cacheRoot, string targetPath, bool allowOutsideCache)
        {
            if (string.IsNullOrWhiteSpace(targetPath)) return Refuse("uninstall target path is empty");
            string target;
            string cache;
            try
            {
                if (!IsFullyRooted(targetPath)) return Refuse("uninstall target path must be fully rooted");
                if (IsUnsupportedWindowsPath(targetPath) ||
                    (!string.IsNullOrWhiteSpace(cacheRoot) && IsUnsupportedWindowsPath(cacheRoot)))
                    return Refuse("device namespaces, alternate streams and ambiguous Windows paths are unsupported");
                if (IsVolumeOrShareRoot(targetPath)) return Refuse("uninstall target is a drive or share root");
                target = PathBoundary.Normalize(targetPath);
                cache = string.IsNullOrWhiteSpace(cacheRoot) ? string.Empty : PathBoundary.Normalize(cacheRoot);
            }
            catch (Exception ex)
            {
                return Refuse("uninstall target path could not be normalized: " + ex.Message);
            }

            if (!string.IsNullOrWhiteSpace(cache) && string.Equals(target, cache, StringComparison.OrdinalIgnoreCase))
                return Refuse("uninstall target is LocalCacheFolder itself");
            // Outside-cache opt-in never authorizes an ancestor of the cache.
            // This invariant also applies to the legacy directory executor.
            if (!string.IsNullOrWhiteSpace(cache) && PathBoundary.IsContained(target, cache))
                return Refuse("uninstall target is an ancestor of LocalCacheFolder");
            if (!allowOutsideCache && !PathBoundary.IsContained(cache, target))
                return Refuse("uninstall target is outside LocalCacheFolder");

            var reparse = FindReparsePoint(target);
            if (!string.IsNullOrWhiteSpace(reparse)) return Refuse("uninstall target or ancestor is a reparse point: " + reparse);
            return new CacheDeletionAuthorization { Allowed = true, TargetPath = target };
        }

        private static string FindReparsePoint(string target)
        {
            var current = target;
            while (!string.IsNullOrWhiteSpace(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return current;
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                catch
                {
                    // Access denied or unknown state is not proof of absence.
                    return current;
                }
                var parent = Path.GetDirectoryName(current);
                if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)) break;
                current = parent;
            }
            return string.Empty;
        }

        private static bool IsVolumeOrShareRoot(string path)
        {
            var full = Path.GetFullPath(path);
            var root = Path.GetPathRoot(full);
            return !string.IsNullOrWhiteSpace(root) && string.Equals(
                full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        // Selective adaptation of the path-hardening review in CH-Rm's MIT
        // fork (ce15ba47); keep upstream policy and avoid device/ADS aliases.
        private static bool IsUnsupportedWindowsPath(string path)
        {
            var value = path.Replace('/', '\\');
            if (value.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                value.StartsWith(@"\\.\", StringComparison.Ordinal) ||
                value.StartsWith(@"\??\", StringComparison.Ordinal) ||
                value.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase))
                return true;

            // The only permitted colon is the drive-letter separator.
            var start = value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':' ? 2 : 0;
            if (value.IndexOf(':', start) >= 0) return true;
            foreach (var segment in value.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment == "." || segment == "..") continue;
                if (segment.EndsWith(" ", StringComparison.Ordinal) ||
                    segment.EndsWith(".", StringComparison.Ordinal))
                    return true;
                var name = segment.Split('.')[0].TrimEnd(' ');
                if (System.Text.RegularExpressions.Regex.IsMatch(name,
                    @"^(?:CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                    return true;
            }
            return false;
        }

        private static bool IsFullyRooted(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return false;
            if (path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                var root = Path.GetPathRoot(path);
                return !string.IsNullOrWhiteSpace(root) && root.StartsWith(@"\\", StringComparison.Ordinal);
            }
            return path.Length >= 3 && path[1] == ':' &&
                (path[2] == Path.DirectorySeparatorChar || path[2] == Path.AltDirectorySeparatorChar);
        }

        private static CacheDeletionAuthorization Refuse(string reason) => new CacheDeletionAuthorization { Reason = reason };
    }

    public sealed class SafeCacheDeletionExecutor
    {
        private readonly SafeCacheDeletionPolicy policy;
        private readonly Action<string> deleteFile;
        private readonly Action<string> deleteDirectory;

        public SafeCacheDeletionExecutor()
            : this(new SafeCacheDeletionPolicy(), DeleteOrdinaryFileHandle, DeleteInspectedDirectory)
        {
        }

        public SafeCacheDeletionExecutor(
            SafeCacheDeletionPolicy policy,
            Action<string> deleteFile,
            Action<string> deleteDirectory)
        {
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
            this.deleteFile = deleteFile ?? throw new ArgumentNullException(nameof(deleteFile));
            this.deleteDirectory = deleteDirectory ?? throw new ArgumentNullException(nameof(deleteDirectory));
        }

        internal static string GetUnsafeDescendantReason(string root, int maxEntries = 150000)
        {
            return SafeFileTreeTraversal.Walk(root, maxEntries,
                System.Threading.CancellationToken.None, null);
        }

        private static void DeleteOrdinaryFileHandle(string path)
        {
            // The explicit file-only action authorizes a cache pathname,
            // not recursive deletion or a hardlink to a source file. Open
            // the actual leaf without following a link, verify it is an
            // ordinary single-link object, and delete that same handle.
            using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(path, true))
            {
                TransferPartialPathPolicy.CapturePrivateFileIdentity(input.SafeFileHandle);
                TransferPartialPathPolicy.DeleteVerifiedFileHandle(input.SafeFileHandle);
            }
            TransferPartialPathPolicy.RequireVacant(path);
        }

        // Legacy explicit directory API only. The shipping uninstall controller
        // still refuses folder removal. Inventory the entire ordinary tree
        // before any mutation; never recursively remove newly encountered
        // entries and never treat an inventory as a durable ownership receipt.
        private static void DeleteInspectedDirectory(string root)
        {
            var directories = new List<string> { root };
            var identities = new Dictionary<string, TransferPartialPathPolicy.FileIdentity>(
                StringComparer.OrdinalIgnoreCase);
            var files = new List<string>();
            identities.Add(root, TransferPartialPathPolicy.ObserveDirectoryIdentity(root));
            var refusal = SafeFileTreeTraversal.Walk(root, SafeFileTreeTraversal.DefaultMaxEntries,
                System.Threading.CancellationToken.None, (path, attributes) =>
                {
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        identities.Add(path, TransferPartialPathPolicy.ObserveDirectoryIdentity(path));
                        directories.Add(path);
                    }
                    else
                    {
                        // Validate deletion access and single-link identity
                        // before starting any deletion.
                        using (var stream = TransferPartialPathPolicy.OpenPrivateFileForReview(path, true))
                            identities.Add(path, TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle));
                        files.Add(path);
                    }
                });
            if (!string.IsNullOrEmpty(refusal)) throw new IOException(refusal);

            // Recheck the containing chain against the inventory. These
            // observations narrow substitution races but are not pinned
            // ancestor handles or a filesystem-wide transaction.
            Action<string> verifyParents = path =>
            {
                var parent = Path.GetDirectoryName(path);
                while (parent != null && identities.ContainsKey(parent))
                {
                    TransferPartialPathPolicy.RequireDirectoryIdentity(parent, identities[parent]);
                    if (string.Equals(parent, root, StringComparison.OrdinalIgnoreCase)) return;
                    parent = Path.GetDirectoryName(parent);
                }
                throw new IOException("Cache directory ancestry was not recorded.");
            };
            foreach (var path in files)
            {
                verifyParents(path);
                using (var stream = TransferPartialPathPolicy.OpenPrivateFileForReview(path, true))
                {
                    if (!TransferPartialPathPolicy.MatchesPrivateFileIdentity(stream.SafeFileHandle, identities[path]))
                        throw new IOException("Cache file identity changed; refusing removal.");
                    TransferPartialPathPolicy.DeleteVerifiedFileHandle(stream.SafeFileHandle);
                }
                TransferPartialPathPolicy.RequireVacant(path);
            }
            for (var i = directories.Count - 1; i >= 0; i--)
            {
                var path = directories[i];
                if (!string.Equals(path, root, StringComparison.OrdinalIgnoreCase)) verifyParents(path);
                TransferPartialPathPolicy.DeleteOwnedEmptyDirectory(path, identities[path]);
            }
        }

        // File-only permission must never fall through to recursive removal
        // when a cached file is missing or has been replaced by a directory.
        public CacheDeletionAuthorization DeleteFileOnly(string cacheRoot, string targetPath, bool allowOutsideCache)
        {
            var authorization = policy.Authorize(cacheRoot, targetPath, allowOutsideCache);
            if (!authorization.Allowed) return authorization;
            if (!File.Exists(authorization.TargetPath) || Directory.Exists(authorization.TargetPath))
                return new CacheDeletionAuthorization { Reason = "file-only uninstall requires an existing regular file" };
            // No directory callback is reachable. The production callback
            // deletes the inspected handle; injected callbacks remain for tests.
            try { deleteFile(authorization.TargetPath); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return new CacheDeletionAuthorization { Reason = "file-only removal could not be verified: " + ex.Message };
            }
            return authorization;
        }

        public CacheDeletionAuthorization Delete(string cacheRoot, string targetPath, bool allowOutsideCache)
        {
            var authorization = policy.Authorize(cacheRoot, targetPath, allowOutsideCache);
            if (!authorization.Allowed) return authorization;
            if (File.Exists(authorization.TargetPath))
                return DeleteFileOnly(cacheRoot, authorization.TargetPath, allowOutsideCache);
            else if (Directory.Exists(authorization.TargetPath))
            {
                // File.Delete/Directory.Delete recursion is not an authorization
                // to traverse descendants that were never inspected.
                try
                {
                    var refusal = GetUnsafeDescendantReason(authorization.TargetPath);
                    if (!string.IsNullOrWhiteSpace(refusal))
                        return new CacheDeletionAuthorization { Reason = refusal };
                    deleteDirectory(authorization.TargetPath);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    return new CacheDeletionAuthorization
                    {
                        Reason = "Directory removal could not be verified; some entries may already have been removed: " + ex.Message
                    };
                }
            }
            else return new CacheDeletionAuthorization { Reason = "uninstall target does not exist" };
            return authorization;
        }
    }

    // Shared bounded top-down traversal. A visitor sees only validated entries;
    // no recursive framework enumeration follows an unchecked descendant.
    internal static class SafeFileTreeTraversal
    {
        internal const int DefaultMaxEntries = 150000;

        internal static string Walk(string root, int maxEntries,
            System.Threading.CancellationToken token, Action<string, FileAttributes> visit)
        {
            var stack = new System.Collections.Generic.Stack<string>();
            stack.Push(root);
            var entries = 0;
            while (stack.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var current = stack.Pop();
                var currentAttributes = File.GetAttributes(current);
                if ((currentAttributes & FileAttributes.ReparsePoint) != 0 ||
                    (currentAttributes & FileAttributes.Directory) == 0)
                    return "cache tree contains a directory link, junction or changed entry: " + current;
                foreach (var entry in Directory.EnumerateFileSystemEntries(current, "*", SearchOption.TopDirectoryOnly))
                {
                    token.ThrowIfCancellationRequested();
                    if (++entries > maxEntries)
                        return "cache tree is too large to verify safely";
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                        return "cache tree contains a linked file or directory: " + entry;
                    if (!PathBoundary.IsContained(root, entry))
                        return "cache tree entry escapes its root";
                    visit?.Invoke(entry, attributes);
                    if ((attributes & FileAttributes.Directory) != 0)
                        stack.Push(entry);
                }
            }
            return string.Empty;
        }

        internal static bool TryMeasure(string path, bool directory,
            System.Threading.CancellationToken token, out long bytes)
        {
            bytes = 0;
            try
            {
                token.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                    ((attributes & FileAttributes.Directory) != 0) != directory)
                    return false;
                if (!directory)
                {
                    bytes = new FileInfo(path).Length;
                    return true;
                }

                long measured = 0;
                var refusal = Walk(path, DefaultMaxEntries, token, (entry, entryAttributes) =>
                {
                    if ((entryAttributes & FileAttributes.Directory) == 0)
                    {
                        // Refresh the file attributes rather than accepting a
                        // directory/link substitution between enumeration and sizing.
                        var info = new FileInfo(entry);
                        if ((info.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                            throw new IOException("Transfer file changed during verification.");
                        measured = checked(measured + info.Length);
                    }
                });
                if (!string.IsNullOrEmpty(refusal)) return false;
                bytes = measured;
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (OverflowException) { return false; }
        }
    }
}
