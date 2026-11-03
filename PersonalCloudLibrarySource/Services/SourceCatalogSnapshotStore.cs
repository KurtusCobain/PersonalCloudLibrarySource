using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PersonalCloudLibrarySource
{
    // A non-executable, non-authoritative last-known-good identity record.
    // The hash detects accidental corruption, not malicious modification
    // by somebody with filesystem write access.
    public sealed class SourceCatalogSnapshotRead
    {
        public bool Succeeded { get; internal set; }
        public string Reason { get; internal set; } = string.Empty;
        public SourceCatalogIdentitySnapshot Snapshot { get; internal set; }
    }

    internal sealed class SourceCatalogSnapshotDocument
    {
        public int SchemaVersion { get; set; }
        public string ProfileId { get; set; }
        public string BindingFingerprint { get; set; }
        public List<string> GameIds { get; set; }
        public string IntegritySha256 { get; set; }
    }

    // Deliberately not called by GetGames or any install/uninstall handler.
    // Explicit saves to a pre-existing plugin-owned directory only.
    public sealed class SourceCatalogSnapshotStore
    {
        public const int SchemaVersion = 1;
        public const int MaxGameCount = 10000;
        public const int MaxFileBytes = 2 * 1024 * 1024;
        private const int MaxGameIdLength = 256;
        private const string FilePrefix = "source-catalog-identities-";
        private const string FileSuffix = ".v1.json";

        public string Save(string pluginDataDirectory, SourceCatalogIdentitySnapshot snapshot)
        {
            var root = ValidateDirectory(pluginDataDirectory);
            var document = MakeDocument(snapshot);
            var target = Path.Combine(root, FileName(document.ProfileId));
            RejectLinkOrDirectory(target);
            // Do not silently overwrite corrupt or future-schema records.
            if (File.Exists(target) && !ReadFile(target, document.ProfileId).Succeeded)
                throw new InvalidOperationException("Existing catalog identity snapshot is invalid.");

            var payload = new UTF8Encoding(false, true).GetBytes(Serializer().Serialize(document));
            if (payload.Length > MaxFileBytes)
                throw new InvalidOperationException("Catalog identity snapshot exceeds storage limit.");

            var pending = Path.Combine(root, FilePrefix + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var file = new FileStream(pending, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    file.Write(payload, 0, payload.Length);
                    file.Flush(true);
                }
                RejectLinkOrDirectory(target);
                if (File.Exists(target))
                {
                    // Keep the replaced valid record as an independently named
                    // backup. No automatic rollback or backup garbage collection.
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

        public SourceCatalogSnapshotRead Load(string pluginDataDirectory, string profileId)
        {
            var root = ValidateDirectory(pluginDataDirectory);
            var path = Path.Combine(root, FileName(profileId));
            return ReadFile(path, profileId);
        }

        private static SourceCatalogSnapshotDocument MakeDocument(SourceCatalogIdentitySnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            var id = CanonicalProfileId(snapshot.ProfileId);
            var fingerprint = snapshot.BindingFingerprint;
            var ids = snapshot.GameIds == null ? null : snapshot.GameIds.ToList();
            if (!ValidRecord(id, fingerprint, ids))
                throw new InvalidOperationException("Invalid catalog identity snapshot.");
            return new SourceCatalogSnapshotDocument
            {
                SchemaVersion = SchemaVersion,
                ProfileId = id,
                BindingFingerprint = fingerprint,
                GameIds = ids,
                IntegritySha256 = Digest(id, fingerprint, ids)
            };
        }

        private static SourceCatalogSnapshotRead ReadFile(string path, string expectedId)
        {
            RejectLinkOrDirectory(path);
            if (!File.Exists(path))
                return Refuse("No snapshot exists for this source.");
            var info = new FileInfo(path);
            if (info.Length == 0 || info.Length > MaxFileBytes)
                return Refuse("Snapshot is empty or exceeds the size limit.");
            try
            {
                string json;
                using (var stream = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), false))
                {
                    json = reader.ReadToEnd();
                    if (stream.Length > MaxFileBytes)
                        return Refuse("Snapshot exceeds the size limit.");
                }
                var doc = Serializer().Deserialize<SourceCatalogSnapshotDocument>(json);
                if (doc == null || doc.SchemaVersion != SchemaVersion ||
                    !string.Equals(doc.ProfileId, expectedId, StringComparison.Ordinal) ||
                    !ValidRecord(doc.ProfileId, doc.BindingFingerprint, doc.GameIds) ||
                    !string.Equals(doc.IntegritySha256,
                        Digest(doc.ProfileId, doc.BindingFingerprint, doc.GameIds),
                        StringComparison.Ordinal))
                    return Refuse("Snapshot schema, integrity or source identity is invalid.");
                return new SourceCatalogSnapshotRead
                {
                    Succeeded = true,
                    Snapshot = new SourceCatalogIdentitySnapshot(
                        doc.ProfileId, doc.BindingFingerprint, doc.GameIds)
                };
            }
            catch (ArgumentException) { return Refuse("Snapshot cannot be decoded."); }
            catch (InvalidOperationException) { return Refuse("Snapshot cannot be decoded."); }
        }

        private static bool ValidRecord(string id, string fingerprint, IList<string> gameIds)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(fingerprint) ||
                fingerprint.Length != 64 || !fingerprint.All(IsUpperHex) ||
                gameIds == null || gameIds.Count > MaxGameCount)
                return false;
            Guid parsed;
            if (!Guid.TryParseExact(id, "D", out parsed) || parsed == Guid.Empty ||
                !string.Equals(parsed.ToString("D"), id, StringComparison.Ordinal))
                return false;
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var gameId in gameIds)
            {
                if (string.IsNullOrWhiteSpace(gameId) || gameId.Length > MaxGameIdLength ||
                    gameId.Any(char.IsControl) || !used.Add(gameId))
                    return false;
            }
            return true;
        }

        private static string Digest(string id, string fingerprint, IEnumerable<string> gameIds)
        {
            var builder = new StringBuilder("pcls-identity-snapshot-v1;");
            Append(builder, id);
            Append(builder, fingerprint);
            foreach (var gameId in gameIds)
                Append(builder, gameId);
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(builder.ToString()))).Replace("-", "");
        }

        private static void Append(StringBuilder builder, string value)
        {
            var encoded = Encoding.UTF8.GetBytes(value ?? string.Empty);
            builder.Append(encoded.Length).Append(':');
            builder.Append(Convert.ToBase64String(encoded)).Append(';');
        }

        private static bool IsUpperHex(char c)
            => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');

        private static JavaScriptSerializer Serializer() => new JavaScriptSerializer
        {
            MaxJsonLength = MaxFileBytes,
            RecursionLimit = 12
        };

        private static SourceCatalogSnapshotRead Refuse(string reason)
            => new SourceCatalogSnapshotRead { Reason = reason };

        private static string FileName(string id)
            => FilePrefix + CanonicalProfileId(id).Replace("-", "") + FileSuffix;

        private static string CanonicalProfileId(string id)
        {
            Guid parsed;
            if (!Guid.TryParseExact(id, "D", out parsed) || parsed == Guid.Empty)
                throw new InvalidOperationException("Source identity is not a canonical GUID.");
            return parsed.ToString("D");
        }

        private static string ValidateDirectory(string pluginDataDirectory)
        {
            if (string.IsNullOrWhiteSpace(pluginDataDirectory))
                throw new ArgumentException("Existing plugin data directory required.");
            var root = Path.GetFullPath(pluginDataDirectory);
            var current = new DirectoryInfo(root);
            if (!current.Exists)
                throw new DirectoryNotFoundException("Plugin data directory does not exist.");
            while (current != null)
            {
                if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Linked profile storage directory refused.");
                current = current.Parent;
            }
            return root;
        }

        private static void RejectLinkOrDirectory(string path)
        {
            if (Directory.Exists(path))
                throw new InvalidOperationException("Snapshot path is a directory.");
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Linked snapshot file refused.");
        }
    }
}
