using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PersonalCloudLibrarySource
{
    // Preparation only. No live reader, writer or activation path uses this
    // component. Older binaries can still write the legacy file; preparation
    // is not a downgrade fence or authorization to switch formats.
    internal sealed class SourceProfilesGenerationPreparation
    {
        private const string DirectoryName = "source-profiles.generations.v1";
        private const string Magic = "PCLS-PROFILE-MIGRATION-PREPARE-1";
        private const int MaxLegacyBytes = SourceProfilesConfigurationCodec.MaxDocumentCharacters * 4;
        private static readonly Guid LogicalKey = new Guid("8d68e44a-29a0-4b38-9d70-2523f76f28b5");
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly SourceProfilesConfigurationCodec codec = new SourceProfilesConfigurationCodec();
        private readonly string root;
        private readonly string legacy;
        private readonly string generations;
        private readonly Action<string> checkpoint;

        public SourceProfilesGenerationPreparation(string directory, Action<string> checkpoint)
        {
            if (string.IsNullOrWhiteSpace(directory) || !SafeCacheDeletionPolicy.IsFullyRooted(directory))
                throw new IOException("An existing absolute plugin data directory is required.");
            TransferPartialPathPolicy.RequireSafePath(directory);
            root = Path.GetFullPath(directory);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Plugin data directory does not exist.");
            legacy = Path.Combine(root, SourceProfilesFileStore.FileName);
            generations = Path.Combine(root, DirectoryName);
            this.checkpoint = checkpoint;
        }

        public sealed class Review
        {
            internal Review(string path, TransferPartialPathPolicy.FileIdentity identity, string digest, int bytes)
            {
                Path = path;
                Identity = identity;
                Digest = digest;
                Bytes = bytes;
            }
            internal string Path { get; }
            internal TransferPartialPathPolicy.FileIdentity Identity { get; }
            public string Digest { get; }
            public int Bytes { get; }
        }

        // Read-only preview; no lock file or generation directory is created.
        public Review Preview()
        {
            using (var ancestors = TransferPartialPathPolicy.PinPublicationAncestors(root))
            using (var file = TransferPartialPathPolicy.OpenPrivateFileForReview(legacy, false))
            {
                var identity = TransferPartialPathPolicy.CapturePrivateFileIdentity(file.SafeFileHandle);
                var bytes = ReadValidated(file);
                TransferPartialPathPolicy.RequireInspectedFilePath(legacy, file.SafeFileHandle);
                ancestors.Verify();
                return new Review(legacy, identity, Hash(bytes), bytes.Length);
            }
        }

        // The exact reviewed file must still be present. The legacy writer
        // lock and the open legacy file remain held throughout preparation.
        // Any occupied generation namespace is preserved, not adopted.
        public string Prepare(Review review)
        {
            if (review == null || !string.Equals(review.Path, legacy, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Review the exact legacy profile file before preparation.");
            using (TransferPartialPathPolicy.OpenPrivateRecordWriteLock(legacy))
            using (var ancestors = TransferPartialPathPolicy.PinPublicationAncestors(root))
            using (var file = TransferPartialPathPolicy.OpenPrivateFileForReview(legacy, false))
            {
                var identity = TransferPartialPathPolicy.CapturePrivateFileIdentity(file.SafeFileHandle);
                var bytes = ReadValidated(file);
                if (!TransferPartialPathPolicy.SameIdentity(review.Identity, identity) ||
                    review.Bytes != bytes.Length || review.Digest != Hash(bytes))
                    throw new InvalidOperationException("Legacy profiles changed since review.");
                TransferPartialPathPolicy.RequireInspectedFilePath(legacy, file.SafeFileHandle);
                ancestors.Verify();
                var envelope = Encode(identity, bytes);
                var directoryIdentity = TransferPartialPathPolicy.CreateOwnedDirectory(generations);
                using (var generationAncestors = TransferPartialPathPolicy.PinPublicationAncestors(generations))
                {
                    TransferPartialPathPolicy.RequireDirectoryIdentity(generations, directoryIdentity);
                    checkpoint?.Invoke("directory-created");
                    var store = new ImmutableRecordGenerationStore(generations, LogicalKey, checkpoint);
                    var head = store.Append(string.Empty, envelope);
                    TransferPartialPathPolicy.RequireDirectoryIdentity(generations, directoryIdentity);
                    TransferPartialPathPolicy.RequireInspectedFilePath(legacy, file.SafeFileHandle);
                    generationAncestors.Verify();
                    ancestors.Verify();
                    return head.Digest;
                }
            }
        }

        // Reconcile a preparation after restart; do not activate anything.
        // A changed legacy object or bytes, incomplete generation, or extra
        // generation is an explicit refusal. No automatic repair or retry.
        public string InspectPrepared()
        {
            using (TransferPartialPathPolicy.OpenPrivateRecordWriteLock(legacy))
            using (var ancestors = TransferPartialPathPolicy.PinPublicationAncestors(root))
            using (var file = TransferPartialPathPolicy.OpenPrivateFileForReview(legacy, false))
            {
                var identity = TransferPartialPathPolicy.CapturePrivateFileIdentity(file.SafeFileHandle);
                var bytes = ReadValidated(file);
                using (var generationAncestors = TransferPartialPathPolicy.PinPublicationAncestors(generations))
                {
                    var store = new ImmutableRecordGenerationStore(generations, LogicalKey, null);
                    var head = store.ReadHead();
                    if (head == null || head.Sequence != 1 || !Equal(head.Payload, Encode(identity, bytes)))
                        throw new IOException("Prepared generation cannot be reconciled with the legacy profile file.");
                    TransferPartialPathPolicy.RequireInspectedFilePath(legacy, file.SafeFileHandle);
                    generationAncestors.Verify();
                    ancestors.Verify();
                    return head.Digest;
                }
            }
        }

        private byte[] ReadValidated(FileStream file)
        {
            var length = file.Length;
            if (length <= 0 || length > MaxLegacyBytes)
                throw new IOException("Legacy profile size is invalid.");
            var bytes = new byte[(int)length];
            var offset = 0;
            while (offset < bytes.Length)
            {
                var count = file.Read(bytes, offset, bytes.Length - offset);
                if (count == 0) throw new IOException("Legacy profiles were truncated.");
                offset += count;
            }
            if (file.ReadByte() != -1 || file.Length != length)
                throw new IOException("Legacy profiles changed during inspection.");
            string json;
            try { json = Utf8.GetString(bytes); }
            catch (DecoderFallbackException ex) { throw new IOException("Legacy profiles are not valid UTF-8.", ex); }
            if (json.Length > 0 && json[0] == '\uFEFF') json = json.Substring(1);
            if (!codec.Parse(json).Succeeded)
                throw new IOException("Legacy profiles do not pass the existing schema validator.");
            return bytes;
        }

        private static byte[] Encode(TransferPartialPathPolicy.FileIdentity identity, byte[] bytes)
            => Utf8.GetBytes(Magic + "\n" +
                identity.VolumeSerial.ToString("X16", CultureInfo.InvariantCulture) + "\n" +
                identity.IdLow.ToString("X16", CultureInfo.InvariantCulture) + "\n" +
                identity.IdHigh.ToString("X16", CultureInfo.InvariantCulture) + "\n" +
                Hash(bytes) + "\n" + Convert.ToBase64String(bytes));

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }

        private static bool Equal(byte[] first, byte[] second)
        {
            if (first.Length != second.Length) return false;
            for (var i = 0; i < first.Length; i++)
                if (first[i] != second[i]) return false;
            return true;
        }
    }
}
