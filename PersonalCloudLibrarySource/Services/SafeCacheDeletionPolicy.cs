using System;
using System.IO;

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
            if (!IsFullyRooted(targetPath)) return Refuse("uninstall target path must be fully rooted");
            string target;
            string cache;
            try
            {
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
                    if ((File.Exists(current) || Directory.Exists(current)) &&
                        (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return current;
                }
                catch
                {
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
            : this(new SafeCacheDeletionPolicy(), File.Delete, path => Directory.Delete(path, true))
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

        public CacheDeletionAuthorization Delete(string cacheRoot, string targetPath, bool allowOutsideCache)
        {
            var authorization = policy.Authorize(cacheRoot, targetPath, allowOutsideCache);
            if (!authorization.Allowed) return authorization;
            if (File.Exists(authorization.TargetPath)) deleteFile(authorization.TargetPath);
            else if (Directory.Exists(authorization.TargetPath)) deleteDirectory(authorization.TargetPath);
            else return new CacheDeletionAuthorization { Reason = "uninstall target does not exist" };
            return authorization;
        }
    }
}
