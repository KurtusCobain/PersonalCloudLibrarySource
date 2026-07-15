using System;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public static class PathBoundary
    {
        public static bool IsContained(string root, string candidate)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(candidate)) return false;
            try
            {
                var normalizedRoot = Normalize(root);
                var normalizedCandidate = Normalize(candidate);
                if (string.Equals(normalizedRoot, normalizedCandidate, StringComparison.OrdinalIgnoreCase)) return true;
                return normalizedCandidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public sealed class PathResolutionResult
    {
        public bool Succeeded { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    }

    public sealed class SourcePathResolver
    {
        public PathResolutionResult ResolveLocal(PersonalCloudLibrarySourceSettings settings, string sourcePath)
        {
            if (settings == null || string.IsNullOrWhiteSpace(sourcePath)) return Failure("Source path is empty.");
            if (Path.IsPathRooted(sourcePath))
            {
                if (string.Equals(
                    PersonalCloudLibrarySource.GetProviderType(settings),
                    PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                    StringComparison.OrdinalIgnoreCase))
                {
                    try { return Success(PathBoundary.Normalize(sourcePath)); }
                    catch (Exception ex) { return Failure("Source path could not be resolved: " + ex.Message); }
                }
                return Failure("Rooted source paths are not allowed for LocalFolder items.");
            }

            var root = settings.LocalLibraryRoot;
            if (string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(settings.LocalManifestPath))
            {
                root = Path.GetDirectoryName(settings.LocalManifestPath);
            }
            if (string.IsNullOrWhiteSpace(root)) return Failure("Local source root is unavailable.");

            try
            {
                var resolved = Path.GetFullPath(Path.Combine(root, sourcePath));
                if (!PathBoundary.IsContained(root, resolved)) return Failure("Source path escapes the local source root.");
                return Success(resolved);
            }
            catch (Exception ex)
            {
                return Failure("Source path could not be resolved: " + ex.Message);
            }
        }

        public string ResolveRclone(PersonalCloudLibrarySourceSettings settings, string sourcePath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)) return string.Empty;
            if (string.IsNullOrWhiteSpace(settings?.RcloneContentRoot)) return NormalizeRemote(sourcePath);
            return NormalizeRemote(settings.RcloneContentRoot) + "/" + NormalizeRemote(sourcePath);
        }

        private static string NormalizeRemote(string value) => (value ?? string.Empty).Trim().Replace('\\', '/').Trim('/');
        private static PathResolutionResult Success(string path) => new PathResolutionResult { Succeeded = true, Path = path };
        private static PathResolutionResult Failure(string error) => new PathResolutionResult { Error = error };
    }

    public sealed class CachePathResolution
    {
        public bool Succeeded { get; set; }
        public string Error { get; set; } = string.Empty;
        public string LaunchPath { get; set; } = string.Empty;
        public string InstallDirectory { get; set; } = string.Empty;
        public string DestinationFile { get; set; } = string.Empty;
        public string DestinationDirectory { get; set; } = string.Empty;
    }

    public sealed class CachePathResolver
    {
        public CachePathResolution Resolve(PersonalCloudLibraryItem item, PersonalCloudLibrarySourceSettings settings)
        {
            var result = new CachePathResolution();
            if (item == null || settings == null)
            {
                result.Error = "Item or settings are unavailable.";
                return result;
            }

            try
            {
                result.LaunchPath = ResolveCandidate(First(item.CachePath, item.LocalPath), settings.LocalCacheFolder);
                if (string.IsNullOrWhiteSpace(result.LaunchPath) && !string.IsNullOrWhiteSpace(item.InstallDirectory) && !string.IsNullOrWhiteSpace(item.LaunchFile))
                {
                    var installRoot = ResolveCandidate(item.InstallDirectory, settings.LocalCacheFolder);
                    result.LaunchPath = ResolveRelativeChild(installRoot, item.LaunchFile);
                }

                result.InstallDirectory = !string.IsNullOrWhiteSpace(item.InstallDirectory)
                    ? ResolveCandidate(item.InstallDirectory, settings.LocalCacheFolder)
                    : !string.IsNullOrWhiteSpace(result.LaunchPath)
                        ? Path.GetDirectoryName(result.LaunchPath)
                        : !string.IsNullOrWhiteSpace(settings.LocalCacheFolder) && !string.IsNullOrWhiteSpace(item.Id)
                            ? Path.Combine(settings.LocalCacheFolder, item.Id)
                            : string.Empty;

                result.DestinationFile = !string.IsNullOrWhiteSpace(item.CachePath)
                    ? ResolveCandidate(item.CachePath, settings.LocalCacheFolder)
                    : !string.IsNullOrWhiteSpace(result.InstallDirectory) && !string.IsNullOrWhiteSpace(item.LaunchFile)
                        ? ResolveRelativeChild(result.InstallDirectory, item.LaunchFile)
                        : result.LaunchPath;

                if (string.IsNullOrWhiteSpace(result.DestinationFile) && !string.IsNullOrWhiteSpace(settings.LocalCacheFolder) && !string.IsNullOrWhiteSpace(item.Id))
                {
                    var name = Path.GetFileName((PersonalCloudLibrarySource.GetItemSourcePath(item) ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
                    result.DestinationFile = Path.Combine(settings.LocalCacheFolder, item.Id, string.IsNullOrWhiteSpace(name) ? item.Id : name);
                }

                result.DestinationDirectory = !string.IsNullOrWhiteSpace(item.InstallDirectory)
                    ? ResolveCandidate(item.InstallDirectory, settings.LocalCacheFolder)
                    : !string.IsNullOrWhiteSpace(result.LaunchPath)
                        ? Path.GetDirectoryName(result.LaunchPath)
                        : !string.IsNullOrWhiteSpace(settings.LocalCacheFolder) && !string.IsNullOrWhiteSpace(item.Id)
                            ? Path.Combine(settings.LocalCacheFolder, item.Id)
                            : string.Empty;
                result.Succeeded = !string.IsNullOrWhiteSpace(result.LaunchPath) || !string.IsNullOrWhiteSpace(result.DestinationFile) || !string.IsNullOrWhiteSpace(result.DestinationDirectory);
                if (!result.Succeeded) result.Error = "Cache paths could not be resolved.";
            }
            catch (Exception ex)
            {
                result.Error = "Cache paths could not be resolved: " + ex.Message;
            }
            return result;
        }

        private static string First(string first, string second) => !string.IsNullOrWhiteSpace(first) ? first : second;
        private static string ResolveCandidate(string candidate, string cacheRoot)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return string.Empty;
            if (Path.IsPathRooted(candidate)) return PathBoundary.Normalize(candidate);
            if (string.IsNullOrWhiteSpace(cacheRoot)) throw new InvalidOperationException("LocalCacheFolder is required for relative cache paths.");
            var resolved = Path.GetFullPath(Path.Combine(cacheRoot, candidate));
            if (!PathBoundary.IsContained(cacheRoot, resolved)) throw new InvalidOperationException("Cache path escapes LocalCacheFolder.");
            return resolved;
        }

        private static string ResolveRelativeChild(string parent, string child)
        {
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(child)) return string.Empty;
            if (Path.IsPathRooted(child)) throw new InvalidOperationException("Launch file must be relative to the install directory.");
            var resolved = Path.GetFullPath(Path.Combine(parent, child));
            if (!PathBoundary.IsContained(parent, resolved)) throw new InvalidOperationException("Launch file escapes the install directory.");
            return resolved;
        }
    }
}
