using System.Collections.Generic;

namespace PersonalCloudLibrarySource
{
    public class LibraryVerificationReport
    {
        public string GeneratedAt { get; set; }
        public string ReportPath { get; set; }
        public string ProviderMode { get; set; }
        public string ManifestSource { get; set; }
        public bool ManifestLoadSucceeded { get; set; }
        public string ManifestLoadError { get; set; }
        public int? ManifestVersion { get; set; }
        public int ConfigurationErrorsCount { get; set; }
        public int TotalManifestItems { get; set; }
        public int DuplicateIdCount { get; set; }
        public int MissingIdCount { get; set; }
        public int MissingTitleCount { get; set; }
        public int InvalidOrMissingSourcePathCount { get; set; }
        public int InvalidOrMissingCachePathCount { get; set; }
        public int SourceTypeFileCount { get; set; }
        public int SourceTypeDirectoryCount { get; set; }
        public int SourceTypeUnknownCount { get; set; }
        public int CachedInstalledCount { get; set; }
        public int MissingLocalCloudOnlyCount { get; set; }
        public int DownloadEligibleCount { get; set; }
        public int UncacheableMisconfiguredCount { get; set; }
        public int RclonePathDoublingWarningCount { get; set; }
        public int LocalFolderPathWarningCount { get; set; }
        public int MissingDescriptionCount { get; set; }
        public int MissingPlatformCount { get; set; }
        public int MissingPlayActionCount { get; set; }
        public int LibraryOwnedGameCount { get; set; }
        public int LibraryMissingCoverImageCount { get; set; }
        public int LibraryMissingBackgroundImageCount { get; set; }
        public int LibraryMissingDescriptionCount { get; set; }
        public int LibraryMissingPlatformCount { get; set; }
        public int LibraryMissingPlayActionCount { get; set; }
        public int CacheOwnedPathCount { get; set; }
        public int CacheOutsidePathCount { get; set; }
        public int CacheUnresolvedPathCount { get; set; }
        public List<string> ConfigurationErrors { get; set; } = new List<string>();
        public List<string> WarningSamples { get; set; } = new List<string>();
    }
}
