using System;

namespace PersonalCloudLibrarySource
{
    // Migration/planning helper only. Active runtime settings and on-disk
    // user configuration remain unchanged until multi-source is enabled.
    public sealed class SourceProfileSettingsProjector
    {
        public PclsSourceProfileDefinition FromLegacySettings(
            PersonalCloudLibrarySourceSettings settings, Guid persistentProfileId)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (persistentProfileId == Guid.Empty)
                throw new ArgumentException("A persistent legacy profile GUID is required.",
                    nameof(persistentProfileId));
            return new PclsSourceProfileDefinition
            {
                ProfileId = persistentProfileId.ToString("D"),
                DisplayName = string.IsNullOrWhiteSpace(settings.LibraryDisplayName)
                    ? "Original PCLS library" : settings.LibraryDisplayName,
                ProviderType = PersonalCloudLibrarySource.GetProviderType(settings),
                IsLegacySingleSource = true,
                Enabled = settings.Enabled,
                LocalManifestPath = settings.LocalManifestPath,
                LocalLibraryRoot = settings.LocalLibraryRoot,
                ManifestRelativePath = settings.ManifestRelativePath,
                RcloneExecutablePath = settings.RcloneExecutablePath,
                RcloneRemoteName = settings.RcloneRemoteName,
                RcloneManifestPath = settings.RcloneManifestPath,
                RcloneContentRoot = settings.RcloneContentRoot,
                RcloneTimeoutSeconds = settings.RcloneTimeoutSeconds
            };
        }

        // Builds independent per-source runtime settings from global cache
        // policy + profile-local manifest/remote information. No settings save.
        public PersonalCloudLibrarySourceSettingsV3 Project(
            PclsSourceProfileDefinition profile,
            PersonalCloudLibrarySourceSettingsV3 shared)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (shared == null) throw new ArgumentNullException(nameof(shared));
            var idCheck = new SourceProfileIdentityPolicy().Validate(
                new[] { profile });
            if (!idCheck.Valid)
                throw new InvalidOperationException("Invalid source profile identity.");
            if (string.IsNullOrWhiteSpace(shared.LocalCacheFolder))
                throw new InvalidOperationException("A shared cache folder must be configured.");
            if (shared.AllowUninstallOutsideCacheFolder)
                throw new InvalidOperationException(
                    "Multi-source rollout does not permit outside-cache uninstall.");

            if (string.Equals(profile.ProviderType,
                PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(profile.LocalManifestPath))
                    throw new InvalidOperationException("Local file profile lacks a manifest path.");
            }
            else if (string.Equals(profile.ProviderType,
                PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(profile.LocalLibraryRoot) ||
                    (string.IsNullOrWhiteSpace(profile.LocalManifestPath) &&
                     string.IsNullOrWhiteSpace(profile.ManifestRelativePath)))
                    throw new InvalidOperationException(
                        "Local folder profile lacks a root or manifest path.");
            }
            else if (string.Equals(profile.ProviderType,
                PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(profile.RcloneExecutablePath) ||
                    string.IsNullOrWhiteSpace(profile.RcloneRemoteName) ||
                    string.IsNullOrWhiteSpace(profile.RcloneManifestPath) ||
                    profile.RcloneTimeoutSeconds < 5 ||
                    profile.RcloneTimeoutSeconds > 300)
                    throw new InvalidOperationException(
                        "Rclone profile executable, remote, manifest or timeout is invalid.");
            }
            var projected = new PersonalCloudLibrarySourceSettingsV3
            {
                Enabled = shared.Enabled && profile.Enabled,
                SourceProviderType = profile.ProviderType,
                LibraryDisplayName = shared.LibraryDisplayName,
                LocalManifestPath = profile.LocalManifestPath,
                LocalLibraryRoot = profile.LocalLibraryRoot,
                ManifestRelativePath = profile.ManifestRelativePath,
                RcloneExecutablePath = profile.RcloneExecutablePath,
                RcloneRemoteName = profile.RcloneRemoteName,
                RcloneManifestPath = profile.RcloneManifestPath,
                RcloneContentRoot = profile.RcloneContentRoot,
                RcloneTimeoutSeconds = profile.RcloneTimeoutSeconds,

                LocalCacheFolder = shared.LocalCacheFolder,
                TreatMissingFilesAsUninstalled = shared.TreatMissingFilesAsUninstalled,
                AllowDownloads = shared.AllowDownloads,
                EnableDiagnostics = shared.EnableDiagnostics,
                UninstallBehavior = shared.UninstallBehavior,
                AllowUninstallOutsideCacheFolder = false,
                TransferConcurrency = shared.TransferConcurrency,
                VerifyAfterTransfer = shared.VerifyAfterTransfer,
                RemoveIncompleteTransferFiles = shared.RemoveIncompleteTransferFiles
            };
            return projected;
        }
    }
}
