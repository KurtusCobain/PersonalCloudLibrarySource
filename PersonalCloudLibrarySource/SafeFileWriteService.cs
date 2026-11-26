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

        internal static string ValidateOutputPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A target path is required.", nameof(path));
            ValidateOrdinaryPath(path);
            if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(path)))
                throw new InvalidOperationException("The target path must include a directory.");
            var full = Path.GetFullPath(path);
            TransferPartialPathPolicy.RequireSafePath(full);
            return full;
        }

        internal static void ValidateOrdinaryPath(string path)
        {
            if (SafeCacheDeletionPolicy.IsUnsupportedWindowsPath(path) ||
                (Path.IsPathRooted(path) && !SafeCacheDeletionPolicy.IsFullyRooted(path)))
                throw new IOException("Path uses an unsupported Windows namespace or name.");
        }

        private static void WriteBytes(string path, byte[] content, string backupDirectory, bool createBackup)
        {
            path = ValidateOutputPath(path);
            if (createBackup && !string.IsNullOrWhiteSpace(backupDirectory))
                ValidateOrdinaryPath(backupDirectory);
            var targetDirectory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(targetDirectory);
            TransferPartialPathPolicy.RequireSafePath(targetDirectory);
            var bytes = content ?? Array.Empty<byte>();
            using (var ancestors = TransferPartialPathPolicy.PinPublicationAncestors(targetDirectory))
            {
                TransferPartialPathPolicy.RequireSafePath(path);
                var temporary = Path.Combine(targetDirectory, Guid.NewGuid().ToString("N") + ".tmp");
                if (!File.Exists(path))
                {
                    TransferPartialPathPolicy.RequireVacant(path);
                    var staged = TransferPartialPathPolicy.WriteRecordStage(temporary, bytes);
                    using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(temporary, true))
                    {
                        TransferPartialPathPolicy.VerifyRecordStage(input, staged, bytes, System.Threading.CancellationToken.None);
                        TransferPartialPathPolicy.PublishVerifiedHandle(input.SafeFileHandle, temporary, path);
                    }
                    TransferPartialPathPolicy.RequireVacant(temporary);
                    return;
                }

                // Hold the original through staging, verification and backup.
                // The final File.Replace call still requires closed leaf handles.
                using (var original = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
                {
                    TransferPartialPathPolicy.CapturePrivateFileIdentity(original.SafeFileHandle);
                    var staged = TransferPartialPathPolicy.WriteRecordStage(temporary, bytes);
                    using (var input = TransferPartialPathPolicy.OpenPrivateFileForReview(temporary, false))
                    {
                        TransferPartialPathPolicy.VerifyRecordStage(input, staged, bytes, System.Threading.CancellationToken.None);
                        if (createBackup && !string.IsNullOrWhiteSpace(backupDirectory))
                            PublishBackup(original, path, backupDirectory);
                        ancestors.Verify();
                        input.Position = 0;
                        TransferPartialPathPolicy.VerifyRecordStage(input, staged, bytes, System.Threading.CancellationToken.None);
                    }
                }
                File.Replace(temporary, path, null);
                // Report uncertainty without rolling back by pathname.
                ancestors.Verify();
                TransferPartialPathPolicy.RequireVacant(temporary);
                using (var result = TransferPartialPathPolicy.OpenPrivateFileForReview(path, false))
                {
                    var identity = TransferPartialPathPolicy.CapturePrivateFileIdentity(result.SafeFileHandle);
                    TransferPartialPathPolicy.VerifyRecordStage(result, identity, bytes, System.Threading.CancellationToken.None);
                }
            }
        }

        private static void PublishBackup(FileStream original, string target, string backupDirectory)
        {
            backupDirectory = Path.GetFullPath(backupDirectory);
            TransferPartialPathPolicy.RequireSafePath(backupDirectory);
            Directory.CreateDirectory(backupDirectory);
            TransferPartialPathPolicy.RequireSafePath(backupDirectory);
            using (var ancestors = TransferPartialPathPolicy.PinPublicationAncestors(backupDirectory))
            {
                var backupPath = Path.Combine(backupDirectory, Path.GetFileName(target) + "." +
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "." + Guid.NewGuid().ToString("N") + ".bak");
                var stagePath = Path.Combine(backupDirectory, Guid.NewGuid().ToString("N") + ".tmp");
                TransferPartialPathPolicy.RequireVacant(backupPath);
                TransferPartialPathPolicy.RequireVacant(stagePath);
                var length = original.Length;
                var expected = TransferPartialPathPolicy.ComputeExactFileDigest(original, length);
                original.Position = 0;
                TransferPartialPathPolicy.FileIdentity staged;
                using (var backup = new FileStream(stagePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    staged = TransferPartialPathPolicy.CapturePrivateFileIdentity(backup.SafeFileHandle);
                    var buffer = new byte[65536];
                    var remaining = length;
                    while (remaining > 0)
                    {
                        var count = original.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (count <= 0) throw new IOException("Backup source was truncated.");
                        backup.Write(buffer, 0, count);
                        remaining -= count;
                    }
                    if (original.ReadByte() != -1 || original.Length != length)
                        throw new IOException("Backup source changed.");
                    backup.Flush(true);
                }
                using (var backup = TransferPartialPathPolicy.OpenPrivateFileForReview(stagePath, true))
                {
                    if (!TransferPartialPathPolicy.MatchesPrivateFileIdentity(backup.SafeFileHandle, staged) ||
                        !SameDigest(TransferPartialPathPolicy.ComputeExactFileDigest(backup, length), expected))
                        throw new IOException("Backup stage identity or content changed.");
                    ancestors.Verify();
                    TransferPartialPathPolicy.PublishVerifiedHandle(backup.SafeFileHandle, stagePath, backupPath);
                }
                TransferPartialPathPolicy.RequireVacant(stagePath);
                using (var backup = TransferPartialPathPolicy.OpenPrivateFileForReview(backupPath, false))
                {
                    if (!TransferPartialPathPolicy.MatchesPrivateFileIdentity(backup.SafeFileHandle, staged) ||
                        !SameDigest(TransferPartialPathPolicy.ComputeExactFileDigest(backup, length), expected))
                        throw new IOException("Published backup could not be verified.");
                }
                if (!SameDigest(TransferPartialPathPolicy.ComputeExactFileDigest(original, length), expected))
                    throw new IOException("Original changed during backup creation.");
            }
        }

        private static bool SameDigest(byte[] first, byte[] second)
        {
            if (first.Length != second.Length) return false;
            for (var i = 0; i < first.Length; i++)
                if (first[i] != second[i]) return false;
            return true;
        }
    }
}
