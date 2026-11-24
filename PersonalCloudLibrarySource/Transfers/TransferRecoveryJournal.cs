using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PersonalCloudLibrarySource
{
    // Point-in-time observations only; never cleanup authorization.
    public enum TransferRecoveryPathState
    {
        Unverified = 0,
        Missing = 1,
        File = 2,
        Directory = 3
    }

    public sealed class TransferRecoveryRecord
    {
        internal TransferRecoveryRecord(Guid gameId, Guid jobId, string destination, string stagingPath,
            TransferRecoveryPathState destinationState, TransferRecoveryPathState stagingState)
        {
            GameId = gameId;
            JobId = jobId;
            Destination = destination;
            StagingPath = stagingPath;
            DestinationState = destinationState;
            StagingState = stagingState;
        }
        public Guid GameId { get; }
        public Guid JobId { get; }
        public string Destination { get; }
        public string StagingPath { get; }
        public TransferRecoveryPathState DestinationState { get; }
        public TransferRecoveryPathState StagingState { get; }
    }

    public sealed class TransferRecoverySnapshot
    {
        internal TransferRecoverySnapshot(List<TransferRecoveryRecord> records, int unreadableCount, bool complete)
        {
            Records = new ReadOnlyCollection<TransferRecoveryRecord>(records);
            UnreadableCount = unreadableCount;
            IsComplete = complete;
        }
        public IReadOnlyList<TransferRecoveryRecord> Records { get; }
        public int UnreadableCount { get; }
        public bool IsComplete { get; }
    }

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
        // Only this journal instance can complete markers it created. A
        // restarted instance may inspect and enforce them, not release them.
        private readonly Dictionary<Guid, TransferPartialPathPolicy.FileIdentity> ownedMarkers =
            new Dictionary<Guid, TransferPartialPathPolicy.FileIdentity>();

        public TransferRecoveryJournal(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Recovery journal directory is required.", nameof(directory));
            this.directory = Path.GetFullPath(directory);
        }

        // Advisory snapshot only. Never creates a directory or lock file,
        // changes a marker, or treats a missing marker as cleanup permission.
        public TransferRecoverySnapshot Inspect()
        {
            var records = new List<TransferRecoveryRecord>();
            var unreadable = 0;
            var complete = true;
            lock (syncRoot)
            {
                try
                {
                    TransferPartialPathPolicy.RequireSafePath(directory);
                    FileAttributes attributes;
                    try { attributes = File.GetAttributes(directory); }
                    catch (FileNotFoundException) { return new TransferRecoverySnapshot(records, 0, true); }
                    catch (DirectoryNotFoundException) { return new TransferRecoverySnapshot(records, 0, true); }
                    if ((attributes & FileAttributes.Directory) == 0)
                        return new TransferRecoverySnapshot(records, 0, false);

                    var count = 0;
                    foreach (var marker in Directory.EnumerateFileSystemEntries(directory, "*.pending", SearchOption.TopDirectoryOnly))
                    {
                        if (++count > MaxRecords) { complete = false; break; }
                        try
                        {
                            var record = Read(marker);
                            var staging = TransferPartialPathPolicy.Create(record.Item2, record.Item3);
                            var destinationState = InspectPath(record.Item2);
                            var stagingState = InspectPath(staging);
                            if (destinationState == TransferRecoveryPathState.Unverified ||
                                stagingState == TransferRecoveryPathState.Unverified)
                                complete = false;
                            records.Add(new TransferRecoveryRecord(record.Item1, record.Item3, record.Item2, staging,
                                destinationState, stagingState));
                        }
                        catch (Exception ex) when (IsInspectionFailure(ex)) { unreadable++; }
                    }
                }
                catch (Exception ex) when (IsInspectionFailure(ex)) { complete = false; }
            }
            return new TransferRecoverySnapshot(records, unreadable, complete && unreadable == 0);
        }

        private static TransferRecoveryPathState InspectPath(string path)
        {
            try
            {
                TransferPartialPathPolicy.RequireSafePath(path);
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    return TransferRecoveryPathState.Unverified;
                return (attributes & FileAttributes.Directory) != 0
                    ? TransferRecoveryPathState.Directory : TransferRecoveryPathState.File;
            }
            catch (FileNotFoundException) { return TransferRecoveryPathState.Missing; }
            catch (DirectoryNotFoundException) { return TransferRecoveryPathState.Missing; }
            catch (Exception ex) when (IsInspectionFailure(ex)) { return TransferRecoveryPathState.Unverified; }
        }

        private static bool IsInspectionFailure(Exception error) =>
            error is IOException || error is UnauthorizedAccessException ||
            error is ArgumentException || error is NotSupportedException ||
            error is System.Security.SecurityException;

        public void Begin(CloudTransferJob job)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));
            TransferPartialPathPolicy.RequireSafePath(job.Destination);
            var destination = PathBoundary.Normalize(job.Destination);
            var partial = TransferPartialPathPolicy.Create(destination, job.Id);
            if (Overlaps(directory, destination) || Overlaps(directory, partial))
                throw new IOException("Transfer destination overlaps the recovery journal.");
            var payload = Encode(job, destination);
            lock (syncRoot)
            using (Acquire())
            {
                var count = 0;
                foreach (var marker in Directory.EnumerateFileSystemEntries(directory, "*.pending", SearchOption.TopDirectoryOnly))
                {
                    if (++count >= MaxRecords)
                        throw new IOException("Transfer recovery journal is full; review pending records.");
                    var existing = Read(marker);
                    var existingPartial = TransferPartialPathPolicy.Create(existing.Item2, existing.Item3);
                    if (existing.Item1 == job.GameId ||
                        Overlaps(existing.Item2, destination) || Overlaps(existingPartial, destination) ||
                        Overlaps(existing.Item2, partial) || Overlaps(existingPartial, partial))
                        throw new TransferRetryRefusedException(
                            "A pending recovery record overlaps this transfer. Review the recovery journal before retrying.");
                }
                TransferPartialPathPolicy.RequireVacant(partial);
                var path = Marker(job.Id);
                TransferPartialPathPolicy.RequireVacant(path);
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    var identity = TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle);
                    stream.Write(payload, 0, payload.Length);
                    stream.Flush(true);
                    ownedMarkers.Add(job.Id, identity);
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
                TransferPartialPathPolicy.FileIdentity expected;
                if (!ownedMarkers.TryGetValue(job.Id, out expected))
                    throw new IOException("Only the originating journal may complete this reservation.");
                using (var stream = TransferPartialPathPolicy.OpenPrivateFileForReview(marker, true))
                {
                    if (!TransferPartialPathPolicy.MatchesPrivateFileIdentity(stream.SafeFileHandle, expected))
                        throw new IOException("Transfer recovery record was replaced.");
                    var existing = ReadRecord(marker, stream);
                    if (existing.Item1 != job.GameId ||
                        !string.Equals(existing.Item2, destination, StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Transfer recovery record identity changed.");
                    // Never remove a reservation while staging is present or
                    // its absence cannot be established. Delete only the handle
                    // that passed both identity and bounded payload checks.
                    TransferPartialPathPolicy.RequireVacant(TransferPartialPathPolicy.Create(destination, job.Id));
                    TransferPartialPathPolicy.DeleteVerifiedFileHandle(stream.SafeFileHandle);
                }
                TransferPartialPathPolicy.RequireVacant(marker);
                ownedMarkers.Remove(job.Id);
            }
        }

        private FileStream Acquire()
        {
            TransferPartialPathPolicy.RequireSafePath(directory);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "journal.lock");
            TransferPartialPathPolicy.RequireSafePath(path);
            var timer = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    try
                    {
                        TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle);
                        return stream;
                    }
                    catch { stream.Dispose(); throw; }
                }
                catch (IOException ex) when ((ex.HResult & 0xffff) == 32 && timer.ElapsedMilliseconds < 2000)
                {
                    // Another instance owns the short journal transaction.
                    // Bound contention without weakening the exclusive lock.
                    Thread.Sleep(25);
                }
            }
        }

        private static bool Overlaps(string first, string second) =>
            PathBoundary.IsContained(first, second) || PathBoundary.IsContained(second, first);

        private string Marker(Guid id) => Path.Combine(directory, id.ToString("N") + ".pending");

        private static byte[] Encode(CloudTransferJob job, string destination)
        {
            var payload = Encoding.GetBytes("PCLS-TRANSFER-1\n" + job.GameId.ToString("N") +
                "\n" + job.Id.ToString("N") + "\n" + destination);
            if (payload.Length > MaxRecordBytes)
                throw new IOException("Transfer recovery record exceeds its size limit.");
            return payload;
        }

        private static Tuple<Guid, string, Guid> Read(string path)
        {
            using (var stream = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
            {
                TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle);
                return ReadRecord(path, stream);
            }
        }

        private static Tuple<Guid, string, Guid> ReadRecord(string path, FileStream stream)
        {
            var length = stream.Length;
            if (length <= 0 || length > MaxRecordBytes)
                throw new IOException("Transfer recovery record has an invalid size.");
            var payload = new byte[(int)length];
            var offset = 0;
            while (offset < payload.Length)
            {
                var read = stream.Read(payload, offset, payload.Length - offset);
                if (read == 0) throw new IOException("Transfer recovery record was truncated.");
                offset += read;
            }
            if (stream.ReadByte() != -1)
                throw new IOException("Transfer recovery record changed during reading.");
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
            return Tuple.Create(gameId, destination, jobId);
        }
    }
}
