using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    // This is a comparison of two *observations*, not a record of file ownership.
    public enum CacheDirectoryObservedChange
    {
        Unchanged,
        Added,
        Missing,
        Modified
    }

    public sealed class CacheDirectoryObservedEntry
    {
        public string RelativePath { get; internal set; } = string.Empty;
        public CacheDirectoryObservedChange Change { get; internal set; }
        public bool WasDirectory { get; internal set; }
        public bool IsDirectory { get; internal set; }
    }

    public sealed class CacheDirectoryObservedDelta
    {
        public IReadOnlyList<CacheDirectoryObservedEntry> Entries { get; internal set; }
            = new CacheDirectoryObservedEntry[0];
        public bool HasChanges { get; internal set; }

        // A matching snapshot alone cannot prove authorization, nor establish
        // that files have not changed since the comparison.
        public bool CanDeleteAutomatically => false;
    }

    public sealed class CacheDirectoryInventoryDiffService
    {
        // Requires detailed in-memory scans from ScanDetailed. Historical V2
        // receipts contain ONLY an overall fingerprint, not per-file history.
        // Never treat this as an authenticated ownership baseline.
        public CacheDirectoryObservedDelta Compare(CacheTreeInventory earlier,
            CacheTreeInventory current)
        {
            if (earlier == null || current == null)
                throw new ArgumentNullException(
                    earlier == null ? nameof(earlier) : nameof(current));

            var previous = Map(earlier);
            var latest = Map(current);
            var names = new SortedSet<string>(previous.Keys, StringComparer.Ordinal);
            names.UnionWith(latest.Keys);
            var changes = new List<CacheDirectoryObservedEntry>();

            foreach (var name in names)
            {
                CacheTreeInventoryEntry before;
                CacheTreeInventoryEntry after;
                var hadBefore = previous.TryGetValue(name, out before);
                var hasAfter = latest.TryGetValue(name, out after);
                var change = !hadBefore ? CacheDirectoryObservedChange.Added
                    : !hasAfter ? CacheDirectoryObservedChange.Missing
                    : !Identical(before, after) ? CacheDirectoryObservedChange.Modified
                    : CacheDirectoryObservedChange.Unchanged;

                changes.Add(new CacheDirectoryObservedEntry
                {
                    RelativePath = name,
                    WasDirectory = hadBefore && before.IsDirectory,
                    IsDirectory = hasAfter && after.IsDirectory,
                    Change = change
                });
            }
            return new CacheDirectoryObservedDelta
            {
                Entries = new ReadOnlyCollection<CacheDirectoryObservedEntry>(changes),
                HasChanges = changes.Any(entry =>
                    entry.Change != CacheDirectoryObservedChange.Unchanged)
            };
        }

        private static Dictionary<string, CacheTreeInventoryEntry> Map(
            CacheTreeInventory snapshot)
        {
            var details = snapshot.DetailedEntries ??
                new CacheTreeInventoryEntry[0];
            if (details.Count != snapshot.FileCount + (long)snapshot.DirectoryCount)
                throw new InvalidOperationException(
                    "Detailed snapshot is missing entries; use ScanDetailed.");

            var map = new Dictionary<string, CacheTreeInventoryEntry>(
                StringComparer.Ordinal);
            foreach (var entry in details)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.RelativePath) ||
                    entry.RelativePath.StartsWith("..", StringComparison.Ordinal) ||
                    entry.RelativePath[0] == '/' || entry.RelativePath[0] == '\\' ||
                    entry.RelativePath.IndexOf(':') >= 0 ||
                    !map.TryAdd(entry.RelativePath, entry))
                    throw new InvalidOperationException(
                        "Detailed snapshot has invalid or duplicate relative paths.");
            }
            return map;
        }

        private static bool Identical(CacheTreeInventoryEntry a, CacheTreeInventoryEntry b)
        {
            return a.IsDirectory == b.IsDirectory &&
                (a.IsDirectory ||
                    (a.FileBytes == b.FileBytes &&
                     string.Equals(a.Sha256, b.Sha256, StringComparison.Ordinal)));
        }
    }
}
