using NUnit.Framework;
using System.IO;
using System.Text.RegularExpressions;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class RuntimeSettingsSnapshotUsageTests
    {
        [Test]
        public void RetryQueue_CapturesSnapshotBeforeEnqueuingBackgroundWork()
        {
            var source = ReadProductionSource("PersonalCloudLibrarySource.GameCommands.cs");

            Assert.That(source, Does.Contain("var settingsSnapshot = settings.GetRuntimeSettingsSnapshot();"));
            Assert.That(source, Does.Contain("GetTransferQueue().Retry(previous.Id, settingsSnapshot)"));
            Assert.That(source, Does.Not.Contain("GetTransferQueue().Retry(previous.Id, settings.Settings)"));
            Assert.That(source, Does.Not.Contain("Task.Run"));
        }

        [Test]
        public void ImportAndLongLivedControllers_ReceiveIndependentRuntimeSnapshots()
        {
            var source = ReadProductionSource("PersonalCloudLibrarySource.cs");

            Assert.That(
                Regex.Matches(source, @"settings\.GetRuntimeSettingsSnapshot\(\)").Count,
                Is.EqualTo(3));
            Assert.That(source, Does.Not.Contain("var pluginSettings = settings.Settings;"));
        }

        [Test]
        public void TransferConcurrency_ReadsCommittedRuntimeSnapshot()
        {
            var source = ReadProductionSource("PersonalCloudLibrarySource.Transfers.cs");

            Assert.That(source, Does.Contain("settings?.GetRuntimeSettingsSnapshot()?.TransferConcurrency"));
            Assert.That(source, Does.Not.Contain("settings?.Settings?.TransferConcurrency"));
        }

        private static string ReadProductionSource(string fileName)
        {
            return File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "PersonalCloudLibrarySource",
                fileName));
        }
    }
}
