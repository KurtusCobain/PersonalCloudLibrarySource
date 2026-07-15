using NUnit.Framework;
using System;

namespace PersonalCloudLibrarySource.Tests.Dashboard
{
    [TestFixture]
    public class VerificationDashboardStateServiceTests
    {
        [Test]
        public void Apply_FailedManifestClearsUnprovenManifestCountAndPreservesLiveLibraryCounts()
        {
            var context = new LibraryStatusContext
            {
                SourceAvailable = true,
                ManifestItemCount = 4,
                ImportedGameCount = 4,
                CachedGameCount = 4
            };
            var report = new LibraryVerificationReport
            {
                ManifestLoadSucceeded = false,
                ManifestLoadError = "The rclone manifest operation timed out after 30 seconds.",
                TotalManifestItems = 0,
                CachedInstalledCount = 0
            };
            report.WarningSamples.Add("Manifest load failed: The rclone manifest operation timed out after 30 seconds.");

            var result = InvokeApply(context, report);

            Assert.That(result.SourceAvailable, Is.False);
            Assert.That(result.ManifestItemCount, Is.Zero);
            Assert.That(result.ImportedGameCount, Is.EqualTo(4));
            Assert.That(result.CachedGameCount, Is.EqualTo(4));
            Assert.That(result.WarningCount, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void Apply_SuccessfulManifestUsesFreshVerificationCounts()
        {
            var context = new LibraryStatusContext
            {
                SourceAvailable = true,
                ManifestItemCount = 2,
                ImportedGameCount = 4,
                CachedGameCount = 3
            };
            var report = new LibraryVerificationReport
            {
                ManifestLoadSucceeded = true,
                TotalManifestItems = 7,
                CachedInstalledCount = 5
            };

            var result = InvokeApply(context, report);

            Assert.That(result.SourceAvailable, Is.True);
            Assert.That(result.ManifestItemCount, Is.EqualTo(7));
            Assert.That(result.CachedGameCount, Is.EqualTo(5));
            Assert.That(result.WarningCount, Is.EqualTo(0));
        }

        [Test]
        public void VerificationMessage_IncludesManifestErrorAndTimeoutGuidance()
        {
            var report = new LibraryVerificationReport
            {
                ManifestLoadSucceeded = false,
                ManifestLoadError = "The rclone manifest operation timed out after 30 seconds.",
                ReportPath = @"D:\Reports\latest-verification-report.txt"
            };
            report.WarningSamples.Add("Manifest load failed: The rclone manifest operation timed out after 30 seconds.");

            var assembly = typeof(LibraryStatusService).Assembly;
            var type = assembly.GetType("PersonalCloudLibrarySource.VerificationMessageBuilder");
            Assert.That(type, Is.Not.Null, "VerificationMessageBuilder must exist.");
            var method = type.GetMethod("Build", new[] { typeof(LibraryVerificationReport) });
            Assert.That(method, Is.Not.Null, "VerificationMessageBuilder.Build must exist.");

            var message = (string)method.Invoke(null, new object[] { report });

            StringAssert.Contains("timed out after 30 seconds", message);
            StringAssert.Contains("Increase the rclone timeout", message);
            StringAssert.Contains(report.ReportPath, message);
        }

        private static LibraryStatusContext InvokeApply(LibraryStatusContext context, LibraryVerificationReport report)
        {
            var assembly = typeof(LibraryStatusService).Assembly;
            var type = assembly.GetType("PersonalCloudLibrarySource.VerificationDashboardStateService");
            Assert.That(type, Is.Not.Null, "VerificationDashboardStateService must exist.");
            var method = type.GetMethod("Apply", new[] { typeof(LibraryStatusContext), typeof(LibraryVerificationReport) });
            Assert.That(method, Is.Not.Null, "VerificationDashboardStateService.Apply must exist.");
            return (LibraryStatusContext)method.Invoke(null, new object[] { context, report });
        }
    }
}
