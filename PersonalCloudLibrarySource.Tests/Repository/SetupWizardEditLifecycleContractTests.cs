using NUnit.Framework;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class SetupWizardEditLifecycleContractTests
    {
        [Test]
        public void WindowConstruction_RegistersCleanupBeforeBeginningGuardedEdit()
        {
            var source = File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "PersonalCloudLibrarySource",
                "Setup",
                "SetupWizardWindowService.cs"));

            var createWindow = source.IndexOf("wizardWindow = playniteApi.Dialogs.CreateWindow");
            var registerClosed = source.IndexOf("wizardWindow.Closed += WizardWindow_Closed;");
            var beginEdit = source.IndexOf("settingsViewModel.BeginEdit();");

            Assert.That(createWindow, Is.GreaterThanOrEqualTo(0));
            Assert.That(registerClosed, Is.GreaterThan(createWindow));
            Assert.That(beginEdit, Is.GreaterThan(registerClosed));
            Assert.That(source, Does.Contain("catch (Exception)"));
            Assert.That(source, Does.Contain("settingsViewModel.CancelEdit();"));
        }
    }
}
