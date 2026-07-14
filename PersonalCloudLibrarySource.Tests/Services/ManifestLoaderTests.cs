using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class ManifestLoaderTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-ManifestLoaderTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }

        [Test]
        public void Load_MissingLocalFile_ReturnsFailureResult()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = Path.Combine(testRoot, "missing.json")
            };

            var result = new ManifestLoader().Load(settings, null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("not found"));
        }

        [Test]
        public void Load_ValidEmptyManifestSource_ReturnsJsonForParser()
        {
            var path = Path.Combine(testRoot, "manifest.json");
            File.WriteAllText(path, "{\"version\":3,\"items\":[]}");
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = path
            };

            var result = new ManifestLoader().Load(settings, null);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Json, Does.Contain("\"items\":[]"));
        }

        [Test]
        public void Load_RcloneException_ReturnsProviderFailureResult()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType
            };

            var result = new ManifestLoader().Load(settings, ignored => { throw new InvalidOperationException("rclone failed"); });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("rclone failed"));
        }

        [TestCase(@"C:\outside.json")]
        [TestCase(@"..\outside.json")]
        public void ResolveLocalManifestPath_RootedOrEscapingRelativePath_IsRefused(string relativePath)
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                LocalLibraryRoot = testRoot,
                ManifestRelativePath = relativePath
            };

            var result = new ManifestLoader().ResolveLocalManifestPath(settings);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Path, Is.Empty);
        }

        [Test]
        public void ResolveLocalManifestPath_ContainedRelativePath_IsCanonical()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                LocalLibraryRoot = testRoot,
                ManifestRelativePath = Path.Combine("catalog", "manifest.json")
            };

            var result = new ManifestLoader().ResolveLocalManifestPath(settings);

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.Path, Is.EqualTo(Path.Combine(testRoot, "catalog", "manifest.json")));
        }
    }
}
