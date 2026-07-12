using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class PluginNavigationServiceTests
    {
        [Test]
        public void Commands_RouteToConfiguredActions()
        {
            var dashboardCalls = 0;
            var settingsCalls = 0;
            var verifyCalls = 0;
            var cacheCalls = 0;
            var reportCalls = 0;
            var manifestCalls = 0;
            var updateHelpCalls = 0;
            var sourceCalls = 0;

            var service = new PluginNavigationService(
                () => dashboardCalls++,
                () => settingsCalls++,
                () => verifyCalls++,
                () => cacheCalls++,
                () => reportCalls++,
                () => manifestCalls++,
                () => updateHelpCalls++,
                () => sourceCalls++);

            service.OpenDashboard();
            service.OpenSettings();
            service.VerifyLibrary();
            service.OpenCacheFolder();
            service.OpenLatestReport();
            service.GenerateManifest();
            service.ShowUpdateLibraryInstructions();
            service.OpenSourceLocation();

            Assert.That(dashboardCalls, Is.EqualTo(1));
            Assert.That(settingsCalls, Is.EqualTo(1));
            Assert.That(verifyCalls, Is.EqualTo(1));
            Assert.That(cacheCalls, Is.EqualTo(1));
            Assert.That(reportCalls, Is.EqualTo(1));
            Assert.That(manifestCalls, Is.EqualTo(1));
            Assert.That(updateHelpCalls, Is.EqualTo(1));
            Assert.That(sourceCalls, Is.EqualTo(1));
        }
    }
}
