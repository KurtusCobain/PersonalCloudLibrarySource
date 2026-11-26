using System;
using System.Collections.Generic;
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

        internal static FileIdentity CreateOwnedDirectory(string path)
        {
            CreateExclusiveDirectory(path);
            using (var handle = OpenDirectoryForReview(path, false))
                return CaptureDirectoryIdentity(handle);
        }

        internal static FileIdentity ObserveDirectoryIdentity(string path)
        {
            using (var handle = OpenDirectoryForReview(path, false))
                return CaptureDirectoryIdentity(handle);
        }

        internal static void RequireDirectoryIdentity(string path, FileIdentity expected)
        {
            if (!SameIdentity(ObserveDirectoryIdentity(path), expected))
                throw new IOException("Private directory identity changed.");
        }

        internal static void DeleteOwnedEmptyDirectory(string path, FileIdentity expected)
        {
            using (var handle = OpenDirectoryForReview(path, true))
            {
                if (!SameIdentity(CaptureDirectoryIdentity(handle), expected))
                    throw new IOException("Private directory identity changed.");
                // FileDispositionInfo on a directory refuses nonempty directories.
                DeleteVerifiedFileHandle(handle);
            }
            RequireVacant(path);
        }

        private static SafeFileHandle OpenDirectoryForReview(string path, bool deleteAccess)
        {
            RequireSafePath(path);
            // FILE_READ_ATTRIBUTES | optional DELETE; deny delete sharing.
            // BACKUP_SEMANTICS | OPEN_REPARSE_POINT; do not follow the leaf.
            var handle = OpenNativeFile(Path.GetFullPath(path),
                0x80u | (deleteAccess ? 0x00010000u : 0u), 3u,
                IntPtr.Zero, 3u, 0x02200000u, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new IOException("Could not open private directory for identity review.",
                    new System.ComponentModel.Win32Exception(error));
            }
            return handle;
        }

        private static FileIdentity CaptureDirectoryIdentity(SafeFileHandle handle)
        {
            HandleInformation information;
            FileIdentity identity;
            if (!GetFileInformationByHandle(handle, out information) ||
                (information.Attributes & (uint)FileAttributes.Directory) == 0 ||
                (information.Attributes & (uint)FileAttributes.ReparsePoint) != 0 ||
                !GetFileInformationByHandleEx(handle, 18, out identity,
                    (uint)Marshal.SizeOf(typeof(FileIdentity))) ||
                (identity.IdLow == 0 && identity.IdHigh == 0))
                throw new IOException("Private directory identity could not be verified.");
            return identity;
        }

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
            return SameIdentity(current, expected);
        }

        internal static bool SameIdentity(FileIdentity current, FileIdentity expected)
        {
            return current.VolumeSerial == expected.VolumeSerial &&
                current.IdLow == expected.IdLow && current.IdHigh == expected.IdHigh;
        }

        internal static FileStream OpenJournalLock(string path)
        {
            RequireSafePath(path);
            // GENERIC_READ | GENERIC_WRITE, exclusive sharing, OPEN_ALWAYS,
            // OPEN_REPARSE_POINT. Never follow a substituted lock-file link.
            var handle = OpenNativeFile(Path.GetFullPath(path), 0xC0000000u, 0u,
                IntPtr.Zero, 4u, 0x00200000u, IntPtr.Zero);
            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                // Preserve sharing-violation HRESULT for bounded contention.
                throw new IOException("Recovery journal lock could not be opened.",
                    unchecked((int)(0x80070000u | ((uint)error & 0xffffu))));
            }
            try
            {
                CapturePrivateFileIdentity(handle);
                return new FileStream(handle, FileAccess.ReadWrite);
            }
            catch { handle.Dispose(); throw; }
        }

        // The three private metadata stores share this persistent lock naming
        // convention. Never unlink the lock on release: a competing writer
        // may already have opened it. Contention fails fast without staging.
        // Nonparticipating processes and stale read-modify-write callers are
        // outside this serialization contract.
        internal static IDisposable OpenPrivateRecordWriteLock(string recordPath)
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(recordPath));
            var ancestors = PinPublicationAncestors(parent);
            FileStream stream = null;
            try
            {
                stream = OpenJournalLock(recordPath + ".write-lock");
                ancestors.Verify();
                return new PrivateRecordWriteLock(stream, ancestors);
            }
            catch
            {
                try { stream?.Dispose(); }
                finally { ancestors.Dispose(); }
                throw;
            }
        }

        private sealed class PrivateRecordWriteLock : IDisposable
        {
            private readonly FileStream stream;
            private readonly PublicationAncestors ancestors;

            public PrivateRecordWriteLock(FileStream stream, PublicationAncestors ancestors)
            {
                this.stream = stream;
                this.ancestors = ancestors;
            }

            public void Dispose()
            {
                try { stream.Dispose(); }
                finally { ancestors.Dispose(); }
            }
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

        // Original archives are read-only input, not owned staging. An
        // ordinary hardlinked source is allowed, but reparse leaves and
        // directory handles are never accepted as archive data.
        internal static FileStream OpenArchiveSource(string path)
        {
            var stream = OpenPrivateFileForReview(path, false);
            try
            {
                HandleInformation information;
                if (!GetFileInformationByHandle(stream.SafeFileHandle, out information) ||
                    (information.Attributes & (uint)(FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
                    information.NumberOfLinks == 0)
                    throw new IOException("Archive source is not an ordinary file.");
                return stream;
            }
            catch { stream.Dispose(); throw; }
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

        [StructLayout(LayoutKind.Sequential)]
        private struct RenameHeader
        {
            public uint ReplaceFlags;
            public IntPtr RootDirectory;
            public uint NameBytes;
            public ushort FirstCharacter;
        }

        [DllImport("kernel32.dll", EntryPoint = "SetFileInformationByHandle", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetRenameInformation(SafeFileHandle handle,
            int informationClass, IntPtr information, uint size);

        // Hold ordinary directory handles below the volume/share root. Use
        // FILE_LIST_DIRECTORY (data read) as well as FILE_READ_ATTRIBUTES:
        // a metadata-only handle is not a rename/delete guard. Deny delete
        // sharing while allowing ordinary sibling operations. Root mappings,
        // nonparticipating filesystem providers and reparse mutations still
        // require independent qualification; this is not a namespace sandbox.
        internal sealed class PublicationAncestors : IDisposable
        {
            private sealed class PinnedDirectory
            {
                public string Path;
                public SafeFileHandle Handle;
                public FileIdentity Identity;
            }

            private readonly List<PinnedDirectory> directories = new List<PinnedDirectory>();

            public PublicationAncestors(string parent)
            {
                var chain = new Stack<string>();
                var root = Path.GetPathRoot(parent);
                if (string.IsNullOrWhiteSpace(root))
                    throw new IOException("Publication parent is not rooted.");
                var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                for (var current = parent; !string.IsNullOrEmpty(current) &&
                    !string.Equals(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                        normalizedRoot, StringComparison.OrdinalIgnoreCase);
                    current = Path.GetDirectoryName(current))
                {
                    if (chain.Count >= 256)
                        throw new IOException("Publication ancestor depth exceeds the safety limit.");
                    chain.Push(current);
                }
                try
                {
                    while (chain.Count > 0)
                    {
                        var path = chain.Pop();
                        var handle = OpenNativeFile(path, 0x81u, 3u, IntPtr.Zero, 3u, 0x02200000u, IntPtr.Zero);
                        if (handle.IsInvalid)
                        {
                            var error = Marshal.GetLastWin32Error();
                            handle.Dispose();
                            throw new IOException("Publication ancestor could not be guarded.",
                                new System.ComponentModel.Win32Exception(error));
                        }
                        try
                        {
                            directories.Add(new PinnedDirectory
                            {
                                Path = path, Handle = handle, Identity = CaptureDirectoryIdentity(handle)
                            });
                        }
                        catch { handle.Dispose(); throw; }
                    }
                    Verify();
                }
                catch { Dispose(); throw; }
            }

            public void Verify()
            {
                foreach (var directory in directories)
                {
                    if (!SameIdentity(CaptureDirectoryIdentity(directory.Handle), directory.Identity) ||
                        !SameIdentity(ObservePublicationObject(directory.Path, true), directory.Identity))
                        throw new IOException("Publication ancestor identity changed.");
                }
            }

            public void Dispose()
            {
                for (var index = directories.Count - 1; index >= 0; index--)
                    directories[index].Handle.Dispose();
                directories.Clear();
            }
        }

        internal static PublicationAncestors PinPublicationAncestors(string parent)
            => new PublicationAncestors(parent);

        private static FileIdentity ObservePublicationObject(string path, bool directory)
        {
            // Metadata-only observation must share all accesses of the
            // primary inspected source handle, including DELETE access.
            using (var observed = OpenNativeFile(path, 0x80u, 7u, IntPtr.Zero, 3u, 0x02200000u, IntPtr.Zero))
            {
                if (observed.IsInvalid)
                    throw new IOException("Publication object could not be re-observed.",
                        new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
                return directory ? CaptureDirectoryIdentity(observed) : CapturePrivateFileIdentity(observed);
            }
        }

        // Retain the qualified absolute sibling native no-replace rename.
        // Unlike the withdrawn relative-name experiment, the ancestor guards
        // do not change the native rename buffer, flags or RootDirectory.
        internal static void PublishVerifiedHandle(SafeFileHandle handle, string staging, string destination)
        {
            var sourceFull = Path.GetFullPath(staging);
            var targetFull = Path.GetFullPath(destination);
            var parent = Path.GetDirectoryName(targetFull);
            if (string.IsNullOrEmpty(parent) ||
                !string.Equals(Path.GetDirectoryName(sourceFull), parent, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceFull, targetFull, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Publication must rename staging to a distinct sibling.");
            RequireSafePath(sourceFull);
            RequireSafePath(targetFull);
            HandleInformation sourceInformation;
            if (!GetFileInformationByHandle(handle, out sourceInformation))
                throw new IOException("Publication source handle could not be inspected.");
            var directory = (sourceInformation.Attributes & (uint)FileAttributes.Directory) != 0;
            var expected = directory ? CaptureDirectoryIdentity(handle) : CapturePrivateFileIdentity(handle);
            using (var ancestors = PinPublicationAncestors(parent))
            {
                RequireSafePath(sourceFull);
                RequireSafePath(targetFull);
                if (!SameIdentity(ObservePublicationObject(sourceFull, directory), expected))
                    throw new IOException("Publication source pathname does not identify the inspected handle.");
                ancestors.Verify();
                var name = System.Text.Encoding.Unicode.GetBytes(targetFull);
                var nameOffset = Marshal.OffsetOf(typeof(RenameHeader), nameof(RenameHeader.FirstCharacter)).ToInt32();
                var lengthOffset = Marshal.OffsetOf(typeof(RenameHeader), nameof(RenameHeader.NameBytes)).ToInt32();
                var size = Math.Max(Marshal.SizeOf(typeof(RenameHeader)), checked(nameOffset + name.Length + 2));
                var info = Marshal.AllocHGlobal(size);
                try
                {
                    // Zero flags (ReplaceIfExists=false), zero root (absolute
                    // target), and a NUL-terminated length-delimited UTF-16 name.
                    Marshal.Copy(new byte[size], 0, info, size);
                    Marshal.WriteInt32(info, lengthOffset, name.Length);
                    Marshal.Copy(name, 0, IntPtr.Add(info, nameOffset), name.Length);
                    if (!SetRenameInformation(handle, 3, info, (uint)size)) // FileRenameInfo
                        throw new IOException("Verified staging could not be published without replacement.",
                            new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
                    ancestors.Verify();
                    if (!SameIdentity(ObservePublicationObject(targetFull, directory), expected))
                        throw new IOException("Published pathname does not identify the inspected handle.");
                }
                finally { Marshal.FreeHGlobal(info); }
            }
        }

        internal static void PublishOwnedDirectory(string staging, string destination, FileIdentity expected)
        {
            using (var handle = OpenDirectoryForReview(staging, true))
            {
                if (!SameIdentity(CaptureDirectoryIdentity(handle), expected))
                    throw new IOException("Private directory identity changed before publication.");
                PublishVerifiedHandle(handle, staging, destination);
            }
            RequireVacant(staging);
        }

        // Bounded, ordinary, single-link private metadata records. The caller
        // may keep the same inspected stream open for an authorized revocation.
        internal static string ReadPrivateRecord(FileStream stream, int maxBytes)
        {
            CapturePrivateFileIdentity(stream.SafeFileHandle);
            var length = stream.Length;
            if (length <= 0 || length > maxBytes)
                throw new IOException("Private record exceeds its readable size limit.");
            var bytes = new byte[(int)length];
            var offset = 0;
            while (offset < bytes.Length)
            {
                var count = stream.Read(bytes, offset, bytes.Length - offset);
                if (count == 0) throw new IOException("Private record was truncated.");
                offset += count;
            }
            if (stream.ReadByte() != -1)
                throw new IOException("Private record changed during inspection.");
            var text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
            return text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
        }

        internal static void WritePrivateRecord(string path, string text, int maxBytes,
            System.Threading.CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var payload = new System.Text.UTF8Encoding(false, true).GetBytes(text);
            if (payload.Length == 0 || payload.Length > maxBytes)
                throw new IOException("Private record exceeds its readable size limit.");
            RequireVacant(path);
            var parent = Path.GetDirectoryName(Path.GetFullPath(path));
            Directory.CreateDirectory(parent);
            RequireSafePath(parent);
            var temporary = Path.Combine(parent, Guid.NewGuid().ToString("N") + ".tmp");
            RequireVacant(temporary);
            var identity = WriteRecordStage(temporary, payload);
            token.ThrowIfCancellationRequested();
            using (var input = OpenPrivateFileForReview(temporary, true))
            {
                VerifyRecordStage(input, identity, payload, token);
                token.ThrowIfCancellationRequested();
                PublishVerifiedHandle(input.SafeFileHandle, temporary, path);
            }
            // On uncertainty leave the temporary evidence, never delete by path.
            RequireVacant(temporary);
        }

        // Existing-record replacement retains the store's atomic File.Replace
        // and unique backup contract. The original is locked against ordinary
        // writes/deletion during validation and staging; both file identities
        // and staged bytes are rechecked immediately before replacement.
        // This narrows but does NOT eliminate the final pathname race after
        // the handles must close for File.Replace. Preserve ambiguous evidence.
        internal static void ReplacePrivateRecord(string path, byte[] payload,
            string temporary, string backup, Func<bool> validateOriginal, string invalidMessage,
            int maxRecordBytes)
        {
            if (payload == null || payload.Length == 0 || payload.Length > maxRecordBytes)
                throw new IOException("Private record replacement payload exceeds its size limit or is empty.");
            if (validateOriginal == null) throw new ArgumentNullException(nameof(validateOriginal));
            var parent = Path.GetDirectoryName(Path.GetFullPath(path));
            var temporaryParent = Path.GetDirectoryName(Path.GetFullPath(temporary));
            var backupParent = Path.GetDirectoryName(Path.GetFullPath(backup));
            if (string.IsNullOrWhiteSpace(parent) ||
                !string.Equals(parent, temporaryParent, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(parent, backupParent, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Private record replacement requires sibling staging and backup paths.");
            using (var ancestors = PinPublicationAncestors(parent))
            {
                RequireVacant(temporary);
                RequireVacant(backup);
                FileIdentity original;
                FileIdentity staged;
                byte[] originalDigest;
                using (var input = OpenPrivateFileForReview(path, false))
                {
                    if (!validateOriginal()) throw new InvalidOperationException(invalidMessage);
                    original = CapturePrivateFileIdentity(input.SafeFileHandle);
                    originalDigest = ComputeRecordDigest(input, maxRecordBytes);
                    staged = WriteRecordStage(temporary, payload);
                    using (var check = OpenPrivateFileForReview(temporary, false))
                        VerifyRecordStage(check, staged, payload, System.Threading.CancellationToken.None);
                }

                using (var stage = OpenPrivateFileForReview(temporary, false))
                using (var check = OpenPrivateFileForReview(path, false))
                {
                    VerifyRecordStage(stage, staged, payload, System.Threading.CancellationToken.None);
                    if (!MatchesPrivateFileIdentity(check.SafeFileHandle, original))
                        throw new IOException("Private record target identity changed before replacement.");
                    if (!validateOriginal()) throw new InvalidOperationException(invalidMessage);
                    RequireRecordDigest(check, originalDigest, maxRecordBytes);
                    ancestors.Verify();
                    RequireVacant(backup);
                }
                File.Replace(temporary, path, backup);
                // Detect uncertainty after replacement; do not attempt
                // rollback or remove evidence by pathname on failure.
                ancestors.Verify();
                RequireVacant(temporary);
                using (var check = OpenPrivateFileForReview(path, false))
                {
                    var identity = CapturePrivateFileIdentity(check.SafeFileHandle);
                    VerifyRecordStage(check, identity, payload, System.Threading.CancellationToken.None);
                }
                using (var check = OpenPrivateFileForReview(backup, false))
                {
                    CapturePrivateFileIdentity(check.SafeFileHandle);
                    RequireRecordDigest(check, originalDigest, maxRecordBytes);
                }
            }
        }

        private static byte[] ComputeRecordDigest(FileStream stream, int maxRecordBytes)
        {
            if (stream.Length <= 0 || stream.Length > maxRecordBytes)
                throw new IOException("Private record exceeds its digest size limit.");
            return ComputeExactFileDigest(stream, stream.Length);
        }

        // Hash only the observed file length plus an EOF check, never
        // arbitrary growth beyond the inspected byte budget.
        internal static byte[] ComputeExactFileDigest(FileStream stream, long expectedLength)
        {
            if (expectedLength < 0 || stream.Length != expectedLength)
                throw new IOException("Inspected file length changed.");
            stream.Position = 0;
            var buffer = new byte[65536];
            var remaining = expectedLength;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                while (remaining > 0)
                {
                    var count = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                    if (count <= 0) throw new IOException("Inspected file was truncated.");
                    sha.TransformBlock(buffer, 0, count, buffer, 0);
                    remaining -= count;
                }
                if (stream.ReadByte() != -1 || stream.Length != expectedLength)
                    throw new IOException("Inspected file grew.");
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return sha.Hash;
            }
        }

        private static void RequireRecordDigest(FileStream stream, byte[] expected, int maxRecordBytes)
        {
            var actual = ComputeRecordDigest(stream, maxRecordBytes);
            if (actual.Length != expected.Length)
                throw new IOException("Private record digest changed.");
            for (var i = 0; i < actual.Length; i++)
                if (actual[i] != expected[i])
                    throw new IOException("Private record content changed.");
        }

        internal static FileIdentity WriteRecordStage(string temporary, byte[] payload)
        {
            RequireVacant(temporary);
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                var identity = CapturePrivateFileIdentity(output.SafeFileHandle);
                output.Write(payload, 0, payload.Length);
                output.Flush(true);
                return identity;
            }
        }

        internal static void VerifyRecordStage(FileStream input, FileIdentity identity,
            byte[] payload, System.Threading.CancellationToken token)
        {
            if (!MatchesPrivateFileIdentity(input.SafeFileHandle, identity) || input.Length != payload.Length)
                throw new IOException("Private record staging identity or length changed.");
            var buffer = new byte[Math.Min(65536, payload.Length)];
            var offset = 0;
            while (offset < payload.Length)
            {
                token.ThrowIfCancellationRequested();
                var count = input.Read(buffer, 0, Math.Min(buffer.Length, payload.Length - offset));
                if (count == 0) throw new IOException("Private record staging was truncated.");
                for (var i = 0; i < count; i++)
                    if (buffer[i] != payload[offset + i])
                        throw new IOException("Private record staging contents changed.");
                offset += count;
            }
            if (input.ReadByte() != -1)
                throw new IOException("Private record staging grew.");
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
