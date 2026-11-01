using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PersonalCloudLibrarySource.Tests.Setup
{
    [TestFixture]
    public class SetupCompletionCoordinatorTests
    {
        [Test]
        public async Task CompleteAsync_PersistsOnlyAfterPreparation()
        {
            var calls = new List<string>();
            var gate = new TaskCompletionSource<bool>();
            var task = new SetupCompletionCoordinator().CompleteAsync(
                async token => { calls.Add("started"); await gate.Task; calls.Add("prepared"); },
                () => { calls.Add("persist"); return true; },
                () => calls.Add("reported"),
                CancellationToken.None);
            Assert.That(calls, Is.EqualTo(new[] { "started" }));
            gate.SetResult(true);
            Assert.That(await task, Is.True);
            Assert.That(calls, Is.EqualTo(new[] { "started", "prepared", "persist", "reported" }));
        }

        [Test]
        public void CompleteAsync_CancelledPreparation_NeverPersists()
        {
            var cts = new CancellationTokenSource();
            var gate = new TaskCompletionSource<bool>();
            var persisted = 0;
            var task = new SetupCompletionCoordinator().CompleteAsync(
                async token => await gate.Task,
                () => { persisted++; return true; },
                () => Assert.Fail("Cancellation must not report success"),
                cts.Token);
            cts.Cancel();
            gate.SetResult(true);
            Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
            Assert.That(persisted, Is.Zero);
        }

        [Test]
        public void Complete_PreparesThenPersistsBeforeReportingSuccess()
        {
            var calls = new List<string>();
            var coordinator = new SetupCompletionCoordinator();

            var completed = coordinator.Complete(
                () => calls.Add("prepare"),
                () =>
                {
                    calls.Add("persist");
                    return true;
                },
                () => calls.Add("success"));

            Assert.That(completed, Is.True);
            Assert.That(calls, Is.EqualTo(new[] { "prepare", "persist", "success" }));
        }

        [Test]
        public void Complete_FailedPersistenceDoesNotReportSuccess()
        {
            var successCount = 0;
            var coordinator = new SetupCompletionCoordinator();

            var completed = coordinator.Complete(
                () => { },
                () => false,
                () => successCount++);

            Assert.That(completed, Is.False);
            Assert.That(successCount, Is.Zero);
        }
    }
}
