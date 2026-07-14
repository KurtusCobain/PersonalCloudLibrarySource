using NUnit.Framework;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource.Tests.Setup
{
    [TestFixture]
    public class SetupCompletionCoordinatorTests
    {
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
