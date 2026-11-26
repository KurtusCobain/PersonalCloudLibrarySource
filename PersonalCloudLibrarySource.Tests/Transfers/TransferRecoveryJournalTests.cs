using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class TransferRecoveryJournalTests
    {
        private string root;
        private string records;
        private CloudTransferManager manager;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-TransferRecoveryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            records = Path.Combine(root, "journal");
            manager = new CloudTransferManager(1);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private CloudTransferJob Job(string destination)
        {
            return manager.Enqueue(Guid.NewGuid(), "Fixture", "source",
                Path.Combine(root, destination), "LocalFolder");
        }

        [Test]
        public void Inspect_MissingJournalDoesNotCreateAnyFiles()
        {
            var snapshot = new TransferRecoveryJournal(records).Inspect();
            Assert.That(snapshot.IsComplete, Is.True);
            Assert.That(snapshot.Records, Is.Empty);
            Assert.That(snapshot.UnverifiedOwnedJobIds, Is.Empty);
            Assert.That(Directory.Exists(records), Is.False);
        }

        [Test]
        public void CatalogPublicationGateDoesNotCreateJournalAndDefersUntilCleanCompletion()
        {
            var journal = new TransferRecoveryJournal(records);
            Assert.That(journal.IsClearForCatalogPublication(), Is.True);
            Assert.That(Directory.Exists(records), Is.False);
            var job = Job("file.zip");
            journal.Begin(job);
            Assert.That(journal.IsClearForCatalogPublication(), Is.False);
            Assert.That(new TransferRecoveryJournal(records).IsClearForCatalogPublication(), Is.False);
            journal.Complete(job);
            Assert.That(journal.IsClearForCatalogPublication(), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CatalogPublicationGateRefusesUnreadableOrUnexpectedJournal(bool directory)
        {
            if (directory)
            {
                Directory.CreateDirectory(records);
                File.WriteAllText(Path.Combine(records, "unknown.pending"), "invalid");
            }
            else File.WriteAllText(records, "preserve");
            Assert.That(new TransferRecoveryJournal(records).IsClearForCatalogPublication(), Is.False);
            if (directory) Assert.That(File.ReadAllText(Path.Combine(records, "unknown.pending")), Is.EqualTo("invalid"));
            else Assert.That(File.ReadAllText(records), Is.EqualTo("preserve"));
        }

        [Test]
        public void CatalogPublicationGateRefusesContendedInProcessJournalWithoutWaiting()
        {
            var journal = new TransferRecoveryJournal(records);
            var field = typeof(TransferRecoveryJournal).GetField("syncRoot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var gate = field.GetValue(journal);
            System.Threading.Tasks.Task<bool> observed;
            lock (gate)
            {
                observed = System.Threading.Tasks.Task.Run(() => journal.IsClearForCatalogPublication());
                Assert.That(observed.Wait(TimeSpan.FromSeconds(2)), Is.True,
                    "Read-only gate must not wait for another journal operation.");
                Assert.That(observed.Result, Is.False);
            }
            Assert.That(Directory.Exists(records), Is.False);
            Assert.That(journal.IsClearForCatalogPublication(), Is.True);
        }

        [Test]
        public void ExclusiveRemovalGateRefusesPendingMarkerAndDoesNotInvokeMutation()
        {
            var journal = new TransferRecoveryJournal(records);
            var job = Job("file.zip");
            journal.Begin(job);
            var other = new TransferRecoveryJournal(records);
            var method = typeof(TransferRecoveryJournal).GetMethod("RunFileRemovalIfClear",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            var calls = 0;
            Func<CacheDeletionAuthorization> remove = () =>
            {
                calls++;
                return new CacheDeletionAuthorization { Allowed = true };
            };
            var result = (CacheDeletionAuthorization)method.Invoke(other, new object[] { remove });
            Assert.That(result.Allowed, Is.False);
            Assert.That(calls, Is.Zero);
            Assert.That(Directory.GetFiles(records, "*.pending").Length, Is.EqualTo(1));
            journal.Complete(job);
            result = (CacheDeletionAuthorization)method.Invoke(other, new object[] { remove });
            Assert.That(result.Allowed, Is.True);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void ExclusiveRemovalGateExcludesOtherJournalTransactionsUntilCallbackReturns()
        {
            var journal = new TransferRecoveryJournal(records);
            var other = new TransferRecoveryJournal(records);
            var job = Job("file.zip");
            var method = typeof(TransferRecoveryJournal).GetMethod("RunFileRemovalIfClear",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            var calls = 0;
            Func<CacheDeletionAuthorization> nested = () =>
            {
                calls++;
                return new CacheDeletionAuthorization { Allowed = true };
            };
            Func<CacheDeletionAuthorization> remove = () =>
            {
                var refused = (CacheDeletionAuthorization)method.Invoke(other, new object[] { nested });
                Assert.That(refused.Allowed, Is.False);
                Assert.That(calls, Is.Zero);
                Assert.Throws<IOException>(() => other.Begin(job));
                Assert.That(Directory.GetFiles(records, "*.pending"), Is.Empty);
                return new CacheDeletionAuthorization { Allowed = true };
            };
            var result = (CacheDeletionAuthorization)method.Invoke(journal, new object[] { remove });
            Assert.That(result.Allowed, Is.True);
            other.Begin(job);
            other.Complete(job);
            Assert.That(Directory.GetFiles(records, "*.pending"), Is.Empty);
        }

        [Test]
        public void CatalogPublicationGateRefusesMissingOwnedMarker()
        {
            var journal = new TransferRecoveryJournal(records);
            var job = Job("file.zip");
            journal.Begin(job);
            File.Delete(Path.Combine(records, job.Id.ToString("N") + ".pending"));
            Assert.That(journal.IsClearForCatalogPublication(), Is.False);
        }

        [Test]
        public void Inspect_ValidAndCorruptRecordsRemainReserved()
        {
            var journal = new TransferRecoveryJournal(records);
            var job = Job("file.zip");
            journal.Begin(job);
            var marker = Path.Combine(records, Guid.NewGuid().ToString("N") + ".pending");
            File.WriteAllText(marker, "invalid");
            var snapshot = new TransferRecoveryJournal(records).Inspect();
            Assert.That(snapshot.IsComplete, Is.False);
            Assert.That(snapshot.UnreadableCount, Is.EqualTo(1));
            Assert.That(snapshot.Records.Count, Is.EqualTo(1));
            Assert.That(snapshot.Records[0].JobId, Is.EqualTo(job.Id));
            Assert.That(snapshot.Records[0].StagingPath, Is.EqualTo(
                TransferPartialPathPolicy.Create(job.Destination, job.Id)));
            Assert.That(File.ReadAllText(marker), Is.EqualTo("invalid"));
            Assert.That(Directory.GetFiles(records, "*.pending").Length, Is.EqualTo(2));
        }

        [Test]
        public void Inspect_ReportsObservedPathTypesWithoutChangingEvidence()
        {
            var journal = new TransferRecoveryJournal(records);
            var job = Job("file.zip");
            journal.Begin(job);
            var partial = TransferPartialPathPolicy.Create(job.Destination, job.Id);
            var initial = journal.Inspect();
            Assert.That(initial.Records[0].DestinationState, Is.EqualTo(TransferRecoveryPathState.Missing));
            Assert.That(initial.Records[0].StagingState, Is.EqualTo(TransferRecoveryPathState.Missing));

            File.WriteAllText(job.Destination, "existing destination");
            Directory.CreateDirectory(partial);
            var evidence = Path.Combine(partial, "preserve.txt");
            File.WriteAllText(evidence, "preserve");
            var observed = journal.Inspect();
            Assert.That(observed.IsComplete, Is.True);
            Assert.That(observed.Records[0].DestinationState, Is.EqualTo(TransferRecoveryPathState.File));
            Assert.That(observed.Records[0].StagingState, Is.EqualTo(TransferRecoveryPathState.Directory));
            Assert.That(File.ReadAllText(job.Destination), Is.EqualTo("existing destination"));
            Assert.That(File.ReadAllText(evidence), Is.EqualTo("preserve"));
            Assert.That(Directory.GetFiles(records, "*.pending").Length, Is.EqualTo(1));
            Assert.Throws<TransferRetryRefusedException>(() => journal.Begin(Job("file.zip")));
        }

        [Test]
        public void Inspect_UnexpectedJournalTypeIsNotReportedAsEmptySuccess()
        {
            File.WriteAllText(records, "preserve");
            var snapshot = new TransferRecoveryJournal(records).Inspect();
            Assert.That(snapshot.IsComplete, Is.False);
            Assert.That(File.ReadAllText(records), Is.EqualTo("preserve"));
        }

        [Test]
        public void InterruptedReservationBlocksOverlappingAttemptAfterReopen()
        {
            var job = Job("cache");
            new TransferRecoveryJournal(records).Begin(job);
            var reopened = new TransferRecoveryJournal(records);
            Assert.Throws<TransferRetryRefusedException>(() => reopened.Begin(Job("cache/child")));
            Assert.That(Directory.GetFiles(records, "*.pending").Length, Is.EqualTo(1));
            reopened.Begin(Job("unrelated"));
            Assert.That(Directory.GetFiles(records, "*.pending").Length, Is.EqualTo(2));
        }

        [Test]
        public void AnotherDestinationCannotOverlapReservedStaging()
        {
            var journal = new TransferRecoveryJournal(records);
            var first = Job("cache");
            journal.Begin(first);
            var partial = TransferPartialPathPolicy.Create(first.Destination, first.Id);
            var conflicting = manager.Enqueue(Guid.NewGuid(), "Conflict", "source",
                Path.Combine(partial, "child"), "LocalFolder");
            Assert.Throws<TransferRetryRefusedException>(() => journal.Begin(conflicting));
            Assert.That(Directory.GetFiles(records, "*.pending").Length, Is.EqualTo(1));
        }

        [Test]
        public void DestinationCannotOverlapJournalStorage()
        {
            var journal = new TransferRecoveryJournal(records);
            Assert.Throws<IOException>(() => journal.Begin(Job("journal")));
            Assert.Throws<IOException>(() => journal.Begin(Job("journal/child")));
            Assert.That(Directory.Exists(records), Is.False);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW",
            CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool CreateHardLinkNative(string link, string existing, IntPtr reserved);

        [Test]
        public void JournalLockHardlinkIsRefusedWithoutModifyingOriginal()
        {
            Directory.CreateDirectory(records);
            var original = Path.Combine(root, "important.txt");
            File.WriteAllText(original, "preserve");
            var linked = Path.Combine(records, "journal.lock");
            Assert.That(CreateHardLinkNative(linked, original, IntPtr.Zero), Is.True);
            Assert.Throws<IOException>(() => new TransferRecoveryJournal(records).Begin(Job("file.zip")));
            Assert.That(File.ReadAllText(original), Is.EqualTo("preserve"));
            Assert.That(File.ReadAllText(linked), Is.EqualTo("preserve"));
            Assert.That(Directory.GetFiles(records, "*.pending"), Is.Empty);
        }

        [Test]
        public void ConcurrentJournalInstancesCannotReserveOverlappingDestinations()
        {
            var first = Job("file.zip");
            var second = Job("file.zip");
            var one = new TransferRecoveryJournal(records);
            var two = new TransferRecoveryJournal(records);
            using (var start = new System.Threading.ManualResetEventSlim())
            {
                Func<TransferRecoveryJournal, CloudTransferJob, bool> reserve = (journal, job) =>
                {
                    start.Wait();
                    try { journal.Begin(job); return true; }
                    catch (TransferRetryRefusedException) { return false; }
                };
                var a = System.Threading.Tasks.Task.Run(() => reserve(one, first));
                var b = System.Threading.Tasks.Task.Run(() => reserve(two, second));
                start.Set();
                Assert.That(System.Threading.Tasks.Task.WaitAll(new System.Threading.Tasks.Task[] { a, b },
                    TimeSpan.FromSeconds(10)), Is.True);
                Assert.That(a.Result, Is.Not.EqualTo(b.Result));
                Assert.That(Directory.GetFiles(records, "*.pending").Length, Is.EqualTo(1));
                if (a.Result) one.Complete(first); else two.Complete(second);
                Assert.That(Directory.GetFiles(records, "*.pending"), Is.Empty);
            }
        }

        [Test]
        public void CleanCompletionReleasesOnlyItsOwnReservation()
        {
            var journal = new TransferRecoveryJournal(records);
            var first = Job("first");
            var second = Job("second");
            journal.Begin(first);
            journal.Begin(second);
            journal.Complete(first);
            Assert.That(Directory.GetFiles(records, "*.pending").Length, Is.EqualTo(1));
            new TransferRecoveryJournal(records).Begin(Job("first"));
            Assert.Throws<TransferRetryRefusedException>(() => journal.Begin(Job("second")));
        }

        [Test]
        public void ReopenedJournalCannotCompleteAnotherInstancesReservation()
        {
            var journal = new TransferRecoveryJournal(records);
            var job = Job("file.zip");
            journal.Begin(job);
            Assert.Throws<IOException>(() => new TransferRecoveryJournal(records).Complete(job));
            Assert.That(Directory.GetFiles(records, "*.pending").Length, Is.EqualTo(1));
            journal.Complete(job);
            Assert.That(Directory.GetFiles(records, "*.pending"), Is.Empty);
        }

        [Test]
        public void Inspect_DistinguishesCurrentJournalUnknownAndReplacedMarkers()
        {
            var journal = new TransferRecoveryJournal(records);
            var job = Job("file.zip");
            journal.Begin(job);
            Assert.That(journal.Inspect().Records[0].Origin, Is.EqualTo(TransferRecoveryOrigin.CurrentJournal));
            Assert.That(new TransferRecoveryJournal(records).Inspect().Records[0].Origin,
                Is.EqualTo(TransferRecoveryOrigin.Unknown));
            var marker = Path.Combine(records, job.Id.ToString("N") + ".pending");
            var original = Path.Combine(root, "original.pending");
            File.Move(marker, original);
            File.Copy(original, marker);
            var snapshot = journal.Inspect();
            Assert.That(snapshot.IsComplete, Is.False);
            Assert.That(snapshot.Records[0].Origin, Is.EqualTo(TransferRecoveryOrigin.Replaced));
            Assert.That(File.ReadAllBytes(marker), Is.EqualTo(File.ReadAllBytes(original)));
        }

        [Test]
        public void SamePayloadReplacementCannotReleaseReservation()
        {
            var journal = new TransferRecoveryJournal(records);
            var job = Job("file.zip");
            journal.Begin(job);
            var marker = Path.Combine(records, job.Id.ToString("N") + ".pending");
            var original = Path.Combine(root, "original.pending");
            File.Move(marker, original);
            File.Copy(original, marker);
            Assert.Throws<IOException>(() => journal.Complete(job));
            Assert.That(File.ReadAllText(marker), Is.EqualTo(File.ReadAllText(original)));
            Assert.Throws<TransferRetryRefusedException>(() =>
                new TransferRecoveryJournal(records).Begin(Job("file.zip")));
        }

        [Test]
        public void ExistingStagingPreventsReleaseAndPreservesContents()
        {
            var journal = new TransferRecoveryJournal(records);
            var job = Job("file.zip");
            journal.Begin(job);
            var partial = TransferPartialPathPolicy.Create(job.Destination, job.Id);
            File.WriteAllText(partial, "preserve");
            Assert.Throws<IOException>(() => journal.Complete(job));
            Assert.That(File.ReadAllText(partial), Is.EqualTo("preserve"));
            Assert.Throws<TransferRetryRefusedException>(() =>
                new TransferRecoveryJournal(records).Begin(Job("file.zip")));
        }

        [Test]
        public void RecordPointingInsideJournalIsInvalidAndBlocksUnrelatedTransfers()
        {
            Directory.CreateDirectory(records);
            var id = Guid.NewGuid();
            var path = Path.Combine(records, id.ToString("N") + ".pending");
            var text = "PCLS-TRANSFER-1\n" + Guid.NewGuid().ToString("N") + "\n" +
                id.ToString("N") + "\n" + Path.Combine(records, "protected");
            File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
            var journal = new TransferRecoveryJournal(records);
            var snapshot = journal.Inspect();
            Assert.That(snapshot.IsComplete, Is.False);
            Assert.That(snapshot.UnreadableCount, Is.EqualTo(1));
            Assert.That(snapshot.Records, Is.Empty);
            Assert.Throws<IOException>(() => journal.Begin(Job("unrelated")));
            Assert.That(File.ReadAllText(path), Is.EqualTo(text));
        }

        [Test]
        public void InvalidUtf8RecordBlocksExecutionAndRemainsIntact()
        {
            Directory.CreateDirectory(records);
            var path = Path.Combine(records, Guid.NewGuid().ToString("N") + ".pending");
            var payload = new byte[] { 0xff, 0xfe, 0xc0 };
            File.WriteAllBytes(path, payload);
            var journal = new TransferRecoveryJournal(records);
            Assert.That(journal.Inspect().UnreadableCount, Is.EqualTo(1));
            Assert.Throws<IOException>(() => journal.Begin(Job("unrelated")));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(payload));
        }

        [Test]
        public void MalformedRecordBlocksExecutionWithoutDeletingEvidence()
        {
            Directory.CreateDirectory(records);
            var marker = Path.Combine(records, Guid.NewGuid().ToString("N") + ".pending");
            File.WriteAllText(marker, "invalid");
            Assert.Throws<IOException>(() => new TransferRecoveryJournal(records).Begin(Job("other")));
            Assert.That(File.ReadAllText(marker), Is.EqualTo("invalid"));
        }

        [Test]
        public void PartialReservationWriteQuarantinesJobWithoutExecutingOrRemovingEvidence()
        {
            var constructor = typeof(TransferRecoveryJournal).GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(Action<FileStream, byte[]>) }, null);
            Assert.That(constructor, Is.Not.Null);
            Action<FileStream, byte[]> partialWrite = (stream, payload) =>
            {
                stream.Write(payload, 0, 4);
                stream.Flush(true);
                throw new IOException("fixture interruption");
            };
            var journal = (TransferRecoveryJournal)constructor.Invoke(new object[] { records, partialWrite });
            var executor = new CountingExecutor();
            using (var queue = new TransferQueueService(manager, executor, journal))
            {
                var job = queue.EnqueueLocal(Guid.NewGuid(), "Fixture", "source",
                    Path.Combine(root, "file.zip"), "LocalFolder", false);
                var completion = queue.GetCompletion(job.Id);
                Assert.That(completion.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(job.State, Is.EqualTo(CloudTransferState.Failed));
                Assert.That(job.RequiresManualRecovery, Is.True);
                Assert.That(completion.Result.RequiresManualRecovery, Is.True);
                Assert.That(executor.Calls, Is.Zero);
                Assert.That(new FileInfo(Path.Combine(records, job.Id.ToString("N") + ".pending")).Length, Is.EqualTo(4));
                Assert.That(journal.Inspect().IsComplete, Is.False);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CurrentSessionMissingOrAlteredMarkerCannotPermitAnotherReservation(bool alter)
        {
            var journal = new TransferRecoveryJournal(records);
            var job = Job("file.zip");
            journal.Begin(job);
            var marker = Path.Combine(records, job.Id.ToString("N") + ".pending");
            if (alter)
            {
                var text = File.ReadAllText(marker).Replace(job.GameId.ToString("N"), Guid.NewGuid().ToString("N"));
                File.WriteAllText(marker, text, new System.Text.UTF8Encoding(false));
                Assert.That(journal.Inspect().Records[0].Origin, Is.EqualTo(TransferRecoveryOrigin.Replaced));
            }
            else File.Delete(marker);
            var snapshot = journal.Inspect();
            Assert.That(snapshot.IsComplete, Is.False);
            Assert.That(snapshot.UnverifiedOwnedJobIds, Is.EqualTo(new[] { job.Id }));
            Assert.Throws<IOException>(() => journal.Begin(Job("other.zip")));
            Assert.Throws<IOException>(() => journal.Complete(job));
            Assert.That(Directory.GetFiles(records, "*.pending").Length, Is.EqualTo(alter ? 1 : 0));
        }

        [Test]
        public void MissingJournalDirectoryReportsCurrentJobIdentityWithoutRecreatingIt()
        {
            var journal = new TransferRecoveryJournal(records);
            var job = Job("file.zip");
            journal.Begin(job);
            var preserved = Path.Combine(root, "preserved-journal");
            Directory.Move(records, preserved);
            var snapshot = journal.Inspect();
            Assert.That(snapshot.IsComplete, Is.False);
            Assert.That(snapshot.Records, Is.Empty);
            Assert.That(snapshot.UnverifiedOwnedJobIds, Is.EqualTo(new[] { job.Id }));
            Assert.That(Directory.Exists(records), Is.False);
            Assert.That(Directory.GetFiles(preserved, "*.pending").Length, Is.EqualTo(1));
        }

        [Test]
        public void SameLengthIncorrectReservationPayloadNeverExecutes()
        {
            var constructor = typeof(TransferRecoveryJournal).GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(Action<FileStream, byte[]>) }, null);
            Assert.That(constructor, Is.Not.Null);
            Action<FileStream, byte[]> wrongWrite = (stream, payload) =>
            {
                payload[0] ^= 1;
                stream.Write(payload, 0, payload.Length);
            };
            var journal = (TransferRecoveryJournal)constructor.Invoke(new object[] { records, wrongWrite });
            var executor = new CountingExecutor();
            using (var queue = new TransferQueueService(manager, executor, journal))
            {
                var job = queue.EnqueueLocal(Guid.NewGuid(), "Fixture", "source",
                    Path.Combine(root, "file.zip"), "LocalFolder", false);
                var completion = queue.GetCompletion(job.Id);
                Assert.That(completion.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(completion.Result.RequiresManualRecovery, Is.True);
                Assert.That(executor.Calls, Is.Zero);
                Assert.That(Directory.GetFiles(records, "*.pending").Length, Is.EqualTo(1));
                Assert.That(journal.Inspect().IsComplete, Is.False);
            }
        }

        [Test]
        public void QueueRefusesExecutionWithPendingReservationFromPreviousSession()
        {
            var original = Job("file.zip");
            new TransferRecoveryJournal(records).Begin(original);
            var freshManager = new CloudTransferManager(1);
            var executor = new CountingExecutor();
            using (var queue = new TransferQueueService(freshManager, executor,
                new TransferRecoveryJournal(records)))
            {
                var job = queue.EnqueueLocal(Guid.NewGuid(), "Retry", "source",
                    original.Destination, "LocalFolder", false);
                Assert.That(queue.GetCompletion(job.Id).Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(job.State, Is.EqualTo(CloudTransferState.Failed));
                Assert.That(executor.Calls, Is.Zero);
            }
        }

        private sealed class CountingExecutor : ICloudTransferExecutor
        {
            public int Calls { get; private set; }
            public CloudTransferExecutionResult ExecuteLocal(Guid id, bool directory)
            {
                Calls++;
                return CloudTransferExecutionResult.Failure("unexpected execution");
            }
            public CloudTransferExecutionResult ExecuteRclone(Guid id, PersonalCloudLibrarySourceSettings settings)
                => ExecuteLocal(id, false);
        }
    }
}
