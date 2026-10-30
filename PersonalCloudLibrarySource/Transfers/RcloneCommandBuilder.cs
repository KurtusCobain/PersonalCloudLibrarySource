using System;
using System.Text;

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
                            QuoteArgument(remoteName + ":" + sourcePath) + " " +
                            QuoteArgument(destinationPath) +
                            " --stats=1s --stats-one-line --progress";

            if (request.IsDirectory)
            {
                arguments += " --create-empty-src-dirs";
            }

            return arguments;
        }

        // Correct Windows command-line escaping for ProcessStartInfo.Arguments.
        // In particular, trailing backslashes must be doubled before the closing
        // quote, and backslashes before embedded quotation marks must be escaped.
        public static string QuoteArgument(string value)
        {
            var result = new StringBuilder("\"");
            var backslashes = 0;
            foreach (var ch in value ?? string.Empty)
            {
                if (ch == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (ch == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                }
                else
                {
                    result.Append('\\', backslashes);
                    result.Append(ch);
                }

                backslashes = 0;
            }

            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }
    }
}
