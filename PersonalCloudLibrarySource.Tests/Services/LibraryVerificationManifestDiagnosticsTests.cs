using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class LibraryVerificationManifestDiagnosticsTests
    {
        [Test]
        public void BuildReport_DistinguishesExternalConsentFromInvalidPathWithoutProbingOrPrintingIt()
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-Verification-" + Guid.NewGuid().ToString("N"));
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = Path.Combine(root, "trusted", "catalog.json"),
                LocalCacheFolder = Path.Combine(root, "cache"),
                AllowDownloads = true
            };
            var manifest = new PersonalCloudLibraryManifest
            {
                Version = 3,
                Items = new List<PersonalCloudLibraryItem>
                {
                    new PersonalCloudLibraryItem { Id = "trusted", Title = "Trusted", SourcePath = "game.zip", CachePath = "one.zip" },
                    new PersonalCloudLibraryItem { Id = "external", Title = "External",
                        SourcePath = @"\\private-fixture-host\private-fixture-share\game.zip", CachePath = "two.zip" }
                }
            };
            var service = new LibraryVerificationService();
            var report = service.BuildReport(settings, "fixture", "report.txt", manifest, null, null, null, Guid.NewGuid());
            Assert.That(report.ExternalLocalSourceConsentCount, Is.EqualTo(1));
            Assert.That(report.InvalidOrMissingSourcePathCount, Is.Zero);
            Assert.That(report.DownloadEligibleCount, Is.EqualTo(2), "Consent is an execution guard, not an invalid manifest.");
            Assert.That(report.WarningSamples.Count(x => x.Contains("presence was not checked")), Is.EqualTo(1));
            var text = string.Join(Environment.NewLine, service.BuildReportLines(report));
            Assert.That(text, Does.Contain("External LocalFile sources requiring consent (not probed): 1"));
            Assert.That(text, Does.Not.Contain("private-fixture-host"));
            Assert.That(text, Does.Not.Contain("private-fixture-share"));
            Assert.That(Directory.Exists(root), Is.False);
        }

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
