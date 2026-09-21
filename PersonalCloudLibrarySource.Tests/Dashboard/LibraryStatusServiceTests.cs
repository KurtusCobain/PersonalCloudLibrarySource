using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests.Dashboard
{
    [TestFixture]
    public class LibraryStatusServiceTests
    {
        private readonly LibraryStatusService service = new LibraryStatusService();

        [Test]
        public void BuildState_IncompleteLocalFileSetup_ReturnsNeedsSetup()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = string.Empty
            };

            var state = service.BuildState(settings, new LibraryStatusContext());

            Assert.That(state.Status, Is.EqualTo(DashboardStatusKind.NeedsSetup));
            Assert.That(state.IsSetupComplete, Is.False);
        }

        [Test]
        public void BuildState_ValidConfiguration_ReturnsReady()
        {
            var settings = CreateReadySettings();

            var state = service.BuildState(settings, new LibraryStatusContext
            {
                SourceAvailable = true,
                ManifestItemCount = 50,
                ImportedGameCount = 48,
                CachedGameCount = 12
            });

            Assert.That(state.Status, Is.EqualTo(DashboardStatusKind.Ready));
            Assert.That(state.StatusText, Is.EqualTo("Ready"));
            Assert.That(state.ManifestItemCount, Is.EqualTo(50));
            Assert.That(state.ImportedGameCount, Is.EqualTo(48));
            Assert.That(state.CachedGameCount, Is.EqualTo(12));
        }

        [Test]
        public void BuildState_SourceUnavailable_ReturnsSourceUnavailable()
        {
            var state = service.BuildState(CreateReadySettings(), new LibraryStatusContext
            {
                SourceAvailable = false
            });

            Assert.That(state.Status, Is.EqualTo(DashboardStatusKind.SourceUnavailable));
        }

        [Test]
        public void BuildState_Warnings_ReturnsVerificationWarnings()
        {
            var state = service.BuildState(CreateReadySettings(), new LibraryStatusContext
            {
                SourceAvailable = true,
                WarningCount = 2
            });

            Assert.That(state.Status, Is.EqualTo(DashboardStatusKind.VerificationWarnings));
            Assert.That(state.WarningCount, Is.EqualTo(2));
        }

        [Test]
        public void BuildState_ActiveTransfers_TakesPriorityOverWarnings()
        {
            var state = service.BuildState(CreateReadySettings(), new LibraryStatusContext
            {
                SourceAvailable = true,
                WarningCount = 2,
                ActiveTransferCount = 1
            });

            Assert.That(state.Status, Is.EqualTo(DashboardStatusKind.Downloading));
        }

        [Test]
        public void BuildState_FailedTransfer_TakesPriorityOverActiveTransfer()
        {
            var state = service.BuildState(CreateReadySettings(), new LibraryStatusContext
            {
                SourceAvailable = true,
                ActiveTransferCount = 1,
                FailedTransferCount = 1
            });

            Assert.That(state.Status, Is.EqualTo(DashboardStatusKind.TransferFailed));
        }

        [Test]
        public void IsSetupComplete_RecognizesEachSupportedSourceType()
        {
            Assert.That(service.IsSetupComplete(new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = @"C:\library.json"
            }), Is.True);

            Assert.That(service.IsSetupComplete(new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                LocalLibraryRoot = @"D:\Games",
                LocalManifestPath = @"C:\generated.json"
            }), Is.True);

            Assert.That(service.IsSetupComplete(new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneExecutablePath = "rclone",
                RcloneRemoteName = "games",
                RcloneManifestPath = "library.json"
            }), Is.True);
        }

        private static PersonalCloudLibrarySourceSettingsV3 CreateReadySettings()
        {
            return new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = true,
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = @"C:\library.json"
            };
        }
    }
}
