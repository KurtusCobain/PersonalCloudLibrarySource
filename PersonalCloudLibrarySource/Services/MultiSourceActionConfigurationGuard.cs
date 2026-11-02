using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PersonalCloudLibrarySource
{
    // A frozen runtime plan is tied to the exact validated profile document
    // and action-relevant shared settings used when its routes were built.
    // This is drift detection, not authenticity against a local attacker.
    public static class MultiSourceActionConfigurationGuard
    {
        public static string Fingerprint(
            IReadOnlyList<PclsSourceProfileDefinition> profiles,
            PersonalCloudLibrarySourceSettingsV3 shared)
        {
            if (shared == null || !shared.Enabled || !shared.EnableMultiSourceProfiles ||
                shared.AllowUninstallOutsideCacheFolder)
                throw new InvalidOperationException("Unsafe or disabled multi-source settings.");
            // Codec enforces bounded, valid profile identities and providers;
            // preserving document order is conservative: reorder forces refresh.
            var sourceDocument = new SourceProfilesConfigurationCodec().Serialize(profiles);
            var builder = new StringBuilder("pcls-route-configuration-v1;");
            Append(builder, sourceDocument);
            Append(builder, shared.LocalCacheFolder);
            Append(builder, shared.TreatMissingFilesAsUninstalled ? "1" : "0");
            Append(builder, shared.AllowDownloads ? "1" : "0");
            Append(builder, shared.UninstallBehavior);
            Append(builder, shared.TransferConcurrency.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            Append(builder, shared.VerifyAfterTransfer ? "1" : "0");
            Append(builder, shared.RemoveIncompleteTransferFiles ? "1" : "0");
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(
                    Encoding.UTF8.GetBytes(builder.ToString()))).Replace("-", "");
        }

        public static bool Matches(MultiSourceRuntimePlan plan,
            IReadOnlyList<PclsSourceProfileDefinition> currentProfiles,
            PersonalCloudLibrarySourceSettingsV3 currentShared)
        {
            if (plan == null || !plan.CanPublish ||
                string.IsNullOrWhiteSpace(plan.ConfigurationFingerprint))
                return false;
            try
            {
                return string.Equals(plan.ConfigurationFingerprint,
                    Fingerprint(currentProfiles, currentShared),
                    StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is InvalidOperationException ||
                                       ex is ArgumentException)
            {
                return false;
            }
        }

        private static void Append(StringBuilder builder, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            builder.Append(bytes.Length).Append(':')
                .Append(Convert.ToBase64String(bytes)).Append(';');
        }
    }
}
