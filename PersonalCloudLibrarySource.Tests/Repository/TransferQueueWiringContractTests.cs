using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class TransferQueueWiringContractTests
    {
        [Test]
        public void ProductionTransferCallersUseQueueAndContainNoDetachedTaskRun()
        {
            var root = FindRepositoryRoot();
            var commands = File.ReadAllText(Path.Combine(root, "PersonalCloudLibrarySource", "PersonalCloudLibrarySource.GameCommands.cs"));
            var plugin = File.ReadAllText(Path.Combine(root, "PersonalCloudLibrarySource", "PersonalCloudLibrarySource.cs"));
            var controller = File.ReadAllText(Path.Combine(root, "PersonalCloudLibrarySource", "RcloneInstallController.cs"));

            Assert.That(commands, Does.Not.Contain("Task.Run"));
            Assert.That(commands, Does.Contain("RetryHostedTransfer(previous.Id)"));
            var transfers = File.ReadAllText(Path.Combine(root, "PersonalCloudLibrarySource", "PersonalCloudLibrarySource.Transfers.cs"));
            Assert.That(transfers, Does.Contain("queue.PrepareHostedRetry"));
            Assert.That(transfers, Does.Contain("playniteApi.InstallGame(gameId)"));
            Assert.That(plugin, Does.Contain("GetTransferQueue()"));
            Assert.That(controller, Does.Contain("transferQueue.Enqueue"));
            Assert.That(controller, Does.Contain("transferQueue.GetCompletion"));
        }

        [Test]
        public void ReadOnlyZipReviewUsesAwaitedWorkerAndShutdownCancellation()
        {
            var root = FindRepositoryRoot();
            var source = File.ReadAllText(Path.Combine(root, "PersonalCloudLibrarySource", "PersonalCloudLibrarySource.ZipPackages.cs"));
            var navigation = File.ReadAllText(Path.Combine(root, "PersonalCloudLibrarySource", "PersonalCloudLibrarySource.Navigation.cs"));
            Assert.That(source, Does.Contain("private async void ReviewZipGameTarget"));
            Assert.That(source, Does.Contain("await Task.Run(() => new ZipPackageReviewService().Inspect("));
            Assert.That(source, Does.Contain("ref activeZipReview"));
            Assert.That(source, Does.Contain("review?.Cancel()"));
            Assert.That(source, Does.Contain("cancellation.IsCancellationRequested || transferPresentationDisposed"));
            Assert.That(navigation, Does.Contain("CancelAllZipExtractions();"));
        }

        [Test]
        public void HostSerializesTransferCreationAndClosesAdmissionBeforeShutdownWait()
        {
            var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(),
                "PersonalCloudLibrarySource", "PersonalCloudLibrarySource.Transfers.cs"));
            var start = source.IndexOf("internal TransferQueueService GetTransferQueue()", StringComparison.Ordinal);
            var end = source.IndexOf("private void ThrowIfTransferHostStopped()", start, StringComparison.Ordinal);
            var queue = source.Substring(start, end - start);
            Assert.That(queue, Does.Contain("lock (transferLifecycleSync)"));
            Assert.That(queue.IndexOf("ThrowIfTransferHostStopped();", StringComparison.Ordinal),
                Is.LessThan(queue.IndexOf("new TransferQueueService", StringComparison.Ordinal)));
            start = source.IndexOf("private void DisposeTransferManager()", StringComparison.Ordinal);
            var shutdown = source.Substring(start);
            Assert.That(shutdown.IndexOf("transferPresentationDisposed = true;", StringComparison.Ordinal),
                Is.LessThan(shutdown.IndexOf("queue.Shutdown(", StringComparison.Ordinal)));
            Assert.That(shutdown, Does.Contain("transferQueue = null;"));
            Assert.That(source, Does.Not.Contain("transferPresentationDisposed = false"));
        }

        private static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                    File.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("Repository root was not found.");
        }
    }
}
