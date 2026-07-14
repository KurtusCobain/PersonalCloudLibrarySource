using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests.Setup
{
    [TestFixture]
    public class SetupWizardViewModelTests
    {
        [Test]
        public void Constructor_CreatesIndependentDraftFromExistingSettings()
        {
            var active = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneExecutablePath = @"C:\Tools\rclone.exe",
                RcloneRemoteName = "games",
                RcloneManifestPath = "library.json",
                RcloneContentRoot = "content",
                LocalCacheFolder = @"D:\Cache",
                AllowDownloads = false
            };

            var viewModel = new SetupWizardViewModel(active, new SetupValidationService());

            Assert.That(viewModel.Draft.SelectedSource, Is.EqualTo(SetupSourceKind.RcloneRemote));
            Assert.That(viewModel.Draft.RcloneRemoteName, Is.EqualTo("games"));
            Assert.That(viewModel.Draft.CachePath, Is.EqualTo(@"D:\Cache"));

            viewModel.Draft.RcloneRemoteName = "changed";
            Assert.That(active.RcloneRemoteName, Is.EqualTo("games"));
        }

        [Test]
        public void Next_MissingRequiredSourceValue_StaysOnConfigureStepAndShowsError()
        {
            var active = new PersonalCloudLibrarySourceSettingsV3();
            var viewModel = new SetupWizardViewModel(active, new SetupValidationService());

            viewModel.SelectSource(SetupSourceKind.ExistingManifest);
            Assert.That(viewModel.Next(), Is.True);
            Assert.That(viewModel.CurrentStep, Is.EqualTo(SetupWizardStep.ConfigureSource));

            Assert.That(viewModel.Next(), Is.False);
            Assert.That(viewModel.CurrentStep, Is.EqualTo(SetupWizardStep.ConfigureSource));
            Assert.That(viewModel.ValidationErrors, Does.Contain("Choose an existing manifest file."));
        }

        [Test]
        public void Back_ReturnsToPreviousStepWithoutChangingActiveSettings()
        {
            var active = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = @"C:\old.json"
            };
            var viewModel = new SetupWizardViewModel(active, new SetupValidationService());

            viewModel.SelectSource(SetupSourceKind.LocalFolder);
            Assert.That(viewModel.Next(), Is.True);
            viewModel.Draft.LocalLibraryRoot = @"E:\Games";
            Assert.That(viewModel.Next(), Is.True);
            Assert.That(viewModel.CurrentStep, Is.EqualTo(SetupWizardStep.ScanPreview));

            Assert.That(viewModel.Back(), Is.True);
            Assert.That(viewModel.CurrentStep, Is.EqualTo(SetupWizardStep.ConfigureSource));
            Assert.That(active.SourceProviderType, Is.EqualTo(PersonalCloudLibrarySourceSettings.LocalFileProviderType));
            Assert.That(active.LocalManifestPath, Is.EqualTo(@"C:\old.json"));
        }

        [Test]
        public void Complete_ValidReview_CopiesDraftIntoActiveSettings()
        {
            var active = new PersonalCloudLibrarySourceSettingsV3
            {
                LocalManifestPath = @"C:\old.json",
                LocalCacheFolder = @"C:\OldCache"
            };
            var viewModel = new SetupWizardViewModel(active, new SetupValidationService());

            viewModel.SelectSource(SetupSourceKind.NetworkFolder);
            viewModel.Draft.LocalLibraryRoot = @"\\HOME-SERVER\Games";
            viewModel.Draft.LocalManifestPath = @"C:\Playnite\generated.json";
            viewModel.Draft.CachePath = @"D:\PlayniteCache";
            viewModel.Draft.AllowDownloads = true;
            viewModel.Draft.TreatMissingFilesAsUninstalled = true;

            Assert.That(viewModel.Next(), Is.True);
            Assert.That(viewModel.Next(), Is.True);
            Assert.That(viewModel.Next(), Is.True);
            Assert.That(viewModel.Next(), Is.True);
            Assert.That(viewModel.CurrentStep, Is.EqualTo(SetupWizardStep.Review));

            Assert.That(viewModel.Complete(), Is.True);
            Assert.That(viewModel.IsCompleted, Is.True);
            Assert.That(viewModel.CurrentStep, Is.EqualTo(SetupWizardStep.Completed));
            Assert.That(active.SourceProviderType, Is.EqualTo(PersonalCloudLibrarySourceSettings.LocalFolderProviderType));
            Assert.That(active.LocalLibraryRoot, Is.EqualTo(@"\\HOME-SERVER\Games"));
            Assert.That(active.LocalManifestPath, Is.EqualTo(@"C:\Playnite\generated.json"));
            Assert.That(active.LocalCacheFolder, Is.EqualTo(@"D:\PlayniteCache"));
            Assert.That(active.AllowDownloads, Is.True);
        }

        [Test]
        public void Cancel_DoesNotCopyDraftIntoActiveSettings()
        {
            var active = new PersonalCloudLibrarySourceSettingsV3
            {
                LocalManifestPath = @"C:\old.json"
            };
            var viewModel = new SetupWizardViewModel(active, new SetupValidationService());

            viewModel.SelectSource(SetupSourceKind.ExistingManifest);
            viewModel.Draft.LocalManifestPath = @"D:\new.json";
            viewModel.Cancel();

            Assert.That(viewModel.IsCancelled, Is.True);
            Assert.That(active.LocalManifestPath, Is.EqualTo(@"C:\old.json"));
        }

        [Test]
        public void ReactivateReviewAfterSaveFailure_AllowsSameDraftToBeCompletedAgain()
        {
            var active = new PersonalCloudLibrarySourceSettingsV3();
            var viewModel = new SetupWizardViewModel(active, new SetupValidationService());
            viewModel.SelectSource(SetupSourceKind.RcloneRemote);
            viewModel.Draft.RcloneExecutablePath = "rclone";
            viewModel.Draft.RcloneRemoteName = "archive";
            viewModel.Draft.RcloneManifestPath = "catalog/library.json";
            viewModel.Draft.CachePath = @"D:\Cache";

            while (viewModel.CurrentStep < SetupWizardStep.Review)
            {
                Assert.That(viewModel.Next(), Is.True);
            }

            Assert.That(viewModel.Complete(), Is.True);

            viewModel.ReactivateReviewAfterSaveFailure("Persistence failed.");

            Assert.That(viewModel.IsCompleted, Is.False);
            Assert.That(viewModel.CurrentStep, Is.EqualTo(SetupWizardStep.Review));
            Assert.That(viewModel.CanComplete, Is.True);
            Assert.That(viewModel.ValidationErrors, Does.Contain("Persistence failed."));
            Assert.That(viewModel.Complete(), Is.True);
        }
    }
}
