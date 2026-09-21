using System;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public sealed class SetupStateService
    {
        private readonly SetupLaunchPolicyService launchPolicy;

        public SetupStateService(SetupLaunchPolicyService launchPolicy)
        {
            this.launchPolicy = launchPolicy ?? throw new ArgumentNullException(nameof(launchPolicy));
        }

        public SetupLaunchAction Evaluate(PersonalCloudLibrarySourceSettingsV3 settings, bool setupValid)
        {
            if (settings == null)
            {
                return SetupLaunchAction.None;
            }

            return launchPolicy.Evaluate(new SetupLaunchContext
            {
                PluginEnabled = settings.Enabled,
                SetupValid = setupValid,
                SetupCompleted = settings.SetupCompleted,
                SetupDismissed = settings.SetupDismissed,
                ShowReminders = settings.ShowSetupReminders
            });
        }

        public bool IsValid(PersonalCloudLibrarySourceSettingsV3 settings)
        {
            if (settings == null || !settings.Enabled)
            {
                return false;
            }

            var provider = PersonalCloudLibrarySource.GetProviderType(settings);
            if (!string.Equals(provider, PersonalCloudLibrarySourceSettings.LocalFileProviderType, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(provider, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(provider, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var validation = new SetupValidationService().Validate(
                SetupDraft.FromSettings(settings),
                SetupWizardStep.Review);
            if (!validation.IsValid)
            {
                return false;
            }

            if (string.Equals(provider, PersonalCloudLibrarySourceSettings.LocalFileProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return File.Exists(settings.LocalManifestPath);
            }

            if (string.Equals(provider, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return Directory.Exists(settings.LocalLibraryRoot);
            }

            return true;
        }
    }
}
