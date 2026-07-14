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
            StringAssert.Contains("if (!postDeletionState.IsInstalled)", uninstall);
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

            StringAssert.Contains("return result.CreateValidatedManifest()", core);
            StringAssert.Contains("manifest = LoadParsedManifest(pluginSettings).Manifest", core);
            StringAssert.Contains("manifestLoader.ResolveLocalManifestPath(pluginSettings)", navigation);
            StringAssert.DoesNotContain("ResolveLocalFolderManifestPath", core + navigation);
        }

        private static string Read(params string[] segments)
        {
            var path = RepositoryRoot;
            foreach (var segment in segments) path = Path.Combine(path, segment);
            return File.ReadAllText(path);
        }
    }
}
