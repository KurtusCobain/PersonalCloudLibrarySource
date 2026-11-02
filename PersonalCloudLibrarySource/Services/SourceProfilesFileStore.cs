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
        {
            var root = ValidateStoreDirectory(pluginDataDirectory);
            var primary = Path.Combine(root, FileName);
            RejectLinksAndDirectories(primary);
            var json = codec.Serialize(profiles);
            var payload = new UTF8Encoding(false, true).GetBytes(json);
            if (payload.Length > SourceProfilesConfigurationCodec.MaxDocumentCharacters * 4)
                throw new InvalidOperationException("Source profiles file is too large.");

            var pending = Path.Combine(root, "source-profiles-" +
                Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(pending, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                }
                if (File.Exists(primary))
                {
                    // A corrupt or unknown current schema is not a license to
                    // overwrite the user's only copy. Explicit recovery first.
                    if (!ReadValidated(primary).Succeeded)
                        throw new InvalidOperationException(
                            "Existing source profiles are invalid; inspect and recover manually.");
                    // Backup name is unique: do not overwrite earlier backups or
                    // anything the user has added to this directory.
                    var backup = Path.Combine(root, "source-profiles-backup-" +
                        Guid.NewGuid().ToString("N") + ".json");
                    RejectLinksAndDirectories(primary);
                    File.Replace(pending, primary, backup);
                    return backup;
                }
                RejectLinksAndDirectories(primary);
                File.Move(pending, primary);
                return string.Empty;
            }
            finally
            {
                // Only a uniquely created, uncommitted temporary file is owned
                // by this operation. Never remove a preexisting user file.
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
            var info = new FileInfo(path);
            if (info.Length > SourceProfilesConfigurationCodec.MaxDocumentCharacters * 4L)
                return codec.Parse(string.Empty);
            using (var file = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(file,
                new UTF8Encoding(false, true), false))
            {
                var json = reader.ReadToEnd();
                return codec.Parse(json);
            }
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
