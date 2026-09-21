using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class ManifestParserValidatorTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("not-json")]
        public void Parse_InvalidContent_Fails(string json)
        {
            var result = new ManifestParserValidator().Parse(json);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.Not.Empty);
        }

        [TestCase(0)]
        [TestCase(4)]
        public void Parse_UnsupportedSchema_Fails(int version)
        {
            var result = new ManifestParserValidator().Parse("{\"version\":" + version + ",\"items\":[]}");

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("version"));
        }

        [Test]
        public void Parse_ValidEmptyManifest_SucceedsDistinctly()
        {
            var result = new ManifestParserValidator().Parse("{\"version\":3,\"items\":[]}");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Manifest.Items, Is.Empty);
            Assert.That(result.Issues, Is.Empty);
        }

        [Test]
        public void Parse_DuplicatesAndInvalidItems_AreReportedAndExcluded()
        {
            const string json = "{\"version\":3,\"items\":[null,{\"id\":\"one\",\"title\":\"One\"},{\"id\":\"ONE\",\"title\":\"Duplicate\"},{\"id\":\"\",\"title\":\"No id\"},{\"id\":\"two\",\"title\":\"\"}]}";

            var result = new ManifestParserValidator().Parse(json);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ValidItems.Count, Is.EqualTo(1));
            Assert.That(result.ValidItems[0].Id, Is.EqualTo("one"));
            Assert.That(result.Issues.Count, Is.EqualTo(4));
            Assert.That(result.CreateValidatedManifest().Items, Has.Count.EqualTo(1));
            Assert.That(result.CreateValidatedManifest().Items[0].Id, Is.EqualTo("one"));
        }
    }
}
