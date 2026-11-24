using System;

namespace PersonalCloudLibrarySource
{
    public sealed class LibraryStatusService
    {
        public CloudLibraryDashboardState BuildState(
            PersonalCloudLibrarySourceSettings settings,
            LibraryStatusContext context)
        {
            context = VerificationDashboardStateService.Apply(
                context ?? new LibraryStatusContext(),
                VerificationDashboardStateService.LatestReport);

            var setupComplete = IsSetupComplete(settings);
            var status = ResolveStatus(setupComplete, context);

            return new CloudLibraryDashboardState(
                status,
                GetStatusText(status),
                FriendlySourceNameProvider.GetDisplayName(settings?.SourceProviderType),
                context.SourceDescription,
                context.ManifestDescription,
                context.CachePath,
                setupComplete,
                context.SourceAvailable,
                Math.Max(0, context.ManifestItemCount),
                Math.Max(0, context.ImportedGameCount),
                Math.Max(0, context.CachedGameCount),
                Math.Max(0, context.WarningCount),
                Math.Max(0, context.ActiveTransferCount),
                Math.Max(0, context.FailedTransferCount));
        }

        public bool IsSetupComplete(PersonalCloudLibrarySourceSettings settings)
        {
            return settings != null && settings.Enabled &&
                SettingsMigrationService.HasConfiguredSource(settings);
        }

        private static DashboardStatusKind ResolveStatus(bool setupComplete, LibraryStatusContext context)
        {
            if (!setupComplete)
            {
                return DashboardStatusKind.NeedsSetup;
            }

            if (context.FailedTransferCount > 0)
            {
                return DashboardStatusKind.TransferFailed;
            }

            if (context.ActiveTransferCount > 0)
            {
                return DashboardStatusKind.Downloading;
            }

            if (context.IsUpdating)
            {
                return DashboardStatusKind.Updating;
            }

            if (!context.SourceAvailable)
            {
                return DashboardStatusKind.SourceUnavailable;
            }

            if (context.WarningCount > 0)
            {
                return DashboardStatusKind.VerificationWarnings;
            }

            return DashboardStatusKind.Ready;
        }

        private static string GetStatusText(DashboardStatusKind status)
        {
            switch (status)
            {
                case DashboardStatusKind.NeedsSetup:
                    return PclsResources.Get("LOCPLSStatusNeedsSetup", "Needs setup");
                case DashboardStatusKind.SourceUnavailable:
                    return PclsResources.Get("LOCPLSStatusSourceUnavailable", "Source unavailable");
                case DashboardStatusKind.VerificationWarnings:
                    return PclsResources.Get("LOCPLSStatusVerificationWarnings", "Verification warnings");
                case DashboardStatusKind.Updating:
                    return PclsResources.Get("LOCPLSStatusUpdating", "Updating");
                case DashboardStatusKind.Downloading:
                    return PclsResources.Get("LOCPLSStatusDownloading", "Downloading");
                case DashboardStatusKind.TransferFailed:
                    return PclsResources.Get("LOCPLSStatusTransferFailed", "Transfer failed");
                default:
                    return PclsResources.Get("LOCPLSStatusReady", "Ready");
            }
        }
    }
}
