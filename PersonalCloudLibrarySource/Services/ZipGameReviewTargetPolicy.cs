using Playnite.SDK.Models;
using System;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public sealed class ZipGameReviewTarget
    {
        public string ArchivePath { get; internal set; } = string.Empty;
        public bool FromLocalCache { get; internal set; }
        public bool RequiresExternalSourceConsent { get; internal set; }
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
            if (!string.IsNullOrWhiteSpace(settings.LocalCacheFolder) &&
                PathBoundary.IsContained(settings.LocalCacheFolder, cachePath) &&
                !string.Equals(PathBoundary.Normalize(settings.LocalCacheFolder),
                    PathBoundary.Normalize(cachePath), StringComparison.OrdinalIgnoreCase) &&
                IsReadableZip(cachePath))
                return new ZipGameReviewTarget
                {
                    ArchivePath = Path.GetFullPath(cachePath),
                    FromLocalCache = true
                };

            // External LocalFile ZIP candidates are lexical until a user
            // approves the exact path. Menu eligibility is not file presence
            // evidence and must not initiate network authentication.
            var provider = PersonalCloudLibrarySource.GetProviderType(settings);
            if (string.Equals(provider,
                    PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                    StringComparison.OrdinalIgnoreCase))
                return refused;

            var sourcePath = target.ResolvedLocalSourcePath;
            var external = SourcePathResolver.RequiresExternalLocalSourceConsent(settings, sourcePath);
            if (external ? IsZipCandidate(sourcePath) : IsReadableZip(sourcePath))
                return new ZipGameReviewTarget
                {
                    ArchivePath = Path.GetFullPath(sourcePath),
                    FromLocalCache = false,
                    RequiresExternalSourceConsent = external
                };
            return refused;
        }

        private static bool IsZipCandidate(string path)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(path) &&
                    SafeCacheDeletionPolicy.IsFullyRooted(path) &&
                    !SafeCacheDeletionPolicy.IsUnsupportedWindowsPath(path) &&
                    string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                return false;
            }
        }

        private static bool IsReadableZip(string path)
        {
            try
            {
                return IsZipCandidate(path) &&
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
