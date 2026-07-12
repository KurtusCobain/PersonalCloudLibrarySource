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
                return "Existing manifest file";
            }

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return "Local, external, or network folder";
            }

            if (string.Equals(providerType, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, StringComparison.OrdinalIgnoreCase))
            {
                return "Cloud storage through rclone";
            }

            return "Unknown source";
        }
    }
}
