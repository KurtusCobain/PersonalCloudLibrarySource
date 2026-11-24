using System;
using System.IO;
using System.Text;

namespace PersonalCloudLibrarySource
{
    // A write-ahead reservation, not a deletion receipt or resume instruction.
    // An interrupted/corrupt journal fails closed. Only the originating job
    // may release its record after confirmed cleanup/publication.
    public sealed class TransferRecoveryJournal
    {
        private const int MaxRecords = 4096;
        private const int MaxRecordBytes = 65536;
        private static readonly Encoding Encoding = new UTF8Encoding(false, true);
        private readonly string directory;
        private readonly object syncRoot = new object();

        public TransferRecoveryJournal(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Recovery journal directory is required.", nameof(directory));
            this.directory = Path.GetFullPath(directory);
        }

        public void Begin(CloudTransferJob job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            var destination = PathBoundary.Normalize(job.Destination);
            TransferPartialPathPolicy.RequireSafePath(destination);
            var partial = TransferPartialPathPolicy.Create(destination, job.Id);
            var payload = Encode(job, destination);
            lock (syncRoot)
            using (Acquire())
            {
                var count = 0;
                foreach (var marker in Directory.EnumerateFiles(directory, "*.pending", SearchOption.TopDirectoryOnly))
                {
                    if (++count >= MaxRecords)
                        throw new IOException("Transfer recovery journal is full; review pending records.");
                    var existing = Read(marker);
                    if (existing.Item1 == job.GameId ||
                        PathBoundary.IsContained(existing.Item2, destination) ||
                        PathBoundary.IsContained(destination, existing.Item2))
                        throw new TransferRetryRefusedException(
                            "A pending recovery record overlaps this transfer. Review the recovery journal before retrying.");
                }
                TransferPartialPathPolicy.RequireVacant(partial);
                var path = Marker(job.Id);
                TransferPartialPathPolicy.RequireVacant(path);
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                }
            }
        }

        public void Complete(CloudTransferJob job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            var destination = PathBoundary.Normalize(job.Destination);
            lock (syncRoot)
            using (Acquire())
            {
                var marker = Marker(job.Id);
                var existing = Read(marker);
                if (existing.Item1 != job.GameId ||
                    !string.Equals(existing.Item2, destination, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Transfer recovery record identity changed.");
                // Never remove a reservation while any staging path is present
                // or its absence cannot be established.
                TransferPartialPathPolicy.RequireVacant(TransferPartialPathPolicy.Create(destination, job.Id));
                TransferPartialPathPolicy.RequireSafePath(marker);
                File.Delete(marker);
            }
        }

        private FileStream Acquire()
        {
            TransferPartialPathPolicy.RequireSafePath(directory);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "journal.lock");
            TransferPartialPathPolicy.RequireSafePath(path);
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private string Marker(Guid id) => Path.Combine(directory, id.ToString("N") + ".pending");

        private static byte[] Encode(CloudTransferJob job, string destination)
        {
            var payload = Encoding.GetBytes("PCLS-TRANSFER-1\n" + job.GameId.ToString("N") +
                "\n" + job.Id.ToString("N") + "\n" + destination);
            if (payload.Length > MaxRecordBytes)
                throw new IOException("Transfer recovery record exceeds its size limit.");
            return payload;
        }

        private static Tuple<Guid, string> Read(string path)
        {
            TransferPartialPathPolicy.RequireSafePath(path);
            byte[] payload;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > MaxRecordBytes)
                    throw new IOException("Transfer recovery record has an invalid size.");
                payload = new byte[(int)stream.Length];
                var offset = 0;
                while (offset < payload.Length)
                {
                    var read = stream.Read(payload, offset, payload.Length - offset);
                    if (read == 0) throw new IOException("Transfer recovery record was truncated.");
                    offset += read;
                }
                if (stream.ReadByte() != -1)
                    throw new IOException("Transfer recovery record changed during reading.");
            }
            var fields = Encoding.GetString(payload).Split('\n');
            Guid gameId, jobId;
            if (fields.Length != 4 || fields[0] != "PCLS-TRANSFER-1" ||
                !Guid.TryParseExact(fields[1], "N", out gameId) ||
                !Guid.TryParseExact(fields[2], "N", out jobId) || jobId == Guid.Empty ||
                !string.Equals(Path.GetFileNameWithoutExtension(path), jobId.ToString("N"), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Transfer recovery record is invalid.");
            var destination = PathBoundary.Normalize(fields[3]);
            if (!string.Equals(destination, fields[3], StringComparison.OrdinalIgnoreCase))
                throw new IOException("Transfer recovery destination is not canonical.");
            TransferPartialPathPolicy.RequireSafePath(destination);
            return Tuple.Create(gameId, destination);
        }
    }
}
