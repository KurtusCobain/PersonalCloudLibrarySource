using NUnit.Framework;
using System;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class LibraryVerificationManifestDiagnosticsTests
    {
        [Test]
        public void BuildReport_RawAuthoritativeParseRetainsInvalidAndDuplicateDiagnostics()
        {
            const string json = "{\"version\":3,\"items\":[{\"id\":\"one\",\"title\":\"One\"},{\"id\":\"ONE\",\"title\":\"Duplicate\"},{\"id\":\"\",\"title\":\"No id\"},{\"id\":\"two\",\"title\":\"\"}]}";
            var parseResult = new ManifestParserValidator().Parse(json);

            var report = new LibraryVerificationService().BuildReport(
                new PersonalCloudLibrarySourceSettingsV3(),
                "fixture",
                "report.txt",
                parseResult.Manifest,
                null,
                null,
                null,
                Guid.Parse("61993828-67a8-4468-93a2-293442e36328"));

            Assert.That(report.TotalManifestItems, Is.EqualTo(4));
            Assert.That(report.DuplicateIdCount, Is.EqualTo(1));
            Assert.That(report.MissingIdCount, Is.EqualTo(1));
            Assert.That(report.MissingTitleCount, Is.EqualTo(1));
        }
    }
}
