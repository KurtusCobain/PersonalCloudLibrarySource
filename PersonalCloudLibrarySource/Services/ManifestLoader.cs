using System;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public sealed class ManifestLoadResult
    {
        private ManifestLoadResult(bool succeeded, string json, string source, string error)
        {
            Succeeded = succeeded;
            Json = json ?? string.Empty;
            Source = source ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Json { get; }
        public string Source { get; }
        public string Error { get; }

        public static ManifestLoadResult Success(string json, string source) => new ManifestLoadResult(true, json, source, string.Empty);
        public static ManifestLoadResult Failure(string source, string error) => new ManifestLoadResult(false, string.Empty, source, error);
    }

    public sealed class ManifestLoader
    {
        public ManifestLoadResult Load(PersonalCloudLibrarySourceSettings settings, Func<PersonalCloudLibrarySourceSettings, string> readRclone)
        {
            if (settings == null)
            {
                return ManifestLoadResult.Failure(string.Empty, "Settings are unavailable.");
            }

            var provider = PersonalCloudLibrarySource.GetProviderType(settings);
            try
            {
                if (string.Equals(provider, PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, StringComparison.OrdinalIgnoreCase))
                {
                    if (readRclone == null) return ManifestLoadResult.Failure("rclone", "Rclone manifest reader is unavailable.");
                    return ManifestLoadResult.Success(readRclone(settings), "rclone");
                }

                string path;
                if (string.Equals(provider, PersonalCloudLibrarySourceSettings.LocalFolderProviderType, StringComparison.OrdinalIgnoreCase))
                {
                    var resolution = ResolveLocalManifestPath(settings);
                    if (!resolution.Succeeded) return ManifestLoadResult.Failure(resolution.Path, resolution.Error);
                    path = resolution.Path;
                }
                else if (string.Equals(provider, PersonalCloudLibrarySourceSettings.LocalFileProviderType, StringComparison.OrdinalIgnoreCase))
                {
                    path = settings.LocalManifestPath;
                }
                else
                {
                    return ManifestLoadResult.Failure(provider, "Source provider type must be LocalFile, LocalFolder, or RcloneRemote.");
                }

                if (string.IsNullOrWhiteSpace(path)) return ManifestLoadResult.Failure(path, "Manifest path is empty or could not be resolved.");
                if (!File.Exists(path)) return ManifestLoadResult.Failure(path, "Manifest was not found: " + path);
                return ManifestLoadResult.Success(File.ReadAllText(path), path);
            }
            catch (Exception ex)
            {
                return ManifestLoadResult.Failure(provider, ex.Message);
            }
        }

        public PathResolutionResult ResolveLocalManifestPath(PersonalCloudLibrarySourceSettings settings)
        {
            if (settings == null) return Failure("Settings are unavailable.");
            if (!string.IsNullOrWhiteSpace(settings.LocalManifestPath))
            {
                try { return Success(PathBoundary.Normalize(settings.LocalManifestPath)); }
                catch (Exception ex) { return Failure("Manifest path could not be resolved: " + ex.Message); }
            }
            if (string.IsNullOrWhiteSpace(settings.LocalLibraryRoot) || string.IsNullOrWhiteSpace(settings.ManifestRelativePath))
                return Failure("Manifest path is empty or could not be resolved.");
            if (Path.IsPathRooted(settings.ManifestRelativePath))
                return Failure("ManifestRelativePath must be relative to LocalLibraryRoot.");
            try
            {
                var combined = Path.GetFullPath(Path.Combine(settings.LocalLibraryRoot, settings.ManifestRelativePath));
                return PathBoundary.IsContained(settings.LocalLibraryRoot, combined)
                    ? Success(combined)
                    : Failure("ManifestRelativePath escapes LocalLibraryRoot.");
            }
            catch (Exception ex)
            {
                return Failure("Manifest path could not be resolved: " + ex.Message);
            }
        }

        private static PathResolutionResult Success(string path) => new PathResolutionResult { Succeeded = true, Path = path };
        private static PathResolutionResult Failure(string error) => new PathResolutionResult { Error = error };
    }
}
