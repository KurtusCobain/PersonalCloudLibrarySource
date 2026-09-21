using System;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public static class TransferPartialPathPolicy
    {
        public static string Create(string destinationPath, Guid jobId)
        {
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new ArgumentException("Transfer destination path is required.", nameof(destinationPath));
            }

            if (jobId == Guid.Empty)
            {
                throw new ArgumentException("Transfer job identity is required.", nameof(jobId));
            }

            var destination = Path.GetFullPath(destinationPath);
            var parent = Path.GetDirectoryName(destination);
            if (string.IsNullOrWhiteSpace(parent))
            {
                throw new ArgumentException("Transfer destination must have a parent directory.", nameof(destinationPath));
            }

            var partial = destination + ".pcls-partial-" + jobId.ToString("N");
            if (!string.Equals(
                Path.GetDirectoryName(Path.GetFullPath(partial)),
                Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Transfer partial path escaped the verified destination parent.");
            }

            return partial;
        }
    }
}
