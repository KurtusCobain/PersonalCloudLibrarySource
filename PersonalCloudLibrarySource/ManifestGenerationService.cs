using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    public class ManifestGenerationService
    {
        private readonly SafeFileWriteService safeFileWriteService = new SafeFileWriteService();

        private static readonly string[] DefaultSingleFileExtensions =
        {
            ".nes", ".sfc", ".smc", ".n64", ".z64", ".v64",
            ".gb", ".gbc", ".gba",
            ".gg", ".gen", ".md", ".sms", ".32x",
            ".rvz", ".gcz", ".iso", ".chd", ".cso", ".pbp",
            ".zip", ".7z",
            ".xci", ".nsp", ".3ds", ".cia",
            ".exe", ".bat", ".cmd", ".lnk"
        };

        private static readonly string[] DefaultDiscLaunchExtensions =
        {
            ".m3u", ".cue", ".chd", ".pbp", ".iso", ".exe", ".bat", ".cmd", ".lnk"
        };

        private static readonly string[] DefaultIgnoredExtensions =
        {
            ".xml", ".json", ".txt",
            ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp",
            ".mp4", ".mkv", ".avi",
            ".sav", ".srm", ".state",
            ".h3", ".tik", ".tmd", ".cert", ".app"
        };

        private static readonly string[] DefaultExcludedFolders =
        {
            "ROMcade_Data",
            "CloudLibrary",
            "MetadataCache",
            "ArtworkCache",
            "TitleAliases",
            "ExternalMetadata",
            "BIOS Menus",
            "Cracked",
            "Saves",
            "Save States",
            "Screenshots",
            "Manuals",
            "BoxArt",
            "Media",
            ".git",
            ".vs",
            "bin",
            "obj"
        };

        private static readonly Dictionary<string, string> PlatformAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Nintendo Entertainment System", "Nintendo NES" },
            { "Super Nintendo Entertainment System", "Nintendo SNES" },
            { "Nintendo 64", "Nintendo 64" },
            { "Game Boy", "Nintendo Game Boy" },
            { "Game Boy Color", "Nintendo Game Boy Color" },
            { "Game Boy Advance", "Nintendo Game Boy Advance" },
            { "GameCube", "Nintendo GameCube" },
            { "Wii U", "Nintendo Wii U" },
            { "Nintendo Switch", "Nintendo Switch" },
            { "3DS Backup", "Nintendo 3DS" },
            { "Nintendo 3DS", "Nintendo 3DS" },
            { "Sega Genesis", "Sega Genesis" },
            { "Game Gear", "Sega Game Gear" },
            { "Dreamcast", "Sega Dreamcast" },
            { "PlayStation", "Sony PlayStation" },
            { "PlayStation 2", "Sony PlayStation 2" },
            { "PlayStation 3", "Sony PlayStation 3" },
            { "PlayStation 4", "Sony PlayStation 4" },
            { "PlayStation Portable", "Sony PSP" },
            { "PSP", "Sony PSP" },
            { "Xbox", "Microsoft Xbox" },
            { "Xbox 360", "Microsoft Xbox 360" },
            { "PC", "PC" },
            { "Windows", "PC" }
        };

        public ManifestGenerationReport Generate(ManifestGenerationOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.SourceRoot))
            {
                throw new InvalidOperationException("A source root folder is required.");
            }

            if (!Directory.Exists(options.SourceRoot))
            {
                throw new DirectoryNotFoundException("The source root folder was not found: " + options.SourceRoot);
            }

            if (string.IsNullOrWhiteSpace(options.OutputPath))
            {
                throw new InvalidOperationException("An output manifest path is required.");
            }

            var includeExtensions = options.IncludeExtensions.Count == 0
                ? DefaultSingleFileExtensions
                : options.IncludeExtensions.ToArray();
            var normalizedIncludeExtensions = new HashSet<string>(
                includeExtensions.Select(NormalizeExtension),
                StringComparer.OrdinalIgnoreCase);
            var normalizedIgnoredExtensions = new HashSet<string>(
                DefaultIgnoredExtensions.Select(NormalizeExtension),
                StringComparer.OrdinalIgnoreCase);
            var excludedFolders = new HashSet<string>(
                DefaultExcludedFolders.Concat(options.ExcludeFolders ?? new List<string>()),
                StringComparer.OrdinalIgnoreCase);

            var rootFullPath = Path.GetFullPath(options.SourceRoot);
            var directories = Directory.GetDirectories(rootFullPath, "*", SearchOption.AllDirectories)
                .Select(path => new ScanEntry
                {
                    Path = NormalizeSourcePath(path.Substring(rootFullPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    IsDirectory = true
                })
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
                .ToList();
            var files = Directory.GetFiles(rootFullPath, "*", SearchOption.AllDirectories)
                .Select(path => new ScanEntry
                {
                    Path = NormalizeSourcePath(path.Substring(rootFullPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    IsDirectory = false
                })
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Path))
                .ToList();

            var report = new ManifestGenerationReport
            {
                SourceRoot = rootFullPath,
                OutputPath = options.OutputPath,
                ReportPath = options.NoReport ? string.Empty : options.ReportPath,
                DirectoryCount = directories.Count,
                FileCount = files.Count,
                ScannedEntryCount = directories.Count + files.Count
            };

            var directoryPaths = new HashSet<string>(directories.Select(d => d.Path), StringComparer.OrdinalIgnoreCase);
            var filesByParent = files
                .GroupBy(file => GetParentPath(file.Path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

            var items = new List<PersonalCloudLibraryItem>();
            var detectedDirectories = new List<string>();

            foreach (var directory in directories)
            {
                if (IsUnderExcludedFolder(directory.Path, excludedFolders))
                {
                    report.SkippedEntries.Add("Skipped excluded folder candidate: " + directory.Path);
                    continue;
                }

                var pathParts = GetPathParts(directory.Path);
                var directoryName = pathParts[pathParts.Length - 1];

                var codeKey = NormalizeSourcePath(directory.Path + "/code");
                var contentKey = NormalizeSourcePath(directory.Path + "/content");
                var metaKey = NormalizeSourcePath(directory.Path + "/meta");
                var isWiiUPackage = directoryPaths.Contains(codeKey) &&
                    directoryPaths.Contains(contentKey) &&
                    directoryPaths.Contains(metaKey);

                if (isWiiUPackage)
                {
                    var role = GetPackageRole(directoryName);
                    if (!options.IncludeNonLaunchablePackages && !string.Equals(role, "game", StringComparison.OrdinalIgnoreCase))
                    {
                        report.SkippedEntries.Add("Skipped Wii U non-game package: " + directory.Path);
                        continue;
                    }

                    items.Add(NewManifestItem(
                        RemoveKnownGameSuffixes(directoryName),
                        GetPlatformFromPath(directory.Path),
                        directory.Path,
                        "directory",
                        ConvertToCachePath(directory.Path),
                        ConvertToCachePath(directory.Path),
                        string.Empty,
                        role));
                    detectedDirectories.Add(directory.Path);
                    continue;
                }

                List<ScanEntry> childFiles;
                if (!filesByParent.TryGetValue(directory.Path, out childFiles))
                {
                    continue;
                }

                ScanEntry preferredLaunch = null;
                foreach (var extension in DefaultDiscLaunchExtensions)
                {
                    preferredLaunch = childFiles
                        .Where(file => string.Equals(GetExtensionFromPath(file.Path), extension, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault();
                    if (preferredLaunch != null)
                    {
                        break;
                    }
                }

                if (preferredLaunch == null)
                {
                    continue;
                }

                var launchPath = NormalizeSourcePath(preferredLaunch.Path);
                items.Add(NewManifestItem(
                    RemoveKnownGameSuffixes(directoryName),
                    GetPlatformFromPath(directory.Path),
                    directory.Path,
                    "directory",
                    ConvertToCachePath(launchPath),
                    ConvertToCachePath(directory.Path),
                    GetFileNameFromPath(launchPath),
                    string.Empty));
                detectedDirectories.Add(directory.Path);
            }

            foreach (var file in files)
            {
                if (IsUnderExcludedFolder(file.Path, excludedFolders))
                {
                    report.SkippedEntries.Add("Skipped file under excluded folder: " + file.Path);
                    continue;
                }

                if (IsUnderDetectedDirectory(file.Path, detectedDirectories))
                {
                    report.SkippedEntries.Add("Skipped file inside detected directory package: " + file.Path);
                    continue;
                }

                var extension = GetExtensionFromPath(file.Path);
                if (normalizedIgnoredExtensions.Contains(extension))
                {
                    report.SkippedEntries.Add("Skipped ignored extension: " + file.Path);
                    continue;
                }

                if (string.Equals(extension, ".bin", StringComparison.OrdinalIgnoreCase))
                {
                    report.SkippedEntries.Add("Skipped standalone .bin item: " + file.Path);
                    continue;
                }

                if (!normalizedIncludeExtensions.Contains(extension))
                {
                    report.SkippedEntries.Add("Skipped unsupported extension: " + file.Path);
                    continue;
                }

                var fileName = GetFileNameFromPath(file.Path);
                var installDirectory = ConvertToCachePath(GetParentPath(file.Path));
                if (string.IsNullOrWhiteSpace(installDirectory))
                {
                    installDirectory = ".";
                }

                items.Add(NewManifestItem(
                    RemoveKnownGameSuffixes(Path.GetFileNameWithoutExtension(fileName)),
                    GetPlatformFromPath(file.Path),
                    file.Path,
                    "file",
                    ConvertToCachePath(file.Path),
                    installDirectory,
                    fileName,
                    string.Empty));
            }

            var sortedItems = items
                .OrderBy(item => item.Platform, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var duplicateGroups = sortedItems
                .GroupBy(item => (item.Platform ?? string.Empty) + "|" + (item.Title ?? string.Empty), StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                report.Warnings.Add("Duplicate-looking title group: " + group.Key + " has " + group.Count() + " entries");
            }

            report.ItemCount = sortedItems.Count;
            report.DetectedDirectories = detectedDirectories;
            report.DetectedDirectoryItemCount = detectedDirectories.Count;
            report.Manifest = new PersonalCloudLibraryManifest
            {
                Version = 3,
                GeneratedBy = "Personal Cloud Library Source manifest generator",
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                SourceMode = "filesystem",
                ItemCount = sortedItems.Count,
                Items = sortedItems
            };

            var outputDirectory = Path.GetDirectoryName(options.OutputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            safeFileWriteService.WriteAllText(
                options.OutputPath,
                Serialization.ToJson(report.Manifest),
                options.BackupDirectory,
                createBackup: true);

            if (!options.NoReport && !string.IsNullOrWhiteSpace(options.ReportPath))
            {
                var reportDirectory = Path.GetDirectoryName(options.ReportPath);
                if (!string.IsNullOrWhiteSpace(reportDirectory))
                {
                    Directory.CreateDirectory(reportDirectory);
                }

                safeFileWriteService.WriteAllLines(
                    options.ReportPath,
                    BuildReportLines(report),
                    options.BackupDirectory,
                    createBackup: true);
            }

            return report;
        }

        private static IEnumerable<string> BuildReportLines(ManifestGenerationReport report)
        {
            var lines = new List<string>
            {
                "Personal Cloud Library Source Manifest Generator Report",
                "================================================",
                "Generated at: " + report.Manifest.GeneratedAt,
                "Source mode: " + report.Manifest.SourceMode,
                "Source root: " + report.SourceRoot,
                "Output: " + report.OutputPath,
                string.Empty,
                "Total scanned entries: " + report.ScannedEntryCount,
                "Total directories: " + report.DirectoryCount,
                "Total files: " + report.FileCount,
                "Generated item count: " + report.ItemCount,
                "Detected directory item count: " + report.DetectedDirectoryItemCount,
                "Skipped entry count: " + report.SkippedEntries.Count,
                "Warnings count: " + report.Warnings.Count,
                string.Empty,
                "Detected Directory Items",
                "-----------------------"
            };

            if (report.DetectedDirectories.Count == 0)
            {
                lines.Add("None");
            }
            else
            {
                lines.AddRange(report.DetectedDirectories.Select(directory => "- " + directory));
            }

            lines.Add(string.Empty);
            lines.Add("Warnings");
            lines.Add("--------");
            if (report.Warnings.Count == 0)
            {
                lines.Add("None");
            }
            else
            {
                lines.AddRange(report.Warnings.Select(warning => "- " + warning));
            }

            lines.Add(string.Empty);
            lines.Add("Skipped Entries");
            lines.Add("---------------");
            if (report.SkippedEntries.Count == 0)
            {
                lines.Add("None");
            }
            else
            {
                lines.AddRange(report.SkippedEntries.Take(2000).Select(skip => "- " + skip));
                if (report.SkippedEntries.Count > 2000)
                {
                    lines.Add("- ... truncated. Total skipped entries: " + report.SkippedEntries.Count);
                }
            }

            return lines;
        }

        private static PersonalCloudLibraryItem NewManifestItem(
            string title,
            string platform,
            string sourcePath,
            string sourceType,
            string cachePath,
            string installDirectory,
            string launchFile,
            string packageRole)
        {
            var item = new PersonalCloudLibraryItem
            {
                Id = NewSlug(platform + " " + title + " " + sourcePath),
                Title = title,
                Platform = platform,
                SourcePath = sourcePath,
                SourceType = sourceType,
                CachePath = cachePath,
                InstallDirectory = installDirectory,
                LaunchFile = launchFile,
                Notes = "Generated by Personal Cloud Library Source manifest generator."
            };

            if (!string.IsNullOrWhiteSpace(packageRole))
            {
                item.PackageRole = packageRole;
            }

            return item;
        }

        private static bool IsUnderExcludedFolder(string path, HashSet<string> excludedFolders)
        {
            foreach (var part in GetPathParts(path))
            {
                if (excludedFolders.Contains(part))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnderDetectedDirectory(string filePath, List<string> detectedDirectories)
        {
            var normalizedFile = NormalizeSourcePath(filePath);
            foreach (var directory in detectedDirectories)
            {
                var normalizedDirectory = NormalizeSourcePath(directory).TrimEnd('/') + "/";
                if (normalizedFile.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] GetPathParts(string path)
        {
            return NormalizeSourcePath(path)
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string GetPlatformFromPath(string path)
        {
            var parts = GetPathParts(path);
            if (parts.Length == 0)
            {
                return "Unknown";
            }

            string alias;
            if (PlatformAliases.TryGetValue(parts[0], out alias))
            {
                return alias;
            }

            return parts[0];
        }

        private static string NormalizeSourcePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim('/');
        }

        private static string GetParentPath(string path)
        {
            var normalized = NormalizeSourcePath(path);
            var lastSlash = normalized.LastIndexOf('/');
            return lastSlash < 0 ? string.Empty : normalized.Substring(0, lastSlash);
        }

        private static string GetExtensionFromPath(string path)
        {
            return Path.GetExtension(GetFileNameFromPath(path)).ToLowerInvariant();
        }

        private static string GetFileNameFromPath(string path)
        {
            return Path.GetFileName(NormalizeSourcePath(path).Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ConvertToCachePath(string sourcePath)
        {
            return NormalizeSourcePath(sourcePath).Replace("/", "\\");
        }

        private static string RemoveKnownGameSuffixes(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            var result = System.Text.RegularExpressions.Regex.Replace(title, @"\s*\((USA|Europe|Japan|World|En|Fr|De|Es|It|Rev\s*\d+|Beta|Proto|Demo)[^)]*\)\s*", " ");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s*\[(Game|DLC|Update)\]\s*", " ");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s*\[[0-9A-Fa-f]{8,16}\]\s*", " ");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(result) ? title : result;
        }

        private static string NewSlug(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "item";
            }

            var slug = System.Text.RegularExpressions.Regex.Replace(text.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            return string.IsNullOrWhiteSpace(slug) ? "item" : slug;
        }

        private static string GetPackageRole(string folderName)
        {
            if (folderName.IndexOf("[DLC]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "dlc";
            }

            if (folderName.IndexOf("[Update]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "update";
            }

            return "game";
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return string.Empty;
            }

            return extension.StartsWith(".") ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        }

        private class ScanEntry
        {
            public string Path { get; set; }
            public bool IsDirectory { get; set; }
        }
    }
}
