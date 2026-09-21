using NUnit.Framework;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class FullscreenWorkflowContractTests
    {
        private static string RepositoryRoot => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", ".."));

        [Test]
        public void StandardControllers_DoNotDependOnDesktopWindowsOrNavigation()
        {
            var install = Read("PersonalCloudLibrarySource", "RcloneInstallController.cs");
            var uninstall = Read("PersonalCloudLibrarySource", "PersonalCloudLibraryUninstallController.cs");
            var combined = install + uninstall;

            StringAssert.DoesNotContain(".Dialogs", combined);
            StringAssert.DoesNotContain("OpenDashboard", combined);
            StringAssert.DoesNotContain("OpenPluginSettings", combined);
            StringAssert.DoesNotContain("CloudGameDetails", combined);
            StringAssert.Contains("GameWorkflowNotificationService", combined);
        }

        private static string Read(params string[] segments)
        {
            var path = RepositoryRoot;
            foreach (var segment in segments) path = Path.Combine(path, segment);
            return File.ReadAllText(path);
        }
    }
}
