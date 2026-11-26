using NUnit.Framework;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class ImportFailureWiringContractTests
    {
        [Test]
        public void CatalogPublicationRechecksRecoveryBeforeBothRoutesReturn()
        {
            var source = File.ReadAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory,
                "PersonalCloudLibrarySource", "PersonalCloudLibrarySource.cs"));
            StringAssert.Contains("RequireRecoveryClearForCatalogPublication(pluginSettings);", source);
            Assert.That(System.Text.RegularExpressions.Regex.Matches(source,
                @"RequireRecoveryClearForCatalogPublication\(pluginSettings\);").Count, Is.EqualTo(4));
            StringAssert.Contains("ImportFailureKind.RecoveryPending", source);
            StringAssert.Contains("throw new ImportOutcomeException(failure);", source);
        }

        [Test]
        public void GetGames_PublishesFailureThenPropagatesTypedOutcome()
        {
            var source = File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "PersonalCloudLibrarySource",
                "PersonalCloudLibrarySource.cs"));

            StringAssert.Contains("var outcome = importOutcomeService.Import", source);
            StringAssert.Contains("PublishImportOutcome(outcome", source);
            StringAssert.Contains("return ImportExecutionPolicy.Complete(outcome);", source);
            StringAssert.DoesNotContain("failed to import the manifest.\");\r\n                diagnostics.Add", source);
        }
    }
}
