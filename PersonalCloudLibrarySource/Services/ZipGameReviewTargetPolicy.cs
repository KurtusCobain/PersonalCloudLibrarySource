using Playnite.SDK.Models;
using System;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public sealed class ZipGameReviewTarget
    {
        public string ArchivePath { get; internal set; } = string.Empty;
        public bool FromLocalCache { get; internal set; }
        public bool Available => !string.IsNullOrWhiteSpace(ArchivePath);
    }

    // Read-only menu eligibility: no extraction approval and no installation.
    // A remote source is never silently fetched for this preview.
    public sealed class ZipGameReviewTargetPolicy
    {
        public ZipGameReviewTarget Resolve(GameCommandTarget target,
            PersonalCloudLibrarySourceSettings settings, Guid pluginId)
        {
            var refused = new ZipGameReviewTarget();
            if (target?.Game == null || target.Item == null || settings == null ||
                !settings.Enabled || pluginId == Guid.Empty ||
                target.Game.PluginId != pluginId ||
                string.IsNullOrWhiteSpace(target.Game.GameId) ||
                !string.Equals(target.Game.GameId, target.Item.Id,
                    StringComparison.OrdinalIgnoreCase) ||
                PersonalCloudLibrarySource.IsDirectoryItem(target.Item))
                return refused;

            // A cached ZIP is available even when a remote source is offline.
            // Never trust an arbitrary externally supplied path as a cache file.
            var cachePath = target.CacheDisplayPath;
            if (IsReadableZip(cachePath) &&
                !string.IsNullOrWhiteSpace(settings.LocalCacheFolder) &&
                PathBoundary.IsContained(settings.LocalCacheFolder, cachePath) &&
                !string.Equals(PathBoundary.Normalize(settings.LocalCacheFolder),
                    PathBoundary.Normalize(cachePath), StringComparison.OrdinalIgnoreCase))
                return new ZipGameReviewTarget
                {
                    ArchivePath = Path.GetFullPath(cachePath),
                    FromLocalCache = true
                };

            // Local manifest providers can point to ZIPs outside the cache.
            // Reading is permitted; these paths must never be extraction targets.
            var provider = PersonalCloudLibrarySource.GetProviderType(settings);
            if (string.Equals(provider,
                    PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                    StringComparison.OrdinalIgnoreCase))
                return refused;

            var sourcePath = target.ResolvedLocalSourcePath;
            if (IsReadableZip(sourcePath))
                return new ZipGameReviewTarget
                {
                    ArchivePath = Path.GetFullPath(sourcePath),
                    FromLocalCache = false
                };
            return refused;
        }

        private static bool IsReadableZip(string path)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(path) &&
                    string.Equals(Path.GetExtension(path), ".zip",
                        StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(path) &&
                    (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
            }
            catch (Exception ex) when (ex is ArgumentException ||
                                       ex is IOException ||
                                       ex is UnauthorizedAccessException ||
                                       ex is NotSupportedException)
            {
                return false;
            }
        }
    }
}
