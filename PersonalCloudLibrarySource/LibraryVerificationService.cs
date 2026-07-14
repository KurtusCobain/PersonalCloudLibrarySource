using Playnite.SDK.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PersonalCloudLibrarySource
{
    public class LibraryVerificationService
    {
        private const int WarningSampleLimit = 25;

        public LibraryVerificationReport BuildReport(
            PersonalCloudLibrarySourceSettings settings,
            string manifestSource,
            string reportPath,
            PersonalCloudLibraryManifest manifest,
            Exception manifestLoadException,
            IEnumerable<string> configurationErrors,
            IEnumerable<Game> existingGames,
            Guid pluginId)
        {
            var report = new LibraryVerificationReport
            {
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                ReportPath = reportPath ?? string.Empty,
                ProviderMode = PersonalCloudLibrarySource.GetProviderType(settings),
                ManifestSource = string.IsNullOrWhiteSpace(manifestSource) ? "Not configured" : manifestSource,
                ManifestLoadSucceeded = manifest != null && manifestLoadException == null,
                ManifestLoadError = manifestLoadException?.Message ?? string.Empty
            };

            if (manifest != null)
            {
                report.ManifestVersion = manifest.Version;
                report.TotalManifestItems = manifest.Items?.Count ?? 0;
                PopulateManifestCounts(report, settings, manifest.Items ?? new List<PersonalCloudLibraryItem>());
            }

            if (configurationErrors != null)
            {
                foreach (var error in configurationErrors.Where(error => !string.IsNullOrWhiteSpace(error)))
                {
                    report.ConfigurationErrors.Add(error);
                }
            }

            report.ConfigurationErrorsCount = report.ConfigurationErrors.Count;
            if (manifestLoadException != null)
            {
                AddWarning(report, "Manifest load failed: " + manifestLoadException.Message);
            }

            PopulateLibraryMetadataSummary(report, existingGames, pluginId);
            return report;
        }

        public IEnumerable<string> BuildReportLines(LibraryVerificationReport report)
        {
            var lines = new List<string>
            {
                "Personal Cloud Library Source Verification Report",
                "===============================================",
                "Generated at: " + report.GeneratedAt,
                "Report path: " + report.ReportPath,
                "Provider mode: " + report.ProviderMode,
                "Manifest source: " + report.ManifestSource,
                "Manifest load: " + (report.ManifestLoadSucceeded ? "succeeded" : "failed"),
                "Manifest version: " + (report.ManifestVersion.HasValue ? report.ManifestVersion.Value.ToString(CultureInfo.InvariantCulture) : "unknown"),
                "Manifest load error: " + (string.IsNullOrWhiteSpace(report.ManifestLoadError) ? "None" : report.ManifestLoadError),
                string.Empty,
                "Privacy note: this report summarizes counts and capped warnings. It does not dump the full manifest inventory.",
                string.Empty,
                "Configuration",
                "-------------",
                "Configuration errors: " + report.ConfigurationErrorsCount.ToString(CultureInfo.InvariantCulture)
            };

            if (report.ConfigurationErrorsCount == 0)
            {
                lines.Add("Configuration status: basic settings look valid");
            }
            else
            {
                lines.AddRange(report.ConfigurationErrors.Select(error => "- " + error));
            }

            lines.Add(string.Empty);
            lines.Add("Manifest Summary");
            lines.Add("----------------");
            lines.Add("Total manifest items: " + report.TotalManifestItems.ToString(CultureInfo.InvariantCulture));
            lines.Add("Duplicate IDs: " + report.DuplicateIdCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Missing IDs: " + report.MissingIdCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Missing titles: " + report.MissingTitleCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Invalid or missing source paths: " + report.InvalidOrMissingSourcePathCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Invalid or missing cache paths: " + report.InvalidOrMissingCachePathCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("sourceType=file count: " + report.SourceTypeFileCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("sourceType=directory count: " + report.SourceTypeDirectoryCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("sourceType=unknown count: " + report.SourceTypeUnknownCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Installed or cached items: " + report.CachedInstalledCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Missing-local or cloud-only items: " + report.MissingLocalCloudOnlyCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Download/cache-eligible items: " + report.DownloadEligibleCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Uncacheable or misconfigured items: " + report.UncacheableMisconfiguredCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Rclone path-doubling warnings: " + report.RclonePathDoublingWarningCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("LocalFolder path warnings: " + report.LocalFolderPathWarningCount.ToString(CultureInfo.InvariantCulture));
            lines.Add(string.Empty);
            lines.Add("Manifest Metadata Gaps");
            lines.Add("----------------------");
            lines.Add("Entries missing description: " + report.MissingDescriptionCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Entries missing platform: " + report.MissingPlatformCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Entries missing local play action because cache is absent: " + report.MissingPlayActionCount.ToString(CultureInfo.InvariantCulture));
            lines.Add(string.Empty);
            lines.Add("Library Metadata Gaps");
            lines.Add("---------------------");
            lines.Add("Current plugin-owned games found in Playnite DB: " + report.LibraryOwnedGameCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Plugin-owned games missing cover image: " + report.LibraryMissingCoverImageCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Plugin-owned games missing background image: " + report.LibraryMissingBackgroundImageCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Plugin-owned games missing description: " + report.LibraryMissingDescriptionCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Plugin-owned games missing platform metadata: " + report.LibraryMissingPlatformCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Plugin-owned games missing play action: " + report.LibraryMissingPlayActionCount.ToString(CultureInfo.InvariantCulture));
            lines.Add(string.Empty);
            lines.Add("Cache Safety Summary");
            lines.Add("--------------------");
            lines.Add("Items whose uninstall target resolves inside LocalCacheFolder: " + report.CacheOwnedPathCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Items whose uninstall target resolves outside LocalCacheFolder: " + report.CacheOutsidePathCount.ToString(CultureInfo.InvariantCulture));
            lines.Add("Items whose uninstall target could not be resolved safely: " + report.CacheUnresolvedPathCount.ToString(CultureInfo.InvariantCulture));
            lines.Add(string.Empty);
            lines.Add("Warning Samples");
            lines.Add("---------------");
        

            if (report.WarningSamples.Count == 0)
            {
                lines.Add("None");
            }
            else
            {
                lines.AddRange(report.WarningSamples.Select(warning => "- " + warning));
            }

            return lines;
        }

        private void PopulateManifestCounts(
            LibraryVerificationReport report,
            PersonalCloudLibrarySourceSettings settings,
            List<PersonalCloudLibraryItem> items)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                if (item == null)
                {
                    AddWarning(report, "Null manifest item encountered.");
                    report.UncacheableMisconfiguredCount++;
                    report.InvalidOrMissingSourcePathCount++;
                    report.InvalidOrMissingCachePathCount++;
                    continue;
                }

                var itemLabel = BuildItemLabel(item);
                if (string.IsNullOrWhiteSpace(item.Id))
                {
                    report.MissingIdCount++;
                    AddWarning(report, "Missing item ID: " + itemLabel);
                }
                else if (!ids.Add(item.Id))
                {
                    report.DuplicateIdCount++;
                    AddWarning(report, "Duplicate item ID: " + item.Id);
                }

                if (string.IsNullOrWhiteSpace(item.Title))
                {
                    report.MissingTitleCount++;
                    AddWarning(report, "Missing item title: " + itemLabel);
                }

                if (string.IsNullOrWhiteSpace(item.Notes))
                {
                    report.MissingDescriptionCount++;
                }

                if (string.IsNullOrWhiteSpace(item.Platform))
                {
                    report.MissingPlatformCount++;
                }

                CountSourceType(report, item.SourceType);

                var sourcePath = PersonalCloudLibrarySource.GetItemSourcePath(item);
                var sourceResolvable = !string.IsNullOrWhiteSpace(sourcePath) &&
                    PersonalCloudLibrarySource.CanResolveSourcePath(settings, sourcePath);
                if (!sourceResolvable)
                {
                    report.InvalidOrMissingSourcePathCount++;
                }

                if (PersonalCloudLibrarySource.HasRcloneContentRootPathDoublingRisk(settings, sourcePath))
                {
                    report.RclonePathDoublingWarningCount++;
                    AddWarning(report, "Possible rclone content-root doubling: " + RedactPath(sourcePath));
                }

                if (string.Equals(
                    PersonalCloudLibrarySource.GetProviderType(settings),
                    PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                    StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(sourcePath))
                {
                    var localSourcePath = PersonalCloudLibrarySource.ResolveLocalFolderSourcePath(settings, sourcePath);
                    if (string.IsNullOrWhiteSpace(localSourcePath) ||
                        (!File.Exists(localSourcePath) && !Directory.Exists(localSourcePath)))
                    {
                        report.LocalFolderPathWarningCount++;
                        AddWarning(report, "LocalFolder source path does not resolve: " + RedactPath(sourcePath));
                    }
                }

                var launchPath = PersonalCloudLibrarySource.ResolveLaunchPath(item, settings);
                var installDirectory = PersonalCloudLibrarySource.ResolveInstallDirectory(item, settings, launchPath);
                var downloadDestinationFile = PersonalCloudLibrarySource.ResolveDownloadDestinationFilePath(item, settings, launchPath);
                var downloadDestinationFolder = PersonalCloudLibrarySource.ResolveDownloadDestinationFolder(item, settings, launchPath);
                var cachePathValid = !string.IsNullOrWhiteSpace(downloadDestinationFile) || !string.IsNullOrWhiteSpace(downloadDestinationFolder);
                if (!cachePathValid)
                {
                    report.InvalidOrMissingCachePathCount++;
                    AddWarning(report, "Missing cache destination for item: " + itemLabel);
                }

                var itemState = new LibraryItemStateResolver().Resolve(
                    item,
                    launchPath,
                    installDirectory,
                    settings.TreatMissingFilesAsUninstalled);
                var launchExists = itemState.HasPlayAction;
                var cachedOrInstalled = itemState.IsCached;
                if (cachedOrInstalled)
                {
                    report.CachedInstalledCount++;
                }
                else
                {
                    report.MissingLocalCloudOnlyCount++;
                }

                if (!launchExists)
                {
                    report.MissingPlayActionCount++;
                }

                var downloadEligible = !cachedOrInstalled &&
                    settings != null &&
                    settings.AllowDownloads &&
                    sourceResolvable;
                if (downloadEligible)
                {
                    report.DownloadEligibleCount++;
                }
                else if (!cachedOrInstalled)
                {
                    report.UncacheableMisconfiguredCount++;
                }

                string refusalReason;
                var uninstallTarget = PersonalCloudLibrarySource.ResolveUninstallTargetPath(item, settings, launchPath, installDirectory);
                var safeUninstallTarget = PersonalCloudLibrarySource.ResolveSafeUninstallTarget(settings, uninstallTarget, out refusalReason);
                if (!string.IsNullOrWhiteSpace(safeUninstallTarget))
                {
                    if (PersonalCloudLibrarySource.IsPathInsideLocalCache(settings, safeUninstallTarget))
                    {
                        report.CacheOwnedPathCount++;
                    }
                    else
                    {
                        report.CacheOutsidePathCount++;
                    }
                }
                else
                {
                    report.CacheUnresolvedPathCount++;
                    if (!string.IsNullOrWhiteSpace(refusalReason))
                    {
                        AddWarning(report, "Cache safety warning for " + itemLabel + ": " + refusalReason);
                    }
                }
            }
        }

        private static void PopulateLibraryMetadataSummary(
            LibraryVerificationReport report,
            IEnumerable<Game> existingGames,
            Guid pluginId)
        {
            if (existingGames == null)
            {
                return;
            }

            foreach (var game in existingGames.Where(game => game != null && game.PluginId == pluginId))
            {
                report.LibraryOwnedGameCount++;

                if (IsBlankStringProperty(game, "CoverImage"))
                {
                    report.LibraryMissingCoverImageCount++;
                }

                if (IsBlankStringProperty(game, "BackgroundImage"))
                {
                    report.LibraryMissingBackgroundImageCount++;
                }

                if (IsBlankStringProperty(game, "Description"))
                {
                    report.LibraryMissingDescriptionCount++;
                }

                if (GetEnumerableCount(game, "PlatformIds") == 0 && GetEnumerableCount(game, "Platforms") == 0)
                {
                    report.LibraryMissingPlatformCount++;
                }

                if (GetEnumerableCount(game, "GameActions") == 0)
                {
                    report.LibraryMissingPlayActionCount++;
                }
            }
        }

        private static bool IsBlankStringProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                return false;
            }

            var value = property.GetValue(target) as string;
            return string.IsNullOrWhiteSpace(value);
        }

        private static int GetEnumerableCount(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                return 0;
            }

            var value = property.GetValue(target) as IEnumerable;
            if (value == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var _ in value)
            {
                count++;
            }

            return count;
        }

        private static void CountSourceType(LibraryVerificationReport report, string rawSourceType)
        {
            if (string.IsNullOrWhiteSpace(rawSourceType))
            {
                report.SourceTypeUnknownCount++;
                return;
            }

            if (rawSourceType.Trim().Equals("directory", StringComparison.OrdinalIgnoreCase))
            {
                report.SourceTypeDirectoryCount++;
                return;
            }

            if (rawSourceType.Trim().Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                report.SourceTypeFileCount++;
                return;
            }

            report.SourceTypeUnknownCount++;
        }

        private static string BuildItemLabel(PersonalCloudLibraryItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.Title))
            {
                return item.Title;
            }

            if (!string.IsNullOrWhiteSpace(item.Id))
            {
                return item.Id;
            }

            return RedactPath(PersonalCloudLibrarySource.GetItemSourcePath(item));
        }

        private static string RedactPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "<empty>";
            }

            var normalized = path.Replace('\\', '/').Trim('/');
            var parts = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 2)
            {
                return normalized;
            }

            return ".../" + parts[parts.Length - 2] + "/" + parts[parts.Length - 1];
        }

        private static void AddWarning(LibraryVerificationReport report, string warning)
        {
            if (report.WarningSamples.Count < WarningSampleLimit && !string.IsNullOrWhiteSpace(warning))
            {
                report.WarningSamples.Add(warning);
            }
        }
    }
}
