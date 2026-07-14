using NUnit.Framework;
using System.IO;
using System.Text.RegularExpressions;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class RuntimeSettingsSnapshotUsageTests
    {
        [Test]
        public void RetryTask_CapturesSnapshotBeforeStartingBackgroundWork()
        {
            var source = ReadProductionSource("PersonalCloudLibrarySource.GameCommands.cs");

            Assert.That(source, Does.Contain("var settingsSnapshot = settings.GetRuntimeSettingsSnapshot();"));
            Assert.That(source, Does.Contain("ExecuteRclone(retry.Id, settingsSnapshot)"));
            Assert.That(source, Does.Not.Contain("ExecuteRclone(retry.Id, settings.Settings)"));
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
