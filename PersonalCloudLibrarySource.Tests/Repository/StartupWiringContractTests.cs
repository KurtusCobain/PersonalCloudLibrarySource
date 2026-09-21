using NUnit.Framework;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class StartupWiringContractTests
    {
        [Test]
        public void StartupRefreshLabel_ClaimsOnlyDashboardAndStatusRefresh()
        {
            var xaml = Read("PersonalCloudLibrarySource", "PersonalCloudLibrarySourceSettingsView.xaml");
            Assert.That(xaml, Does.Contain("LOCPLSSettingsRefreshStartup"));
            var localization = Read("PersonalCloudLibrarySource", "Localization", "en_US.xaml");
            Assert.That(localization, Does.Contain("Refresh dashboard and library status when Playnite starts"));
            Assert.That(xaml, Does.Not.Contain("Refresh Playnite Game Library when Playnite starts"));
        }

        [Test]
        public void PluginStartup_IsOwnedByStartupServiceAndShutdownCancelsThenDisposesItFirst()
        {
            var source = Read("PersonalCloudLibrarySource", "PersonalCloudLibrarySource.Navigation.cs");
            var run = source.IndexOf("startupActionService.Start(");
            var cancel = source.IndexOf("startupActionService.Stop(");
            var dispose = source.IndexOf("startupActionService.Dispose();");
            var queueShutdown = source.IndexOf("DisposeTransferManager();");

            Assert.That(run, Is.GreaterThanOrEqualTo(0));
            Assert.That(cancel, Is.GreaterThanOrEqualTo(0));
            Assert.That(dispose, Is.GreaterThan(cancel));
            Assert.That(queueShutdown, Is.GreaterThan(dispose));
        }

        [Test]
        public void OnApplicationStarted_StartsTrackedOperationWithoutWaitingOrGeneratingInline()
        {
            var source = Read("PersonalCloudLibrarySource", "PersonalCloudLibrarySource.Navigation.cs");
            var start = source.IndexOf("public override void OnApplicationStarted");
            var stop = source.IndexOf("public override void OnApplicationStopped", start);
            var body = source.Substring(start, stop - start);

            Assert.That(body, Does.Contain("startupActionService.Start("));
            Assert.That(body, Does.Not.Contain(".Wait("));
            Assert.That(body, Does.Not.Contain("GenerateManifestFromFolder("));
            Assert.That(body, Does.Not.Contain("Task.Run("));
        }

        [Test]
        public void StartupUiCallbacksUseCancellationAwarePostNeverSynchronousInvoke()
        {
            var source = Read("PersonalCloudLibrarySource", "PersonalCloudLibrarySource.Navigation.cs");

            Assert.That(source, Does.Contain("startupUiDispatcher.Post("));
            Assert.That(source, Does.Not.Contain("startupUiDispatcher.Invoke("));
            Assert.That(source, Does.Contain("cancellationToken);"));
        }

        [Test]
        public void WizardCancel_RestoresDraftBeforePersistingDismissal()
        {
            var source = Read("PersonalCloudLibrarySource", "Setup", "SetupWizardWindowService.cs");
            var cancelHandler = source.IndexOf("private void HandleCancelled()");
            var restore = source.IndexOf("settingsViewModel.CancelEdit();", cancelHandler);
            var persist = source.IndexOf("setupDismissed();", cancelHandler);

            Assert.That(restore, Is.GreaterThan(cancelHandler));
            Assert.That(persist, Is.GreaterThan(restore));
        }

        [Test]
        public void WizardWindowClose_AlsoRestoresBeforePersistingDismissal()
        {
            var source = Read("PersonalCloudLibrarySource", "Setup", "SetupWizardWindowService.cs");
            var closedHandler = source.IndexOf("private void WizardWindow_Closed");
            var restore = source.IndexOf("settingsViewModel.CancelEdit();", closedHandler);
            var persist = source.IndexOf("setupDismissed();", closedHandler);

            Assert.That(restore, Is.GreaterThan(closedHandler));
            Assert.That(persist, Is.GreaterThan(restore));
        }

        private static string Read(params string[] parts) =>
            File.ReadAllText(Path.Combine(TestContext.CurrentContext.WorkDirectory, Path.Combine(parts)));
    }
}
