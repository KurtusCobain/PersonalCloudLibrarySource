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

            string remoteOperand;
            if (!TryBuildRemoteOperand(request.RemoteName, request.RemoteSourcePath, true, out remoteOperand))
                throw new ArgumentException("The rclone source is invalid.");
            var destinationPath = request.DestinationPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(destinationPath) || destinationPath.StartsWith("-", StringComparison.Ordinal))
                throw new ArgumentException("The rclone destination is invalid.");
            var command = request.IsDirectory ? "copy" : "copyto";
            var arguments = command + " " +
                            QuoteArgument(remoteOperand) + " " +
                            QuoteArgument(destinationPath) +
                            " --stats=1s --stats-one-line --progress --ask-password=false";

            if (request.UseInPlace)
            {
                arguments += " --inplace";
            }

            if (request.IsDirectory)
            {
                arguments += " --create-empty-src-dirs";
            }

            return arguments;
        }

        // Configured named remotes only. Do not reinterpret inline backend or
        // config syntax, including :local:. Disposable tests use named local remotes.
        internal static bool TryBuildRemoteOperand(string name, string path,
            bool trimLeadingSlash, out string operand)
        {
            operand = string.Empty;
            var remote = (name ?? string.Empty).Trim();
            var source = (path ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(source)) return false;
            foreach (var ch in source)
                if (char.IsControl(ch)) return false;
            foreach (var segment in source.Split('/'))
                if (segment == "." || segment == "..") return false;
            if (remote.EndsWith(":", StringComparison.Ordinal))
                remote = remote.Substring(0, remote.Length - 1);
            if (string.IsNullOrWhiteSpace(remote) || remote.StartsWith("-", StringComparison.Ordinal) ||
                remote.IndexOfAny(new[] { ':', ',', '"', '\'', '/', '\\', '=' }) >= 0)
                return false;
            foreach (var ch in remote)
                if (char.IsControl(ch)) return false;
            if (trimLeadingSlash) source = source.TrimStart('/');
            if (string.IsNullOrWhiteSpace(source)) return false;
            operand = remote + ":" + source;
            return true;
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
