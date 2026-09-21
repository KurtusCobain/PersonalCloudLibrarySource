using NUnit.Framework;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Setup
{
    [TestFixture]
    public class SetupStateServiceTests
    {
        private readonly SetupStateService service = new SetupStateService(new SetupLaunchPolicyService());

        [TestCase(false, false, false, true, false, SetupLaunchAction.None)]
        [TestCase(true, true, false, false, true, SetupLaunchAction.None)]
        [TestCase(true, false, false, false, true, SetupLaunchAction.OpenWizard)]
        [TestCase(true, false, false, true, true, SetupLaunchAction.ShowReminder)]
        [TestCase(true, false, true, false, true, SetupLaunchAction.ShowReminder)]
        [TestCase(true, false, false, true, false, SetupLaunchAction.None)]
        public void Evaluate_MapsPersistedSetupState(
            bool enabled,
            bool valid,
            bool completed,
            bool dismissed,
            bool reminders,
            SetupLaunchAction expected)
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = enabled,
                SetupCompleted = completed,
                SetupDismissed = dismissed,
                ShowSetupReminders = reminders
            };

            Assert.That(service.Evaluate(settings, valid), Is.EqualTo(expected));
        }

        [Test]
        public void IsValid_LocalFolderRequiresExistingRootButNotPreexistingManifest()
        {
            var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "setup-state-root");
            Directory.CreateDirectory(root);
            try
            {
                var settings = new PersonalCloudLibrarySourceSettingsV3
                {
                    Enabled = true,
                    SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                    LocalLibraryRoot = root,
                    AllowDownloads = false
                };

                Assert.That(service.IsValid(settings), Is.True);
                settings.LocalLibraryRoot = Path.Combine(root, "missing");
                Assert.That(service.IsValid(settings), Is.False);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void IsValid_LocalFileRequiresExistingManifest()
        {
            var manifest = Path.Combine(TestContext.CurrentContext.WorkDirectory, "setup-state.json");
            File.WriteAllText(manifest, "{}");
            try
            {
                var settings = new PersonalCloudLibrarySourceSettingsV3
                {
                    Enabled = true,
                    SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                    LocalManifestPath = manifest,
                    AllowDownloads = false
                };

                Assert.That(service.IsValid(settings), Is.True);
                settings.LocalManifestPath = manifest + ".missing";
                Assert.That(service.IsValid(settings), Is.False);
            }
            finally
            {
                File.Delete(manifest);
            }
        }

        [Test]
        public void IsValid_InvalidProviderIsRejected()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true,
                SourceProviderType = "Unsupported",
                LocalManifestPath = "anything.json",
                AllowDownloads = false
            };

            Assert.That(service.IsValid(settings), Is.False);
        }
    }
}
