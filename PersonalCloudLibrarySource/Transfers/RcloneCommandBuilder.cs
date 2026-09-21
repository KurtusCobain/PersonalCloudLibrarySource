using System;

namespace PersonalCloudLibrarySource
{
    public static class RcloneCommandBuilder
    {
        public static string BuildArguments(RcloneTransferRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var remoteName = (request.RemoteName ?? string.Empty).Trim().TrimEnd(':');
            var sourcePath = (request.RemoteSourcePath ?? string.Empty)
                .Trim()
                .Replace('\\', '/')
                .TrimStart('/');
            var destinationPath = request.DestinationPath ?? string.Empty;
            var command = request.IsDirectory ? "copy" : "copyto";
            var arguments = command + " " +
                            Quote(remoteName + ":" + sourcePath) + " " +
                            Quote(destinationPath) +
                            " --stats=1s --stats-one-line --progress";

            if (request.IsDirectory)
            {
                arguments += " --create-empty-src-dirs";
            }

            return arguments;
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }
    }
}
