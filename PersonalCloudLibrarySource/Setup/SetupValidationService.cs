using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource
{
    public sealed class SetupValidationResult
    {
        public SetupValidationResult(IList<string> errors)
        {
            Errors = errors ?? new List<string>();
        }

        public IList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
    }

    public sealed class SetupValidationService
    {
        public SetupValidationResult Validate(SetupDraft draft, SetupWizardStep step)
        {
            var errors = new List<string>();
            if (draft == null)
            {
                errors.Add("Setup information is unavailable.");
                return new SetupValidationResult(errors);
            }

            if (step == SetupWizardStep.ChooseSource || step == SetupWizardStep.Review)
            {
                ValidateSourceSelection(draft, errors);
            }

            if (step == SetupWizardStep.ConfigureSource || step == SetupWizardStep.Review)
            {
                ValidateSourceConfiguration(draft, errors);
            }

            if (step == SetupWizardStep.CacheBehavior || step == SetupWizardStep.Review)
            {
                ValidateCacheBehavior(draft, errors);
            }

            return new SetupValidationResult(errors);
        }

        private static void ValidateSourceSelection(SetupDraft draft, ICollection<string> errors)
        {
            if (!draft.SelectedSource.HasValue)
            {
                errors.Add("Choose where your library is stored.");
            }
        }

        private static void ValidateSourceConfiguration(SetupDraft draft, ICollection<string> errors)
        {
            if (!draft.SelectedSource.HasValue)
            {
                errors.Add("Choose where your library is stored.");
                return;
            }

            switch (draft.SelectedSource.Value)
            {
                case SetupSourceKind.ExistingManifest:
                    if (string.IsNullOrWhiteSpace(draft.LocalManifestPath))
                    {
                        errors.Add("Choose an existing manifest file.");
                    }
                    break;

                case SetupSourceKind.LocalFolder:
                case SetupSourceKind.NetworkFolder:
                    if (string.IsNullOrWhiteSpace(draft.LocalLibraryRoot))
                    {
                        errors.Add("Choose a local, external, or network library folder.");
                    }
                    break;

                case SetupSourceKind.RcloneRemote:
                    if (string.IsNullOrWhiteSpace(draft.RcloneExecutablePath))
                    {
                        errors.Add("Choose the rclone executable.");
                    }
                    if (string.IsNullOrWhiteSpace(draft.RcloneRemoteName))
                    {
                        errors.Add("Choose an rclone remote.");
                    }
                    if (string.IsNullOrWhiteSpace(draft.RcloneManifestPath))
                    {
                        errors.Add("Enter the remote manifest path.");
                    }
                    if (draft.RcloneTimeoutSeconds < 5 || draft.RcloneTimeoutSeconds > 300)
                    {
                        errors.Add("The rclone timeout must be between 5 and 300 seconds.");
                    }
                    break;
            }
        }

        private static void ValidateCacheBehavior(SetupDraft draft, ICollection<string> errors)
        {
            if (draft.AllowDownloads && string.IsNullOrWhiteSpace(draft.CachePath))
            {
                errors.Add("Choose a cache folder or select catalog-only mode.");
            }

            if (!string.Equals(
                    draft.UninstallBehavior,
                    PersonalCloudLibrarySourceSettings.RemoveCachedFileOnlyUninstallBehavior,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    draft.UninstallBehavior,
                    PersonalCloudLibrarySourceSettings.RemoveCachedInstallFolderUninstallBehavior,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    draft.UninstallBehavior,
                    PersonalCloudLibrarySourceSettings.AskEachTimeUninstallBehavior,
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Choose a valid cached-copy removal behavior.");
            }
        }
    }
}
