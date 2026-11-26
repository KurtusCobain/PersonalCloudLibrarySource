using NUnit.Framework;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class PathStateConsumerContractTests
    {
        private static string RepositoryRoot => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", ".."));

        [Test]
        public void InstallAndUninstall_UseSharedStateApplicator()
        {
            var install = Read("PersonalCloudLibrarySource", "RcloneInstallController.cs");
            var uninstall = Read("PersonalCloudLibrarySource", "PersonalCloudLibraryUninstallController.cs");

            StringAssert.Contains("new LibraryItemStateApplicator().Apply(Game, itemState)", install);
            StringAssert.Contains("new LibraryItemStateApplicator().Reconcile(", uninstall);
            StringAssert.Contains("if (hasPlayniteHost)", uninstall);
            StringAssert.Contains("InvokeOnUninstalled();", uninstall);
            StringAssert.Contains("throw new InvalidOperationException", uninstall);
        }

        [Test]
        public void UninstallController_DelegatesDeletionToSafeExecutor()
        {
            var source = Read("PersonalCloudLibrarySource", "PersonalCloudLibraryUninstallController.cs");

            StringAssert.Contains("deletionExecutor.Delete", source);
            StringAssert.DoesNotContain("Directory.Delete", source);
            StringAssert.DoesNotContain("File.Delete", source);
        }

        [Test]
        public void ManifestConsumers_UseValidatedItemsAndSharedLocalPathResolution()
        {
            var core = Read("PersonalCloudLibrarySource", "PersonalCloudLibrarySource.cs");
            var navigation = Read("PersonalCloudLibrarySource", "PersonalCloudLibrarySource.Navigation.cs");

            var importer = Read("PersonalCloudLibrarySource", "Services", "ImportOutcomeService.cs");
            StringAssert.Contains("return LoadParsedManifest(pluginSettings).CreateValidatedManifest()", core);
            StringAssert.Contains("importOutcomeService.Import(pluginSettings", core);
            StringAssert.Contains("mapper.Map(parse.ValidItems, settings, diagnostics)", importer);
            StringAssert.Contains("ImportOutcome.Success(source, parse.ValidItems", importer);
            StringAssert.Contains("var parsed = new ManifestParserValidator().Parse(LoadManifestJson(pluginSettings))", core);
            StringAssert.Contains("manifest = parsed.Manifest", core);
            StringAssert.Contains("manifestLoader.ResolveLocalManifestPath(pluginSettings)", navigation);
            StringAssert.DoesNotContain("ResolveLocalFolderManifestPath", core + navigation);
        }

        [TestCase("RcloneInstallController.cs")]
        [TestCase("LibraryVerificationService.cs")]
        [TestCase("Services/GameCommandService.cs")]
        [TestCase("PersonalCloudLibraryUninstallController.cs")]
        public void WorkflowConsumersResolveCachePathsOncePerOperation(string path)
        {
            var source = Read("PersonalCloudLibrarySource", path);
            Assert.That(System.Text.RegularExpressions.Regex.Matches(source,
                @"new CachePathResolver\(\)\.Resolve\(item, settings\)").Count, Is.EqualTo(1));
            StringAssert.DoesNotContain("PersonalCloudLibrarySource.ResolveLaunchPath(", source);
            StringAssert.DoesNotContain("PersonalCloudLibrarySource.ResolveInstallDirectory(", source);
            StringAssert.DoesNotContain("PersonalCloudLibrarySource.ResolveDownloadDestination", source);
        }

        [Test]
        public void InstallReceiptUsesCapturedQueueDestinationRatherThanResolvingAgain()
        {
            var install = Read("PersonalCloudLibrarySource", "RcloneInstallController.cs");
            StringAssert.Contains("cacheRoot, job.Destination", install);
            StringAssert.Contains("ownershipReceipts.RecordCompleted(cacheRoot, ownershipReceiptKey, destination)", install);
            StringAssert.Contains("cacheRoot, ownershipReceiptKey, destination,", install);
        }

        private static string Read(params string[] segments)
        {
            var path = RepositoryRoot;
            foreach (var segment in segments) path = Path.Combine(path, segment);
            return File.ReadAllText(path);
        }
    }
}
