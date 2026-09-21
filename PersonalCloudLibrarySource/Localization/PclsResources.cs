using Playnite.SDK;
using System;
using System.Globalization;

namespace PersonalCloudLibrarySource
{
    internal static class PclsResources
    {
        internal const string LaunchFileIdentifier = "launchFile";
        internal const string CachePathIdentifier = "cachePath";
        internal const string InstallDirectoryIdentifier = "installDirectory";

        public static string Get(string key, string fallback)
        {
            try
            {
                var value = ResourceProvider.GetString(key);
                return string.IsNullOrEmpty(value) ||
                       string.Equals(value, "<!" + key + "!>", StringComparison.Ordinal)
                    ? fallback
                    : value;
            }
            catch
            {
                return fallback;
            }
        }

        public static string Format(string key, string fallback, params object[] arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key, fallback), arguments ?? new object[0]);
        }
    }
}
