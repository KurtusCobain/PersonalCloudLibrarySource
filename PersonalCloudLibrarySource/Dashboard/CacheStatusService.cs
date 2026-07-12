using System;

namespace PersonalCloudLibrarySource
{
    public sealed class CacheStatusSnapshot
    {
        public CacheStatusSnapshot(
            string cachePath,
            int cachedGameCount,
            long cacheSizeBytes,
            long freeSpaceBytes,
            bool isAvailable,
            bool isWritable)
        {
            CachePath = cachePath ?? string.Empty;
            CachedGameCount = Math.Max(0, cachedGameCount);
            CacheSizeBytes = Math.Max(0, cacheSizeBytes);
            FreeSpaceBytes = Math.Max(0, freeSpaceBytes);
            IsAvailable = isAvailable;
            IsWritable = isWritable;
        }

        public string CachePath { get; }
        public int CachedGameCount { get; }
        public long CacheSizeBytes { get; }
        public long FreeSpaceBytes { get; }
        public bool IsAvailable { get; }
        public bool IsWritable { get; }
    }

    public sealed class CacheStatusService
    {
        public CacheStatusSnapshot BuildStatus(
            string cachePath,
            int cachedGameCount,
            long cacheSizeBytes,
            long freeSpaceBytes,
            bool isAvailable,
            bool isWritable)
        {
            return new CacheStatusSnapshot(
                cachePath,
                cachedGameCount,
                cacheSizeBytes,
                freeSpaceBytes,
                isAvailable,
                isWritable);
        }
    }
}
