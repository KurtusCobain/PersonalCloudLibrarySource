using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests.Dashboard
{
    [TestFixture]
    public class ImportDashboardStateServiceTests
    {
        [Test]
        public void CreateReport_FailureMarksManifestUnavailableAndPreservesError()
        {
            var outcome = ImportOutcome.Failure(ImportFailureKind.SourceUnavailable, "manifest.json", "offline");

            var report = ImportDashboardStateService.CreateReport(outcome, "diagnostics.txt");

            Assert.That(report.ManifestLoadSucceeded, Is.False);
            Assert.That(report.ManifestSource, Is.EqualTo("manifest.json"));
            Assert.That(report.ManifestLoadError, Is.EqualTo("offline"));
            Assert.That(report.ReportPath, Is.EqualTo("diagnostics.txt"));
        }

        [Test]
        public void CreateReport_ValidEmptyMarksManifestAvailableWithZeroItems()
        {
            var outcome = ImportOutcome.Success("manifest.json", new PersonalCloudLibraryItem[0], new Playnite.SDK.Models.GameMetadata[0]);

            var report = ImportDashboardStateService.CreateReport(outcome, "diagnostics.txt");

            Assert.That(report.ManifestLoadSucceeded, Is.True);
            Assert.That(report.TotalManifestItems, Is.Zero);
            Assert.That(report.ManifestLoadError, Is.Empty);
        }
    }
}
