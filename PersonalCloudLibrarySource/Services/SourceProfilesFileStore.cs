using System;
using System.Collections.Generic;
using System.IO;
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

        // Explicit backup preview, NOT rollback. The caller may present this
        // result to a user, but the implementation never auto-adopts an old
        // backup or writes an invalid current file.
        public SourceProfilesParseResult InspectBackup(
            string pluginDataDirectory, string backupFileName)
        {
            var root = ValidateStoreDirectory(pluginDataDirectory);
            if (string.IsNullOrWhiteSpace(backupFileName) ||
                !backupFileName.StartsWith("source-profiles-backup-",
                    StringComparison.Ordinal) ||
                !backupFileName.EndsWith(".json", StringComparison.Ordinal) ||
                backupFileName.IndexOfAny(new[] { '/', '\\', ':' }) >= 0)
                throw new InvalidOperationException("Only a local profile backup can be inspected.");
            var token = backupFileName.Substring("source-profiles-backup-".Length,
                backupFileName.Length - "source-profiles-backup-".Length - ".json".Length);
            Guid identifier;
            if (token.Length != 32 || !Guid.TryParseExact(token, "N", out identifier))
                throw new InvalidOperationException("Invalid profile backup identity.");
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
