using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PersonalCloudLibrarySource
{
    public static class TransferPartialPathPolicy
    {
        // A staging name is not proof of ownership. Refuse collisions and
        // inaccessible/linked paths; never erase pre-existing content to start.
        internal static void RequireVacant(string path)
        {
            RequireSafePath(path);
            try { File.GetAttributes(path); }
            catch (FileNotFoundException) { return; }
            catch (DirectoryNotFoundException) { return; }
            throw new IOException("Transfer staging already exists. Preserve it for review.");
        }

        internal static void RequireSafePath(string path)
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(path));
            var authorization = new SafeCacheDeletionPolicy().Authorize(parent, path, false);
            if (!authorization.Allowed)
                throw new IOException("Transfer staging path cannot be verified: " + authorization.Reason);
        }

        // Directory.CreateDirectory is idempotent: it cannot establish that
        // this invocation won creation of a previously vacant staging root.
        // Use Windows' non-idempotent single-component creation instead.
        internal static void CreateExclusiveDirectory(string path)
        {
            RequireVacant(path);
            if (!CreateDirectoryNative(Path.GetFullPath(path), IntPtr.Zero))
                throw new IOException("Could not exclusively create private staging directory.",
                    new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode,
            SetLastError = true, ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateDirectoryNative(string path, IntPtr securityAttributes);

        internal static void RequirePublicationPaths(string staging, string destination, bool directory)
        {
            RequireSafePath(staging);
            var attributes = File.GetAttributes(staging);
            if ((attributes & FileAttributes.ReparsePoint) != 0 ||
                ((attributes & FileAttributes.Directory) != 0) != directory)
                throw new IOException("Transfer staging changed before publication.");
            RequireVacant(destination);
        }

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
