using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PersonalCloudLibrarySource
{
    // Display-only evidence, never a GameMetadata result or a controller route.
    public sealed class SourceCatalogDisplayEntry
    {
        public string GameId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public sealed class SourceCatalogDisplaySnapshot
    {
        private readonly ReadOnlyCollection<SourceCatalogDisplayEntry> entries;

        internal SourceCatalogDisplaySnapshot(string profileId, string fingerprint,
            IEnumerable<SourceCatalogDisplayEntry> source)
        {
            ProfileId = profileId;
            BindingFingerprint = fingerprint;
            entries = new ReadOnlyCollection<SourceCatalogDisplayEntry>(
                source.Select(x => new SourceCatalogDisplayEntry
                {
                    GameId = x.GameId, Name = x.Name, Description = x.Description
                }).ToList());
        }

        public string ProfileId { get; }
        internal string BindingFingerprint { get; }
        // Reconstruct copies so callers cannot change validated state.
        public IReadOnlyList<SourceCatalogDisplayEntry> Entries =>
            new ReadOnlyCollection<SourceCatalogDisplayEntry>(
                entries.Select(x => new SourceCatalogDisplayEntry
                {
                    GameId = x.GameId, Name = x.Name, Description = x.Description
                }).ToList());
    }

    public sealed class SourceCatalogDisplayRead
    {
        public bool Succeeded { get; internal set; }
        public SourceCatalogDisplaySnapshot Snapshot { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
    }

    internal sealed class SourceCatalogDisplayDocument
    {
        public int SchemaVersion { get; set; }
        public string ProfileId { get; set; }
        public string BindingFingerprint { get; set; }
        public List<SourceCatalogDisplayEntry> Entries { get; set; }
        public string IntegritySha256 { get; set; }
    }

    // Explicit persistence only. Not called by GetGames, install or uninstall.
    // SHA-256 catches corruption; it cannot authenticate a malicious local editor.
    public sealed class SourceCatalogDisplaySnapshotStore
    {
        public const int SchemaVersion = 1;
        public const int MaxBytes = 4 * 1024 * 1024;
        private const int MaxNameLength = 512;
        private const int MaxDescriptionLength = 4096;
        private const int MaxIdLength = 256;
        private const string FilePrefix = "source-catalog-display-";
        private const string FileSuffix = ".v1.json";

        // Only a complete, successfully mapped source is eligible for capture.
        // Validate the current identities through the existing source policy,
        // without trusting a stored file or accepting a partial mapping.
        public SourceCatalogDisplaySnapshot CaptureSuccessful(
            PclsSourceProfileDefinition profile,
            PersonalCloudLibrarySourceSettingsV3 shared,
            ImportOutcome successfulImport)
        {
            var identity = new SourceCatalogContinuityPolicy()
                .CaptureSuccessful(profile, shared, successfulImport);
            var mapped = successfulImport.Games.ToDictionary(x => x.GameId,
                StringComparer.OrdinalIgnoreCase);
            var naming = new SourceProfileIdentityPolicy();
            var entries = new List<SourceCatalogDisplayEntry>();
            foreach (var item in successfulImport.ValidItems)
            {
                var game = mapped[item.Id];
                entries.Add(new SourceCatalogDisplayEntry
                {
                    GameId = naming.GameId(profile, item.Id),
                    Name = game.Name, Description = game.Description ?? string.Empty
                });
            }
            if (!ValidEntries(entries) ||
                !new HashSet<string>(entries.Select(e => e.GameId),
                    StringComparer.OrdinalIgnoreCase).SetEquals(identity.GameIds))
                throw new InvalidOperationException("Catalog display evidence is invalid.");
            return new SourceCatalogDisplaySnapshot(
                identity.ProfileId, identity.BindingFingerprint, entries);
        }

        public string Save(string pluginDataDirectory, SourceCatalogDisplaySnapshot snapshot)
        {
            var root = ValidateRoot(pluginDataDirectory);
            var doc = ToDocument(snapshot);
            var target = Path.Combine(root, FileName(doc.ProfileId));
            RejectLinkedOrDirectory(target);
            if (File.Exists(target) && !ReadFile(target, doc.ProfileId).Succeeded)
                throw new InvalidOperationException("Refusing to overwrite invalid display evidence.");
            var bytes = new UTF8Encoding(false, true).GetBytes(Serializer().Serialize(doc));
            if (bytes.Length > MaxBytes)
                throw new InvalidOperationException("Display evidence exceeds the size limit.");
            var pending = Path.Combine(root, FilePrefix +
                Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(pending, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                RejectLinkedOrDirectory(target);
                if (File.Exists(target))
                {
                    var backup = Path.Combine(root, FilePrefix + "backup-" +
                        Guid.NewGuid().ToString("N") + ".json");
                    File.Replace(pending, target, backup);
                }
                else
                    File.Move(pending, target);
                return target;
            }
            finally
            {
                if (File.Exists(pending))
                {
                    try
                    {
                        if ((File.GetAttributes(pending) & FileAttributes.ReparsePoint) == 0)
                            File.Delete(pending);
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        public SourceCatalogDisplayRead Load(string pluginDataDirectory, string profileId)
            => ReadFile(Path.Combine(ValidateRoot(pluginDataDirectory),
                FileName(profileId)), CanonicalId(profileId));

        // Neither identity nor display data may independently authorize stale
        // catalog replay. They must agree on profile, binding, and exact IDs.
        public bool MatchesIdentity(SourceCatalogDisplaySnapshot display,
            SourceCatalogIdentitySnapshot identities)
        {
            return display != null && identities != null &&
                string.Equals(display.ProfileId, identities.ProfileId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(display.BindingFingerprint, identities.BindingFingerprint,
                    StringComparison.Ordinal) &&
                new HashSet<string>(display.Entries.Select(e => e.GameId),
                    StringComparer.OrdinalIgnoreCase).SetEquals(identities.GameIds) &&
                display.Entries.Count == identities.GameCount;
        }

        private static SourceCatalogDisplayDocument ToDocument(SourceCatalogDisplaySnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            var id = CanonicalId(snapshot.ProfileId);
            var entries = snapshot.Entries.ToList();
            if (!ValidFingerprint(snapshot.BindingFingerprint) || !ValidEntries(entries))
                throw new InvalidOperationException("Invalid display snapshot.");
            return new SourceCatalogDisplayDocument
            {
                SchemaVersion = SchemaVersion, ProfileId = id,
                BindingFingerprint = snapshot.BindingFingerprint,
                Entries = entries,
                IntegritySha256 = Digest(id, snapshot.BindingFingerprint, entries)
            };
        }

        private static SourceCatalogDisplayRead ReadFile(string path, string expectedId)
        {
            RejectLinkedOrDirectory(path);
            if (!File.Exists(path))
                return Refuse("No display snapshot.");
            try
            {
                using (var stream = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length == 0 || stream.Length > MaxBytes)
                        return Refuse("Display snapshot size invalid.");
                    using (var reader = new StreamReader(stream,
                        new UTF8Encoding(false, true), false))
                    {
                        var json = reader.ReadToEnd();
                        var doc = Serializer().Deserialize<SourceCatalogDisplayDocument>(json);
                        if (doc == null || doc.SchemaVersion != SchemaVersion ||
                            !string.Equals(doc.ProfileId, expectedId, StringComparison.Ordinal) ||
                            !ValidFingerprint(doc.BindingFingerprint) ||
                            !ValidEntries(doc.Entries) ||
                            !string.Equals(doc.IntegritySha256,
                                Digest(doc.ProfileId, doc.BindingFingerprint, doc.Entries),
                                StringComparison.Ordinal))
                            return Refuse("Display snapshot schema or integrity invalid.");
                        return new SourceCatalogDisplayRead
                        {
                            Succeeded = true,
                            Snapshot = new SourceCatalogDisplaySnapshot(
                                doc.ProfileId, doc.BindingFingerprint, doc.Entries)
                        };
                    }
                }
            }
            catch (ArgumentException) { return Refuse("Display snapshot invalid."); }
            catch (InvalidOperationException) { return Refuse("Display snapshot invalid."); }
        }

        private static bool ValidEntries(IList<SourceCatalogDisplayEntry> entries)
        {
            if (entries == null || entries.Count > SourceCatalogSnapshotStore.MaxGameCount)
                return false;
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (entry == null || !ValidText(entry.GameId, MaxIdLength, true) ||
                    !ValidText(entry.Name, MaxNameLength, true) ||
                    entry.Description == null ||
                    entry.Description.Length > MaxDescriptionLength ||
                    !ids.Add(entry.GameId))
                    return false;
            }
            return true;
        }

        private static bool ValidText(string value, int limit, bool required)
            => value != null && value.Length <= limit &&
                (!required || !string.IsNullOrWhiteSpace(value)) &&
                !value.Any(char.IsControl);

        private static bool ValidFingerprint(string value)
            => value != null && value.Length == 64 &&
                value.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'));

        private static string Digest(string id, string fingerprint,
            IEnumerable<SourceCatalogDisplayEntry> entries)
        {
            var input = new StringBuilder("pcls-display-v1;");
            Append(input, id);
            Append(input, fingerprint);
            foreach (var entry in entries)
            {
                Append(input, entry.GameId);
                Append(input, entry.Name);
                Append(input, entry.Description);
            }
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(input.ToString()))).Replace("-", "");
        }

        private static void Append(StringBuilder builder, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            builder.Append(bytes.Length).Append(':');
            builder.Append(Convert.ToBase64String(bytes)).Append(';');
        }

        private static SourceCatalogDisplayRead Refuse(string reason)
            => new SourceCatalogDisplayRead { Reason = reason };

        private static string CanonicalId(string id)
        {
            Guid parsed;
            if (!Guid.TryParseExact(id, "D", out parsed) || parsed == Guid.Empty)
                throw new InvalidOperationException("Canonical source GUID required.");
            return parsed.ToString("D");
        }

        private static string FileName(string id)
            => FilePrefix + CanonicalId(id).Replace("-", "") + FileSuffix;

        private static JavaScriptSerializer Serializer() => new JavaScriptSerializer
        {
            MaxJsonLength = MaxBytes, RecursionLimit = 12
        };

        private static string ValidateRoot(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Existing plugin directory is required.");
            var root = Path.GetFullPath(directory);
            var current = new DirectoryInfo(root);
            if (!current.Exists)
                throw new DirectoryNotFoundException("Plugin directory does not exist.");
            while (current != null)
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Linked plugin storage refused.");
                current = current.Parent;
            }
            return root;
        }

        private static void RejectLinkedOrDirectory(string path)
        {
            if (Directory.Exists(path))
                throw new InvalidOperationException("Display path is occupied by a directory.");
            if (File.Exists(path) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Linked display snapshot refused.");
        }
    }
}
