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
        public void HostedUninstallRechecksRecoveryAndDoesNotInvalidateActiveImportPlanOnDeferral()
        {
            var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "PersonalCloudLibrarySource");
            var source = File.ReadAllText(Path.Combine(root, "PersonalCloudLibrarySource.cs"));
            var uninstall = File.ReadAllText(Path.Combine(root, "PersonalCloudLibraryUninstallController.cs"));
            StringAssert.Contains("() => IsRecoveryClearForCatalogPublication() && IsCurrentExperimentalActionPlan(plan)", source);
            StringAssert.Contains("workflow.CanUninstall && IsRecoveryClearForCatalogPublication()", source);
            var start = source.IndexOf("private void RequireRecoveryClearForCatalogPublication(", System.StringComparison.Ordinal);
            var end = source.IndexOf("// Every action request", start, System.StringComparison.Ordinal);
            var deferral = source.Substring(start, end - start);
            StringAssert.DoesNotContain("experimentalMultiSourcePlan = null", deferral);
            StringAssert.DoesNotContain("PublishImportOutcome(failure", deferral);
            StringAssert.Contains("executionAuthorized != null && !executionAuthorized()", uninstall);
            Assert.That(System.Text.RegularExpressions.Regex.Matches(source,
                "RecoveryProtectedRemoval = ExecuteRecoveryProtectedRemoval").Count, Is.EqualTo(2));
            StringAssert.Contains("RecoveryProtectedRemoval(remove)", uninstall);
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
