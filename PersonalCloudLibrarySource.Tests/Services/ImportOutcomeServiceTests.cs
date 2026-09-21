using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class ImportOutcomeServiceTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-ImportOutcomeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }

        [Test]
        public void Import_MissingLocalManifest_ReturnsSourceFailure()
        {
            var settings = LocalFileSettings(Path.Combine(testRoot, "missing.json"));

            var outcome = new ImportOutcomeService().Import(settings, null);

            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.FailureKind, Is.EqualTo(ImportFailureKind.SourceUnavailable));
            Assert.That(outcome.Error, Does.Contain("not found"));
        }

        [TestCase("not-json", ImportFailureKind.InvalidManifest)]
        [TestCase("{\"version\":4,\"items\":[]}", ImportFailureKind.UnsupportedSchema)]
        public void Import_InvalidManifest_ReturnsStructuredFailure(string json, ImportFailureKind expectedKind)
        {
            var path = WriteManifest(json);

            var outcome = new ImportOutcomeService().Import(LocalFileSettings(path), null);

            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.FailureKind, Is.EqualTo(expectedKind));
            Assert.That(outcome.Error, Is.Not.Empty);
        }

        [TestCase(typeof(InvalidOperationException), "rclone cat failed")]
        [TestCase(typeof(TimeoutException), "rclone manifest retrieval timed out")]
        public void Import_RcloneFailure_ReturnsSourceFailure(Type exceptionType, string message)
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneRemoteName = "archive",
                RcloneManifestPath = "catalog/manifest.json"
            };

            var outcome = new ImportOutcomeService().Import(
                settings,
                ignored => { throw (Exception)Activator.CreateInstance(exceptionType, message); });

            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(outcome.FailureKind, Is.EqualTo(ImportFailureKind.SourceUnavailable));
            Assert.That(outcome.Source, Is.EqualTo("archive:catalog/manifest.json"));
            Assert.That(outcome.Error, Does.Contain(message));
        }

        [Test]
        public void Import_ValidEmptyManifest_IsSuccessfulEmptyLibrary()
        {
            var path = WriteManifest("{\"version\":3,\"items\":[]}");

            var outcome = new ImportOutcomeService().Import(LocalFileSettings(path), null);

            Assert.That(outcome.Succeeded, Is.True, outcome.Error);
            Assert.That(outcome.FailureKind, Is.EqualTo(ImportFailureKind.None));
            Assert.That(outcome.Games, Is.Empty);
            Assert.That(outcome.ValidItems, Is.Empty);
        }

        [Test]
        public void Complete_FailureThrowsTypedExceptionButValidEmptyReturnsNormally()
        {
            var failure = ImportOutcome.Failure(ImportFailureKind.SourceUnavailable, "source", "offline");
            var success = ImportOutcome.Success("source", new PersonalCloudLibraryItem[0], new Playnite.SDK.Models.GameMetadata[0]);

            var error = Assert.Throws<ImportOutcomeException>(() => ImportExecutionPolicy.Complete(failure));
            Assert.That(error.Outcome, Is.SameAs(failure));
            Assert.That(ImportExecutionPolicy.Complete(success), Is.Empty);
        }

        private PersonalCloudLibrarySourceSettings LocalFileSettings(string path)
        {
            return new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = path
            };
        }

        private string WriteManifest(string json)
        {
            var path = Path.Combine(testRoot, Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, json);
            return path;
        }
    }
}
