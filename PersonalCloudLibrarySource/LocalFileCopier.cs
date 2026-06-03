using System;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public class LocalFileCopier
    {
        public LocalCopyResult CopyFileToLocalPath(string sourceFilePath, string localFullFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                return LocalCopyResult.Fail("Source file path is empty.");
            }

            if (string.IsNullOrWhiteSpace(localFullFilePath))
            {
                return LocalCopyResult.Fail("Local destination file path could not be resolved.");
            }

            if (!File.Exists(sourceFilePath))
            {
                return LocalCopyResult.Fail($"Source file does not exist: {sourceFilePath}");
            }

            try
            {
                var destinationFolder = Path.GetDirectoryName(localFullFilePath);
                if (string.IsNullOrWhiteSpace(destinationFolder))
                {
                    return LocalCopyResult.Fail("Local destination folder could not be resolved.");
                }

                Directory.CreateDirectory(destinationFolder);
                File.Copy(sourceFilePath, localFullFilePath, true);
                return LocalCopyResult.Success("Local file copy completed.");
            }
            catch (Exception ex)
            {
                return LocalCopyResult.Fail("Local file copy failed.", ex);
            }
        }

        public LocalCopyResult CopyDirectoryToLocalPath(string sourceDirectoryPath, string localDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(sourceDirectoryPath))
            {
                return LocalCopyResult.Fail("Source directory path is empty.");
            }

            if (string.IsNullOrWhiteSpace(localDirectoryPath))
            {
                return LocalCopyResult.Fail("Local destination directory path could not be resolved.");
            }

            if (!Directory.Exists(sourceDirectoryPath))
            {
                return LocalCopyResult.Fail($"Source directory does not exist: {sourceDirectoryPath}");
            }

            try
            {
                CopyDirectoryContents(sourceDirectoryPath, localDirectoryPath);
                return LocalCopyResult.Success("Local directory copy completed.");
            }
            catch (Exception ex)
            {
                return LocalCopyResult.Fail("Local directory copy failed.", ex);
            }
        }

        private static void CopyDirectoryContents(string sourceDirectoryPath, string localDirectoryPath)
        {
            Directory.CreateDirectory(localDirectoryPath);

            foreach (var directory in Directory.GetDirectories(sourceDirectoryPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = directory.Substring(sourceDirectoryPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(localDirectoryPath, relativePath));
            }

            foreach (var filePath in Directory.GetFiles(sourceDirectoryPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = filePath.Substring(sourceDirectoryPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destinationPath = Path.Combine(localDirectoryPath, relativePath);
                var destinationFolder = Path.GetDirectoryName(destinationPath);

                if (!string.IsNullOrWhiteSpace(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                File.Copy(filePath, destinationPath, true);
            }
        }
    }

    public class LocalCopyResult
    {
        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
        public Exception Exception { get; private set; }

        public static LocalCopyResult Success(string message)
        {
            return new LocalCopyResult
            {
                Succeeded = true,
                Message = message
            };
        }

        public static LocalCopyResult Fail(string message, Exception exception = null)
        {
            return new LocalCopyResult
            {
                Succeeded = false,
                Message = message,
                Exception = exception
            };
        }
    }
}
