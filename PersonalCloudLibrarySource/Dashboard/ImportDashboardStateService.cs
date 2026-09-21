using System;

namespace PersonalCloudLibrarySource
{
    public static class ImportDashboardStateService
    {
        public static LibraryVerificationReport CreateReport(ImportOutcome outcome, string diagnosticsPath)
        {
            if (outcome == null) throw new ArgumentNullException(nameof(outcome));
            return new LibraryVerificationReport
            {
                GeneratedAt = DateTime.UtcNow.ToString("O"),
                ReportPath = diagnosticsPath ?? string.Empty,
                ManifestSource = outcome.Source,
                ManifestLoadSucceeded = outcome.Succeeded,
                ManifestLoadError = outcome.Error,
                TotalManifestItems = outcome.Succeeded ? outcome.ValidItems.Count : 0
            };
        }
    }
}
