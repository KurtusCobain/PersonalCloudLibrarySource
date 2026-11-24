using System;

namespace PersonalCloudLibrarySource
{
    public sealed class SetupStatePersistenceService
    {
        public void MarkDismissed(
            PersonalCloudLibrarySourceSettingsV3 settings,
            Action<PersonalCloudLibrarySourceSettingsV3> save)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            var snapshot = SettingsMigrationService.CloneForEditing(settings) as PersonalCloudLibrarySourceSettingsV3;
            snapshot.SetupDismissed = true;
            save(snapshot);
            settings.SetupDismissed = true;
        }

        public void ApplyGeneratedManifest(PersonalCloudLibrarySourceSettingsV3 candidate,
            ManifestGenerationReport report, PersonalCloudLibrarySourceSettingsV3 expected)
        {
            if (candidate == null || report?.Manifest == null || expected == null ||
                !candidate.Enabled || candidate.EnableMultiSourceProfiles ||
                !string.Equals(candidate.SourceProviderType,
                    PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(candidate.SourceProviderType, expected.SourceProviderType, StringComparison.Ordinal) ||
                !string.Equals(candidate.LocalManifestPath, expected.LocalManifestPath, StringComparison.Ordinal) ||
                !string.Equals(candidate.ManifestRelativePath, expected.ManifestRelativePath, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(candidate.LocalLibraryRoot) ||
                string.IsNullOrWhiteSpace(expected.LocalLibraryRoot) ||
                string.IsNullOrWhiteSpace(report.SourceRoot) ||
                string.IsNullOrWhiteSpace(report.OutputPath) ||
                !string.Equals(PathBoundary.Normalize(candidate.LocalLibraryRoot),
                    PathBoundary.Normalize(expected.LocalLibraryRoot), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(PathBoundary.Normalize(candidate.LocalLibraryRoot),
                    PathBoundary.Normalize(report.SourceRoot), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The committed manifest configuration changed; preserve generated files for review.");

            candidate.LocalManifestPath = report.OutputPath;
            candidate.ManifestRelativePath = string.Empty;
            candidate.LastGeneratedManifestPath = report.OutputPath;
            candidate.LastGeneratedReportPath = report.ReportPath;
            candidate.LastManifestGeneratedAt = report.Manifest.GeneratedAt;
            candidate.LastManifestItemCount = report.ItemCount;
        }

        public void MarkCompleted(PersonalCloudLibrarySourceSettingsV3 settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.SetupCompleted = true;
            settings.SetupDismissed = false;
        }
    }
}
