using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PersonalCloudLibrarySource
{
    public static class RcloneProgressParser
    {
        private static readonly Regex TransferRegex = new Regex(
            @"Transferred:\s*([0-9]+(?:\.[0-9]+)?)\s*([KMGT]?i?B)\s*/\s*([0-9]+(?:\.[0-9]+)?)\s*([KMGT]?i?B)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool TryParse(string line, out long transferredBytes, out long totalBytes)
        {
            transferredBytes = 0;
            totalBytes = 0;
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            var match = TransferRegex.Match(line);
            if (!match.Success)
            {
                return false;
            }

            double transferred;
            double total;
            if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out transferred) ||
                !double.TryParse(match.Groups[3].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out total))
            {
                return false;
            }

            transferredBytes = ToBytes(transferred, match.Groups[2].Value);
            totalBytes = ToBytes(total, match.Groups[4].Value);
            return totalBytes >= 0 && transferredBytes >= 0;
        }

        private static long ToBytes(double value, string unit)
        {
            var normalized = (unit ?? "B").Trim().ToUpperInvariant();
            double multiplier;
            switch (normalized)
            {
                case "KB":
                    multiplier = 1000d;
                    break;
                case "KIB":
                    multiplier = 1024d;
                    break;
                case "MB":
                    multiplier = 1000d * 1000d;
                    break;
                case "MIB":
                    multiplier = 1024d * 1024d;
                    break;
                case "GB":
                    multiplier = 1000d * 1000d * 1000d;
                    break;
                case "GIB":
                    multiplier = 1024d * 1024d * 1024d;
                    break;
                case "TB":
                    multiplier = 1000d * 1000d * 1000d * 1000d;
                    break;
                case "TIB":
                    multiplier = 1024d * 1024d * 1024d * 1024d;
                    break;
                default:
                    multiplier = 1d;
                    break;
            }

            return Convert.ToInt64(Math.Round(Math.Max(0d, value) * multiplier));
        }
    }
}
