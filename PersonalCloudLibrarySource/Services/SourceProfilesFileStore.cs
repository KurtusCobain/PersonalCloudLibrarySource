using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PersonalCloudLibrarySource
{
    // File-store groundwork only. Does not change Playnite settings, and is
    // deliberately NOT called from the live GameLibrary plugin.
    public sealed class SourceProfilesFileStore
    {
        public const string FileName = "source-profiles.v1.json";
        private readonly SourceProfilesConfigurationCodec codec;

        public SourceProfilesFileStore()
            : this(new SourceProfilesConfigurationCodec())
        {
        }

        public SourceProfilesFileStore(SourceProfilesConfigurationCodec codec)
        {
            this.codec = codec ?? throw new ArgumentNullException(nameof(codec));
        }

        // A caller must choose an existing, trusted plugin-data directory.
        // No implicit directory creation, symlink following or auto-recovery.
        public string Save(string pluginDataDirectory,
            IReadOnlyList<PclsSourceProfileDefinition> profiles)
            => SaveCore(pluginDataDirectory, profiles, null, false);

        // Compare the editor's canonical baseline inside the same write lock
        // that covers staging and replacement, not only in the UI service.
        internal string SaveAgainstBaseline(string pluginDataDirectory,
            IReadOnlyList<PclsSourceProfileDefinition> profiles,
            IReadOnlyList<PclsSourceProfileDefinition> baseline, bool requireExisting)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            return SaveCore(pluginDataDirectory, profiles, codec.Serialize(baseline), requireExisting);
        }

        private string SaveCore(string pluginDataDirectory,
            IReadOnlyList<PclsSourceProfileDefinition> profiles, string baseline, bool requireExisting)
        {
            var root = ValidateStoreDirectory(pluginDataDirectory);
            var primary = Path.Combine(root, FileName);
            var json = codec.Serialize(profiles);
            var payload = new UTF8Encoding(false, true).GetBytes(json);
            if (payload.Length > SourceProfilesConfigurationCodec.MaxDocumentCharacters * 4)
                throw new InvalidOperationException("Source profiles file is too large.");

            using (var writeLock = TransferPartialPathPolicy.OpenPrivateRecordWriteLock(primary))
            {
                RejectLinksAndDirectories(primary);
                var replacing = File.Exists(primary);
                if (replacing && !MatchesBaseline(primary, baseline))
                    throw new InvalidOperationException(
                        "Existing source profiles are invalid or changed; reload before saving.");
                if (!replacing && requireExisting)
                    throw new InvalidOperationException("Source profiles disappeared; reload before saving.");

                if (!replacing)
                {
                    TransferPartialPathPolicy.WritePrivateRecord(primary, json,
                        SourceProfilesConfigurationCodec.MaxDocumentCharacters * 4,
                        System.Threading.CancellationToken.None);
                    return string.Empty;
                }

                var pending = Path.Combine(root, "source-profiles-" +
                    Guid.NewGuid().ToString("N") + ".tmp");
                var backup = Path.Combine(root, "source-profiles-backup-" +
                    Guid.NewGuid().ToString("N") + ".json");
                TransferPartialPathPolicy.ReplacePrivateRecord(primary, payload, pending, backup,
                    () => MatchesBaseline(primary, baseline),
                    "Existing source profiles are invalid or changed; reload before saving.",
                    SourceProfilesConfigurationCodec.MaxDocumentCharacters * 4);
                return backup;
            }
        }

        private bool MatchesBaseline(string path, string baseline)
        {
            var active = ReadValidated(path);
            return active.Succeeded && (baseline == null ||
                string.Equals(codec.Serialize(active.Profiles), baseline, StringComparison.Ordinal));
        }

        public SourceProfilesParseResult Load(string pluginDataDirectory)
        {
            var root = ValidateStoreDirectory(pluginDataDirectory);
            var path = Path.Combine(root, FileName);
            return ReadValidated(path);
        }

        // Never enumerate outside our existing plugin-data root. Any backup
        // discovered here is a candidate for inspection, not a trusted restore.
        public IReadOnlyList<string> ListBackupNames(string pluginDataDirectory)
        {
            var root = ValidateStoreDirectory(pluginDataDirectory);
            var names = Directory.EnumerateFiles(root,
                    "source-profiles-backup-*.json", SearchOption.TopDirectoryOnly)
                .Take(201).Select(Path.GetFileName).ToList();
            if (names.Count > 200)
                throw new InvalidOperationException(
                    "Too many source profile backups; review the plugin data folder.");
            // Validate strict generated filename format. Do not list links or
            // directories masquerading as backups.
            return names.Where(name => IsBackupName(name) &&
                    (File.GetAttributes(Path.Combine(root, name)) &
                        FileAttributes.ReparsePoint) == 0)
                .OrderByDescending(name => name, StringComparer.Ordinal)
                .ToList().AsReadOnly();
        }

        private static bool IsBackupName(string name)
        {
            const string prefix = "source-profiles-backup-";
            const string suffix = ".json";
            if (string.IsNullOrWhiteSpace(name) ||
                !name.StartsWith(prefix, StringComparison.Ordinal) ||
                !name.EndsWith(suffix, StringComparison.Ordinal) ||
                name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                return false;
            var token = name.Substring(prefix.Length,
                name.Length - prefix.Length - suffix.Length);
            Guid id;
            return token.Length == 32 && Guid.TryParseExact(token, "N", out id);
        }

        // Explicit backup preview, NOT rollback. The caller may present this
        // result to a user, but the implementation never auto-adopts an old
        // backup or writes an invalid current file.
        public SourceProfilesParseResult InspectBackup(
            string pluginDataDirectory, string backupFileName)
        {
            var root = ValidateStoreDirectory(pluginDataDirectory);
            if (!IsBackupName(backupFileName))
                throw new InvalidOperationException("Invalid local profile backup identity.");
            return ReadValidated(Path.Combine(root, backupFileName));
        }

        private SourceProfilesParseResult ReadValidated(string path)
        {
            RejectLinksAndDirectories(path);
            if (!File.Exists(path))
                return codec.Parse(string.Empty);
            try
            {
                using (var file = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
                    return codec.Parse(TransferPartialPathPolicy.ReadPrivateRecord(file,
                        SourceProfilesConfigurationCodec.MaxDocumentCharacters * 4));
            }
            catch (IOException) { return codec.Parse(string.Empty); }
            catch (UnauthorizedAccessException) { return codec.Parse(string.Empty); }
            catch (DecoderFallbackException) { return codec.Parse(string.Empty); }
        }

        private static string ValidateStoreDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Plugin data directory is required.", nameof(directory));
            var root = Path.GetFullPath(directory);
            var current = new DirectoryInfo(root);
            if (!current.Exists)
                throw new DirectoryNotFoundException("The plugin data directory does not exist.");
            while (current != null)
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException(
                        "Source profiles cannot be stored through a filesystem link.");
                current = current.Parent;
            }
            return root;
        }

        private static void RejectLinksAndDirectories(string path)
        {
            if (Directory.Exists(path))
                throw new InvalidOperationException(
                    "Source profiles path is occupied by a directory.");
            if (File.Exists(path) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(
                    "Source profiles path is a filesystem link.");
        }
    }
}
