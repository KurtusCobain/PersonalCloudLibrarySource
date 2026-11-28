using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    // Experimental primitive for a dedicated, existing private directory.
    // Not wired to any live store. Cooperative writers/readers use the same
    // verified lock. Hash chaining detects corruption, not malicious rollback
    // of an entire valid suffix; no external trusted anchor exists yet.
    internal sealed class ImmutableRecordGenerationStore
    {
        internal const int MaxGenerations = 64;
        internal const int MaxPayloadBytes = 1024 * 1024;
        private const int MaxEnvelopeBytes = 2 * 1024 * 1024;
        private const string LockName = "generation.lock";
        private const string PendingName = "generation.pending";
        private const string Magic = "PCLS-GENERATION-1";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly string root;
        private readonly string key;
        private readonly Action<string> checkpoint;

        // A test-only interruption seam. Production activation is a separate
        // decision and must not supply an external callback.
        public ImmutableRecordGenerationStore(string directory, Guid logicalKey, Action<string> checkpoint)
        {
            if (logicalKey == Guid.Empty) throw new ArgumentException("A logical key is required.", nameof(logicalKey));
            if (string.IsNullOrWhiteSpace(directory) || !SafeCacheDeletionPolicy.IsFullyRooted(directory))
                throw new IOException("An existing absolute private directory is required.");
            TransferPartialPathPolicy.RequireSafePath(directory);
            root = Path.GetFullPath(directory);
            if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Generation directory does not exist.");
            key = logicalKey.ToString("N");
            this.checkpoint = checkpoint;
        }

        public sealed class Head
        {
            private readonly byte[] payload;
            internal Head(int sequence, string digest, byte[] payload)
            {
                Sequence = sequence;
                Digest = digest;
                this.payload = (byte[])payload.Clone();
            }
            public int Sequence { get; }
            public string Digest { get; }
            public byte[] Payload => (byte[])payload.Clone();
        }

        // An empty store is represented by null; malformed or pending
        // evidence throws. Opening the cooperative lock may create its file.
        public Head ReadHead()
        {
            using (Acquire()) return ReadChain();
        }

        // Compare-and-append against the caller's reviewed head digest;
        // empty string is the explicit expectation of an empty store.
        public Head Append(string expectedHeadDigest, byte[] payload)
        {
            if (expectedHeadDigest == null || (expectedHeadDigest.Length != 0 && !IsDigest(expectedHeadDigest)))
                throw new ArgumentException("An explicit canonical head digest is required.", nameof(expectedHeadDigest));
            if (payload == null || payload.Length == 0 || payload.Length > MaxPayloadBytes)
                throw new IOException("Generation payload exceeds its bounds or is empty.");
            var frozen = (byte[])payload.Clone();
            using (Acquire())
            {
                var previous = ReadChain();
                if (!string.Equals(previous?.Digest ?? string.Empty, expectedHeadDigest, StringComparison.Ordinal))
                    throw new InvalidOperationException("Generation head changed; reload before appending.");
                var sequence = (previous?.Sequence ?? 0) + 1;
                if (sequence > MaxGenerations)
                    throw new IOException("Generation capacity reached; preserve evidence and review.");
                var envelope = Encode(sequence, expectedHeadDigest, frozen);
                var bytes = Utf8.GetBytes(envelope);
                var target = Path.Combine(root, FileName(sequence));
                var pending = Path.Combine(root, PendingName);
                TransferPartialPathPolicy.RequireVacant(target);
                var identity = TransferPartialPathPolicy.WriteRecordStage(pending, bytes);
                checkpoint?.Invoke("staged");
                // No overwrite; uncertain stages are deliberately retained.
                TransferPartialPathPolicy.CompletePrivateRecordStage(pending, target, identity, bytes, CancellationToken.None);
                checkpoint?.Invoke("published");
                using (var stream = TransferPartialPathPolicy.OpenPrivateFileForReview(target, false))
                    TransferPartialPathPolicy.VerifyRecordStage(stream, identity, bytes, CancellationToken.None);
                var actual = ReadChain();
                if (actual == null || actual.Sequence != sequence ||
                    !string.Equals(actual.Digest, Hash(bytes), StringComparison.Ordinal))
                    throw new IOException("Generation publication could not be reconciled.");
                return actual;
            }
        }

        private IDisposable Acquire()
        {
            TransferPartialPathPolicy.RequireSafePath(root);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("Generation directory disappeared.");
            return TransferPartialPathPolicy.OpenGuardedExclusiveLock(Path.Combine(root, LockName));
        }

        private Head ReadChain()
        {
            var names = new List<string>();
            var observed = 0;
            foreach (var path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.TopDirectoryOnly))
            {
                if (++observed > MaxGenerations + 1)
                    throw new IOException("Generation namespace exceeds its bounds or contains pending evidence.");
                var name = Path.GetFileName(path);
                if (string.Equals(name, LockName, StringComparison.Ordinal)) continue;
                if (name.Length != 15 || !name.EndsWith(".record", StringComparison.Ordinal))
                    throw new IOException("Unexpected or pending generation evidence; preserve the namespace.");
                names.Add(name);
            }
            names.Sort(StringComparer.Ordinal);
            Head head = null;
            for (var index = 0; index < names.Count; index++)
            {
                var sequence = index + 1;
                if (!string.Equals(names[index], FileName(sequence), StringComparison.Ordinal))
                    throw new IOException("Generation sequence is not contiguous and canonical.");
                var path = Path.Combine(root, names[index]);
                using (var stream = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
                {
                    var text = TransferPartialPathPolicy.ReadPrivateRecord(stream, MaxEnvelopeBytes);
                    // The shared reader accepts a UTF-8 BOM; this format does not.
                    if (Utf8.GetByteCount(text) != stream.Length)
                        throw new IOException("Generation encoding is not canonical.");
                    head = Decode(text, sequence, head?.Digest ?? string.Empty);
                }
            }
            return head;
        }

        private Head Decode(string text, int sequence, string previous)
        {
            var fields = text.Split('\n');
            if (fields.Length != 6 || fields[0] != Magic || fields[1] != key ||
                fields[2] != sequence.ToString(CultureInfo.InvariantCulture) ||
                fields[3] != previous || !IsDigest(fields[4]))
                throw new IOException("Generation identity, sequence or predecessor is invalid.");
            byte[] payload;
            try { payload = Convert.FromBase64String(fields[5]); }
            catch (FormatException ex) { throw new IOException("Invalid generation payload encoding.", ex); }
            if (payload.Length == 0 || payload.Length > MaxPayloadBytes ||
                !string.Equals(Encode(sequence, previous, payload), text, StringComparison.Ordinal))
                throw new IOException("Generation payload or integrity is invalid.");
            return new Head(sequence, Hash(Utf8.GetBytes(text)), payload);
        }

        private string Encode(int sequence, string previous, byte[] payload)
            => Magic + "\n" + key + "\n" + sequence.ToString(CultureInfo.InvariantCulture) +
                "\n" + previous + "\n" + Hash(payload) + "\n" + Convert.ToBase64String(payload);

        private static string FileName(int sequence)
            => sequence.ToString("D8", CultureInfo.InvariantCulture) + ".record";

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        }

        private static bool IsDigest(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (var c in value)
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }
    }
}
