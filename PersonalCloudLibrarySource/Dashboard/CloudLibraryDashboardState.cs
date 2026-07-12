namespace PersonalCloudLibrarySource
{
    public enum DashboardStatusKind
    {
        NeedsSetup = 0,
        Ready = 1,
        SourceUnavailable = 2,
        VerificationWarnings = 3,
        Updating = 4,
        Downloading = 5,
        TransferFailed = 6
    }

    public sealed class LibraryStatusContext
    {
        public bool SourceAvailable { get; set; } = true;
        public bool IsUpdating { get; set; }
        public int ManifestItemCount { get; set; }
        public int ImportedGameCount { get; set; }
        public int CachedGameCount { get; set; }
        public int WarningCount { get; set; }
        public int ActiveTransferCount { get; set; }
        public int FailedTransferCount { get; set; }
        public string SourceDescription { get; set; } = string.Empty;
        public string ManifestDescription { get; set; } = string.Empty;
        public string CachePath { get; set; } = string.Empty;
    }

    public sealed class CloudLibraryDashboardState
    {
        public CloudLibraryDashboardState(
            DashboardStatusKind status,
            string statusText,
            string sourceTypeDisplayName,
            string sourceDescription,
            string manifestDescription,
            string cachePath,
            bool isSetupComplete,
            bool sourceAvailable,
            int manifestItemCount,
            int importedGameCount,
            int cachedGameCount,
            int warningCount,
            int activeTransferCount,
            int failedTransferCount)
        {
            Status = status;
            StatusText = statusText ?? string.Empty;
            SourceTypeDisplayName = sourceTypeDisplayName ?? string.Empty;
            SourceDescription = sourceDescription ?? string.Empty;
            ManifestDescription = manifestDescription ?? string.Empty;
            CachePath = cachePath ?? string.Empty;
            IsSetupComplete = isSetupComplete;
            SourceAvailable = sourceAvailable;
            ManifestItemCount = manifestItemCount;
            ImportedGameCount = importedGameCount;
            CachedGameCount = cachedGameCount;
            WarningCount = warningCount;
            ActiveTransferCount = activeTransferCount;
            FailedTransferCount = failedTransferCount;
        }

        public DashboardStatusKind Status { get; }
        public string StatusText { get; }
        public string SourceTypeDisplayName { get; }
        public string SourceDescription { get; }
        public string ManifestDescription { get; }
        public string CachePath { get; }
        public bool IsSetupComplete { get; }
        public bool SourceAvailable { get; }
        public int ManifestItemCount { get; }
        public int ImportedGameCount { get; }
        public int CachedGameCount { get; }
        public int WarningCount { get; }
        public int ActiveTransferCount { get; }
        public int FailedTransferCount { get; }
    }
}
