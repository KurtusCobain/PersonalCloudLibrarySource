using NUnit.Framework;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class ImportFailureWiringContractTests
    {
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
