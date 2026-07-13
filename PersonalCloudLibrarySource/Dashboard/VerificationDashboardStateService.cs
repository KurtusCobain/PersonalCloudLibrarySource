using System;

namespace PersonalCloudLibrarySource
{
    public static class VerificationDashboardStateService
    {
        public static LibraryStatusContext Apply(
            LibraryStatusContext context,
            LibraryVerificationReport report)
        {
            context = context ?? new LibraryStatusContext();
            if (report == null)
            {
                return context;
            }

            context.WarningCount = Math.Max(
                context.WarningCount,
                report.WarningSamples?.Count ?? 0);

            if (!report.ManifestLoadSucceeded)
            {
                context.SourceAvailable = false;
                return context;
            }

            context.SourceAvailable = true;
            context.ManifestItemCount = Math.Max(0, report.TotalManifestItems);
            context.CachedGameCount = Math.Max(0, report.CachedInstalledCount);
            return context;
        }
    }

    public static class VerificationMessageBuilder
    {
        public static string Build(LibraryVerificationReport report)
        {
            if (report == null)
            {
                return "Setup verification did not return a report.";
            }

            var passed = report.ConfigurationErrorsCount == 0 && report.ManifestLoadSucceeded;
            var message = passed
                ? "Setup verification completed."
                : "Setup verification found issues.";

            message += Environment.NewLine + Environment.NewLine +
                "Manifest load: " + (report.ManifestLoadSucceeded ? "Succeeded" : "Failed") + Environment.NewLine;

            if (!string.IsNullOrWhiteSpace(report.ManifestLoadError))
            {
                message += "Manifest error: " + report.ManifestLoadError + Environment.NewLine;
                if (report.ManifestLoadError.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    message += "Increase the rclone timeout in Advanced settings, then verify again." + Environment.NewLine;
                }
            }

            message +=
                "Items found: " + report.TotalManifestItems + Environment.NewLine +
                "Download/cache-eligible: " + report.DownloadEligibleCount + Environment.NewLine +
                "Cached or installed: " + report.CachedInstalledCount + Environment.NewLine +
                "Warnings sampled: " + (report.WarningSamples?.Count ?? 0) + Environment.NewLine +
                "Configuration errors: " + report.ConfigurationErrorsCount + Environment.NewLine +
                Environment.NewLine +
                "Verification report:" + Environment.NewLine +
                (report.ReportPath ?? string.Empty) + Environment.NewLine + Environment.NewLine +
                (passed
                    ? "Next: save settings if needed, then run Update Game Library in Playnite."
                    : "Next: review the verification report, fix the flagged issues, and run verification again.");

            return message;
        }
    }
}
