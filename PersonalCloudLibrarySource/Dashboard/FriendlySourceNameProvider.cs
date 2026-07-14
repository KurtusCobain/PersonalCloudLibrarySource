using System;

namespace PersonalCloudLibrarySource
{
    public static class FriendlySourceNameProvider
    {
        public static string GetDisplayName(string providerType)
        {
            if (string.IsNullOrWhiteSpace(providerType) ||
                string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFileProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return PclsResources.Get("LOCPLSSourceExistingManifest", "Existing manifest file");
            }

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return PclsResources.Get("LOCPLSSourceLocalFolder", "Local, external, or network folder");
            }

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return PclsResources.Get("LOCPLSSourceRclone", "Cloud storage through rclone");
            }

            return PclsResources.Get("LOCPLSSourceUnknown", "Unknown source");
        }
    }
}
