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
        public void MalformedRecordBlocksExecutionWithoutDeletingEvidence()
        {
            Directory.CreateDirectory(records);
            var marker = Path.Combine(records, Guid.NewGuid().ToString("N") + ".pending");
            File.WriteAllText(marker, "invalid");
            Assert.Throws<IOException>(() => new TransferRecoveryJournal(records).Begin(Job("other")));
            Assert.That(File.ReadAllText(marker), Is.EqualTo("invalid"));
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
