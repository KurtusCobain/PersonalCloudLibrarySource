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
            {
                throw new InvalidOperationException("The target path must include a directory.");
            }

            Directory.CreateDirectory(targetDirectory);
            var tempPath = Path.Combine(
                targetDirectory,
                Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                File.WriteAllBytes(tempPath, content ?? Array.Empty<byte>());

                if (File.Exists(path))
                {
                    if (createBackup && !string.IsNullOrWhiteSpace(backupDirectory))
                    {
                        Directory.CreateDirectory(backupDirectory);
                        var backupFileName =
                            Path.GetFileName(path) + "." + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".bak";
                        var backupPath = Path.Combine(backupDirectory, backupFileName);
                        File.Copy(path, backupPath, overwrite: true);
                    }

                    File.Replace(tempPath, path, null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch
            {
                TryDeleteTempFile(tempPath);
                throw;
            }

            TryDeleteTempFile(tempPath);
        }

        private static void TryDeleteTempFile(string tempPath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }
}
