using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PersonalCloudLibrarySource
{
    public class SafeFileWriteService
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public void WriteAllText(string path, string content, string backupDirectory = null, bool createBackup = false)
        {
            WriteBytes(path, Utf8NoBom.GetBytes(content ?? string.Empty), backupDirectory, createBackup);
        }

        public void WriteAllLines(string path, IEnumerable<string> lines, string backupDirectory = null, bool createBackup = false)
        {
            var content = string.Join(Environment.NewLine, lines ?? Array.Empty<string>()) + Environment.NewLine;
            WriteAllText(path, content, backupDirectory, createBackup);
        }

        private static void WriteBytes(string path, byte[] content, string backupDirectory, bool createBackup)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A target path is required.", nameof(path));
            }

            var targetDirectory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(targetDirectory))
                throw new InvalidOperationException("The target path must include a directory.");

            path = Path.GetFullPath(path);
            targetDirectory = Path.GetDirectoryName(path);
            TransferPartialPathPolicy.RequireSafePath(path);
            Directory.CreateDirectory(targetDirectory);
            TransferPartialPathPolicy.RequireSafePath(targetDirectory);

            // A new output and a replacement are distinct operations. Do not
            // switch from one to the other after a concurrent path change.
            TransferPartialPathPolicy.FileIdentity? original = null;
            if (File.Exists(path))
            {
                using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
                    original = TransferPartialPathPolicy.CapturePrivateFileIdentity(input.SafeFileHandle);
            }
            else TransferPartialPathPolicy.RequireVacant(path);

            var tempPath = Path.Combine(targetDirectory, Guid.NewGuid().ToString("N") + ".tmp");
            TransferPartialPathPolicy.RequireVacant(tempPath);
            var bytes = content ?? Array.Empty<byte>();
            TransferPartialPathPolicy.FileIdentity staged;
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                staged = TransferPartialPathPolicy.CapturePrivateFileIdentity(stream.SafeFileHandle);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            // For new files, publish through the exact verified handle.
            // On uncertain failure preserve evidence instead of deleting by name.
            using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(tempPath, true))
            {
                if (!TransferPartialPathPolicy.MatchesPrivateFileIdentity(input.SafeFileHandle, staged) ||
                    input.Length != bytes.Length)
                    throw new IOException("Output staging identity or length changed.");
                var buffer = new byte[65536];
                var offset = 0;
                while (offset < bytes.Length)
                {
                    var count = input.Read(buffer, 0, Math.Min(buffer.Length, bytes.Length - offset));
                    if (count == 0) throw new IOException("Output staging was truncated.");
                    for (var i = 0; i < count; i++)
                        if (buffer[i] != bytes[offset + i])
                            throw new IOException("Output staging changed.");
                    offset += count;
                }
                if (input.ReadByte() != -1) throw new IOException("Output staging grew.");
                if (!original.HasValue)
                {
                    TransferPartialPathPolicy.PublishVerifiedHandle(input.SafeFileHandle, tempPath, path);
                    return;
                }
            }

            // Replacement retains the existing File.Replace compatibility
            // contract. Check the original again and copy its backup while
            // holding a read handle that denies ordinary writes and deletion.
            using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
            {
                if (!TransferPartialPathPolicy.MatchesPrivateFileIdentity(input.SafeFileHandle, original.Value))
                    throw new IOException("Output target changed before replacement.");
                if (createBackup && !string.IsNullOrWhiteSpace(backupDirectory))
                {
                    backupDirectory = Path.GetFullPath(backupDirectory);
                    TransferPartialPathPolicy.RequireSafePath(backupDirectory);
                    Directory.CreateDirectory(backupDirectory);
                    TransferPartialPathPolicy.RequireSafePath(backupDirectory);
                    var backupPath = Path.Combine(backupDirectory, Path.GetFileName(path) + "." +
                        DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "." + Guid.NewGuid().ToString("N") + ".bak");
                    TransferPartialPathPolicy.RequireVacant(backupPath);
                    using (var backup = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        input.CopyTo(backup);
                        backup.Flush(true);
                    }
                }
            }
            // File.Replace requires closed handles. This is a narrowed
            // pathname replacement window, not an atomic identity comparison.
            TransferPartialPathPolicy.RequireSafePath(path);
            TransferPartialPathPolicy.RequireSafePath(tempPath);
            using (var check = TransferPartialPathPolicy.OpenPrivateFileForReview(tempPath, false))
                if (!TransferPartialPathPolicy.MatchesPrivateFileIdentity(check.SafeFileHandle, staged))
                    throw new IOException("Output staging was replaced.");
            using (var check = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
                if (!TransferPartialPathPolicy.MatchesPrivateFileIdentity(check.SafeFileHandle, original.Value))
                    throw new IOException("Output target was replaced.");
            File.Replace(tempPath, path, null);
        }
    }
}
