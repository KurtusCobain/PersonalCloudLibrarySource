using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

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

        // Capture an identity from the actual opened handle, not a second
        // path lookup. Use the full Windows 128-bit file ID (including ReFS).
        internal struct FileIdentity
        {
            public ulong VolumeSerial;
            public ulong IdLow;
            public ulong IdHigh;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HandleInformation
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerial;
            public uint SizeHigh;
            public uint SizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out HandleInformation information);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,
            int informationClass, out FileIdentity information, uint size);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode,
            ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle OpenNativeFile(string path, uint access, uint share,
            IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle handle,
            int informationClass, ref byte information, uint size);

        internal static FileIdentity CapturePrivateFileIdentity(SafeFileHandle handle)
        {
            HandleInformation information;
            FileIdentity identity;
            // FileIdInfo = 18; refuse unknown identity or multiple hardlinks.
            if (!GetFileInformationByHandle(handle, out information) ||
                (information.Attributes & (uint)(FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                information.NumberOfLinks != 1 ||
                !GetFileInformationByHandleEx(handle, 18, out identity,
                    (uint)Marshal.SizeOf(typeof(FileIdentity))) ||
                (identity.IdLow == 0 && identity.IdHigh == 0))
                throw new IOException("Private file identity or link count could not be verified.");
            return identity;
        }

        internal static bool MatchesPrivateFileIdentity(SafeFileHandle handle, FileIdentity expected)
        {
            var current = CapturePrivateFileIdentity(handle);
            return current.VolumeSerial == expected.VolumeSerial &&
                current.IdLow == expected.IdLow && current.IdHigh == expected.IdHigh;
        }

        internal static FileStream OpenPrivateFileForReview(string path, bool deleteAccess)
        {
            RequireSafePath(path);
            // GENERIC_READ | optional DELETE, FILE_SHARE_READ, OPEN_EXISTING,
            // FILE_FLAG_OPEN_REPARSE_POINT. No pathname delete after verification.
            var handle = OpenNativeFile(Path.GetFullPath(path),
                0x80000000u | (deleteAccess ? 0x00010000u : 0u), 1u,
                IntPtr.Zero, 3u, 0x00200000u, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new IOException("Could not open private file for identity review.",
                    new System.ComponentModel.Win32Exception(error));
            }
            try { return new FileStream(handle, FileAccess.Read); }
            catch { handle.Dispose(); throw; }
        }

        internal static void DeleteVerifiedFileHandle(SafeFileHandle handle)
        {
            // FILE_DISPOSITION_INFO.DeleteFile is a one-byte BOOLEAN.
            // Mark exactly the already-verified open file for deletion on close.
            byte delete = 1;
            if (!SetFileInformationByHandle(handle, 4, ref delete, 1u))
                throw new IOException("Verified private file could not be marked for deletion.",
                    new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
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
