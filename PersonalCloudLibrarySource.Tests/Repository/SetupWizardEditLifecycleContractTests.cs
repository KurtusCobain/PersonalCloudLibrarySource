using NUnit.Framework;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class SetupWizardEditLifecycleContractTests
    {
        [Test]
        public void Completion_UsesBackgroundManifestGenerationButHostThreadSettingsCommit()
        {
            var navigation = File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.WorkDirectory, "PersonalCloudLibrarySource",
                "PersonalCloudLibrarySource.Navigation.cs"));
            var view = File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.WorkDirectory, "PersonalCloudLibrarySource",
                "Setup", "SetupWizardView.xaml.cs"));
            var windowService = File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.WorkDirectory, "PersonalCloudLibrarySource",
                "Setup", "SetupWizardWindowService.cs"));

            Assert.That(navigation, Does.Contain("PrepareSetupWizardCompletionAsync(CancellationToken token)"));
            Assert.That(navigation, Does.Contain("OutputPath = GetDefaultGeneratedManifestPath()"));
            Assert.That(navigation, Does.Contain("Task.Run("));
            Assert.That(navigation, Does.Contain("manifestGenerationService.Generate(options, token)"));
            Assert.That(navigation, Does.Contain("token.ThrowIfCancellationRequested();"));
            Assert.That(navigation, Does.Contain("settings.MarkSetupCompleted();"));
            Assert.That(view, Does.Contain("private async void Finish_Click"));
            Assert.That(view, Does.Contain("finishCancellation?.Cancel();"));
            Assert.That(windowService, Does.Contain("HandleCompletedAsync(CancellationToken token)"));
        }

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
