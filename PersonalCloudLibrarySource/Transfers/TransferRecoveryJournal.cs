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

    // Observations are not proof that a transfer has stopped. In particular,
    // an unknown origin may be a live reservation owned by another instance.
    public enum TransferRecoveryOrigin
    {
        Unknown = 0,
        CurrentJournal = 1,
        Replaced = 2
    }

    public sealed class TransferRecoveryRecord
    {
        internal TransferRecoveryRecord(Guid gameId, Guid jobId, string destination, string stagingPath,
            TransferRecoveryPathState destinationState, TransferRecoveryPathState stagingState,
            TransferRecoveryOrigin origin)
        {
            GameId = gameId;
            JobId = jobId;
            Destination = destination;
            StagingPath = stagingPath;
            DestinationState = destinationState;
            StagingState = stagingState;
            Origin = origin;
        }
        public Guid GameId { get; }
        public Guid JobId { get; }
        public string Destination { get; }
        public string StagingPath { get; }
        public TransferRecoveryPathState DestinationState { get; }
        public TransferRecoveryPathState StagingState { get; }
        public TransferRecoveryOrigin Origin { get; }
    }

    public sealed class TransferRecoverySnapshot
    {
        internal TransferRecoverySnapshot(List<TransferRecoveryRecord> records, int unreadableCount,
            bool complete, List<Guid> unverifiedOwnedJobIds)
        {
            Records = new ReadOnlyCollection<TransferRecoveryRecord>(records);
            UnreadableCount = unreadableCount;
            UnverifiedOwnedJobIds = new ReadOnlyCollection<Guid>(unverifiedOwnedJobIds);
            IsComplete = complete && unreadableCount == 0 && unverifiedOwnedJobIds.Count == 0;
        }
        public IReadOnlyList<TransferRecoveryRecord> Records { get; }
        public int UnreadableCount { get; }
        // Missing, changed, unreadable or not fully observed. Not evidence
        // that the job stopped or that any path may be deleted.
        public IReadOnlyList<Guid> UnverifiedOwnedJobIds { get; }
        public bool IsComplete { get; }
    }

    internal sealed class TransferReservationWriteUncertainException : IOException
    {
        internal TransferReservationWriteUncertainException(Exception inner)
            : base("Recovery reservation creation could not be confirmed. Preserve journal evidence for review.", inner) { }
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
        private readonly Action<FileStream, byte[]> writePayload;
        // Only this journal instance can complete markers it created. A
        // restarted instance may inspect and enforce them, not release them.
        private sealed class OwnedReservation
        {
            public TransferPartialPathPolicy.FileIdentity Identity;
            public Guid GameId;
            public string Destination;
        }

        private readonly Dictionary<Guid, OwnedReservation> ownedMarkers =
            new Dictionary<Guid, OwnedReservation>();

        public TransferRecoveryJournal(string directory)
            : this(directory, (stream, payload) => stream.Write(payload, 0, payload.Length))
        {
        }

        // Internal seam for deterministic partial-write qualification.
        internal TransferRecoveryJournal(string directory, Action<FileStream, byte[]> writePayload)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Recovery journal directory is required.", nameof(directory));
            if (SafeCacheDeletionPolicy.IsUnsupportedWindowsPath(directory) ||
                (Path.IsPathRooted(directory) && !SafeCacheDeletionPolicy.IsFullyRooted(directory)))
                throw new ArgumentException("Recovery journal path is ambiguous.", nameof(directory));
            this.directory = Path.GetFullPath(directory);
            this.writePayload = writePayload ?? throw new ArgumentNullException(nameof(writePayload));
        }

        // A conservative, read-only gate for catalog publication. Do not
        // parse markers or probe their destinations: a pending filename alone
        // is enough to defer refresh, including malformed records. Do not wait
        // behind an in-process reservation transaction on a host thread.
        public bool IsClearForCatalogPublication()
        {
            if (!Monitor.TryEnter(syncRoot)) return false;
            try
            {
                if (ownedMarkers.Count != 0) return false;
                TransferPartialPathPolicy.RequireSafePath(directory);
                FileAttributes attributes;
                try { attributes = File.GetAttributes(directory); }
                catch (FileNotFoundException) { return true; }
                catch (DirectoryNotFoundException) { return true; }
                if ((attributes & FileAttributes.Directory) == 0) return false;
                using (var entries = Directory.EnumerateFileSystemEntries(directory, "*.pending",
                    SearchOption.TopDirectoryOnly).GetEnumerator())
                    return !entries.MoveNext();
            }
            catch (Exception ex) when (IsInspectionFailure(ex)) { return false; }
            finally { Monitor.Exit(syncRoot); }
        }

        // Read only reservation metadata, never the recorded destination.
        // A valid unrelated reservation does not block launching another game.
        // Unknown, malformed, changed or excessive evidence fails closed.
        internal bool IsClearForGameLaunch(Guid gameId, string launchPath, string installDirectory)
        {
            if (gameId == Guid.Empty || !Monitor.TryEnter(syncRoot)) return false;
            try
            {
                TransferPartialPathPolicy.RequireSafePath(directory);
                FileAttributes attributes;
                try { attributes = File.GetAttributes(directory); }
                catch (FileNotFoundException) { return ownedMarkers.Count == 0; }
                catch (DirectoryNotFoundException) { return ownedMarkers.Count == 0; }
                if ((attributes & FileAttributes.Directory) == 0) return false;

                var timer = Stopwatch.StartNew();
                var observedOwned = new HashSet<Guid>();
                var count = 0;
                foreach (var marker in Directory.EnumerateFileSystemEntries(directory, "*.pending", SearchOption.TopDirectoryOnly))
                {
                    // A host launch is synchronous. Bound the amount of
                    // metadata work and check elapsed time between reads.
                    if (++count > 128 || timer.ElapsedMilliseconds >= 1000) return false;
                    using (var stream = TransferPartialPathPolicy.OpenPrivateFileForReview(marker, false))
                    {
                        var identity = TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle);
                        var record = ReadRecord(marker, stream, false);
                        OwnedReservation expected;
                        if (ownedMarkers.TryGetValue(record.Item3, out expected))
                        {
                            if (!MatchesOwned(expected, identity, record)) return false;
                            observedOwned.Add(record.Item3);
                        }
                        if (ConflictsWithLaunch(record.Item1, record.Item2, record.Item3,
                            gameId, launchPath, installDirectory))
                            return false;
                    }
                }
                return observedOwned.Count == ownedMarkers.Count && timer.ElapsedMilliseconds < 1000;
            }
            catch (Exception ex) when (IsInspectionFailure(ex) || ex is InvalidOperationException) { return false; }
            finally { Monitor.Exit(syncRoot); }
        }

        internal static bool ConflictsWithLaunch(Guid reservedGame, string destination, Guid jobId,
            Guid gameId, string launchPath, string installDirectory)
        {
            if (reservedGame == gameId) return true;
            var staging = TransferPartialPathPolicy.Create(destination, jobId);
            return LaunchPathOverlaps(launchPath, destination, staging) ||
                LaunchPathOverlaps(installDirectory, destination, staging);
        }

        private static bool LaunchPathOverlaps(string path, string destination, string staging)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) return false;
            if (!SafeCacheDeletionPolicy.IsFullyRooted(path) || SafeCacheDeletionPolicy.IsUnsupportedWindowsPath(path))
                return true;
            return Overlaps(path, destination) || Overlaps(path, staging);
        }

        // Serialize an already-authorized hosted file-only removal against
        // cooperating journal transactions. This is not deletion authorization
        // by itself. Unlike the advisory gate, a mutation may create the
        // private journal directory and persistent lock file.
        internal CacheDeletionAuthorization RunFileRemovalIfClear(Func<CacheDeletionAuthorization> remove)
        {
            if (remove == null) throw new ArgumentNullException(nameof(remove));
            var refusal = new CacheDeletionAuthorization
            {
                Reason = "Transfer recovery is active or could not be exclusively verified. Preserve files and review recovery."
            };
            if (!Monitor.TryEnter(syncRoot)) return refusal;
            try
            {
                IDisposable guard;
                try { guard = Acquire(false); }
                catch (Exception ex) when (IsInspectionFailure(ex)) { return refusal; }
                using (guard)
                {
                    if (!IsClearForCatalogPublication()) return refusal;
                    // Do not swallow mutation exceptions or retry the callback.
                    return remove();
                }
            }
            finally { Monitor.Exit(syncRoot); }
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
                var observedOwned = new HashSet<Guid>();
                try
                {
                    TransferPartialPathPolicy.RequireSafePath(directory);
                    FileAttributes attributes;
                    try { attributes = File.GetAttributes(directory); }
                    catch (FileNotFoundException) { return Snapshot(records, 0, true, observedOwned); }
                    catch (DirectoryNotFoundException) { return Snapshot(records, 0, true, observedOwned); }
                    if ((attributes & FileAttributes.Directory) == 0)
                        return Snapshot(records, 0, false, observedOwned);

                    var count = 0;
                    foreach (var marker in Directory.EnumerateFileSystemEntries(directory, "*.pending", SearchOption.TopDirectoryOnly))
                    {
                        if (++count > MaxRecords) { complete = false; break; }
                        try
                        {
                            TransferPartialPathPolicy.FileIdentity observedIdentity;
                            Tuple<Guid, string, Guid> record;
                            using (var stream = TransferPartialPathPolicy.OpenPrivateFileForReview(marker, false))
                            {
                                observedIdentity = TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle);
                                record = ReadRecord(marker, stream);
                            }
                            OwnedReservation expected;
                            var origin = !ownedMarkers.TryGetValue(record.Item3, out expected)
                                ? TransferRecoveryOrigin.Unknown
                                : MatchesOwned(expected, observedIdentity, record)
                                    ? TransferRecoveryOrigin.CurrentJournal : TransferRecoveryOrigin.Replaced;
                            if (origin == TransferRecoveryOrigin.CurrentJournal)
                                observedOwned.Add(record.Item3);
                            if (origin == TransferRecoveryOrigin.Replaced) complete = false;
                            var staging = TransferPartialPathPolicy.Create(record.Item2, record.Item3);
                            var destinationState = InspectPath(record.Item2);
                            var stagingState = InspectPath(staging);
                            if (destinationState == TransferRecoveryPathState.Unverified ||
                                stagingState == TransferRecoveryPathState.Unverified)
                                complete = false;
                            records.Add(new TransferRecoveryRecord(record.Item1, record.Item3, record.Item2, staging,
                                destinationState, stagingState, origin));
                        }
                        catch (Exception ex) when (IsInspectionFailure(ex)) { unreadable++; }
                    }
                }
                catch (Exception ex) when (IsInspectionFailure(ex)) { complete = false; }
                return Snapshot(records, unreadable, complete, observedOwned);
            }
        }

        // Called only under syncRoot. Include the IDs that could not be
        // reconciled even when zero marker files were physically observed.
        private TransferRecoverySnapshot Snapshot(List<TransferRecoveryRecord> records,
            int unreadable, bool complete, HashSet<Guid> observedOwned)
        {
            var unverified = new List<Guid>();
            foreach (var id in ownedMarkers.Keys)
                if (!observedOwned.Contains(id)) unverified.Add(id);
            unverified.Sort();
            return new TransferRecoverySnapshot(records, unreadable, complete, unverified);
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
                // A current-session marker that vanished or changed must
                // not silently stop protecting a live or uncertain transfer.
                VerifyOwnedReservations();
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
                try
                {
                    TransferPartialPathPolicy.FileIdentity identity;
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                    {
                        identity = TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle);
                        writePayload(stream, (byte[])payload.Clone());
                        stream.Flush(true);
                        stream.Position = 0;
                        TransferPartialPathPolicy.VerifyRecordStage(stream, identity, payload, CancellationToken.None);
                    }
                    ownedMarkers.Add(job.Id, new OwnedReservation
                    {
                        Identity = identity, GameId = job.GameId, Destination = destination
                    });
                }
                catch (Exception ex)
                {
                    // Creation, write, flush or close may have left a partial
                    // marker. Do not execute or silently retry the operation.
                    // Preserve the evidence even if Begin did not return.
                    throw new TransferReservationWriteUncertainException(ex);
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
                OwnedReservation expected;
                if (!ownedMarkers.TryGetValue(job.Id, out expected))
                    throw new IOException("Only the originating journal may complete this reservation.");
                using (var stream = TransferPartialPathPolicy.OpenPrivateFileForReview(marker, true))
                {
                    if (!TransferPartialPathPolicy.MatchesPrivateFileIdentity(stream.SafeFileHandle, expected.Identity))
                        throw new IOException("Transfer recovery record was replaced.");
                    var existing = ReadRecord(marker, stream);
                    if (!MatchesOwned(expected, TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle), existing) ||
                        existing.Item1 != job.GameId ||
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

        private static bool MatchesOwned(OwnedReservation expected,
            TransferPartialPathPolicy.FileIdentity identity, Tuple<Guid, string, Guid> record)
        {
            return TransferPartialPathPolicy.SameIdentity(expected.Identity, identity) &&
                expected.GameId == record.Item1 &&
                string.Equals(expected.Destination, record.Item2, StringComparison.OrdinalIgnoreCase);
        }

        private void VerifyOwnedReservations()
        {
            foreach (var entry in ownedMarkers)
            {
                var path = Marker(entry.Key);
                using (var stream = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
                {
                    var identity = TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle);
                    if (!MatchesOwned(entry.Value, identity, ReadRecord(path, stream)))
                        throw new IOException("Current-session recovery marker changed; preserve journal evidence.");
                }
            }
        }

        private IDisposable Acquire(bool waitForContention = true)
        {
            TransferPartialPathPolicy.RequireSafePath(directory);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "journal.lock");
            TransferPartialPathPolicy.RequireSafePath(path);
            var timer = Stopwatch.StartNew();
            while (true)
            {
                try { return TransferPartialPathPolicy.OpenGuardedExclusiveLock(path); }
                catch (IOException ex) when (waitForContention &&
                    (ex.HResult & 0xffff) == 32 && timer.ElapsedMilliseconds < 2000)
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

        private Tuple<Guid, string, Guid> Read(string path)
        {
            using (var stream = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
            {
                TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle);
                return ReadRecord(path, stream);
            }
        }

        private Tuple<Guid, string, Guid> ReadRecord(string path, FileStream stream, bool inspectDestination = true)
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
            string[] fields;
            try { fields = Encoding.GetString(payload).Split('\n'); }
            catch (DecoderFallbackException ex)
            {
                throw new IOException("Transfer recovery record is not valid UTF-8.", ex);
            }
            Guid gameId, jobId;
            if (fields.Length != 4 || fields[0] != "PCLS-TRANSFER-1" ||
                !Guid.TryParseExact(fields[1], "N", out gameId) ||
                !Guid.TryParseExact(fields[2], "N", out jobId) || jobId == Guid.Empty ||
                !string.Equals(Path.GetFileNameWithoutExtension(path), jobId.ToString("N"), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Transfer recovery record is invalid.");
            if (!SafeCacheDeletionPolicy.IsFullyRooted(fields[3]) ||
                SafeCacheDeletionPolicy.IsUnsupportedWindowsPath(fields[3]))
                throw new IOException("Transfer recovery destination is ambiguous.");
            var destination = PathBoundary.Normalize(fields[3]);
            if (!string.Equals(destination, fields[3], StringComparison.OrdinalIgnoreCase))
                throw new IOException("Transfer recovery destination is not canonical.");
            if (inspectDestination) TransferPartialPathPolicy.RequireSafePath(destination);
            var staging = TransferPartialPathPolicy.Create(destination, jobId);
            if (Overlaps(directory, destination) || Overlaps(directory, staging))
                throw new IOException("Transfer recovery record overlaps journal storage.");
            return Tuple.Create(gameId, destination, jobId);
        }
    }
}
