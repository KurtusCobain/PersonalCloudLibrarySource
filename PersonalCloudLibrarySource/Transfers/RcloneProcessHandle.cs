using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PersonalCloudLibrarySource
{
    public interface IRcloneProcessFactory
    {
        IRcloneProcessHandle Create();
    }

    public interface IRcloneProcessHandle : IDisposable
    {
        ProcessStartInfo StartInfo { get; set; }
        event DataReceivedEventHandler OutputDataReceived;
        event DataReceivedEventHandler ErrorDataReceived;
        bool HasExited { get; }
        int ExitCode { get; }
        bool Start();
        void BeginOutputReadLine();
        void BeginErrorReadLine();
        bool WaitForExit(int milliseconds);
        void WaitForExit();
        void Kill();
    }

    public sealed class RcloneProcessFactory : IRcloneProcessFactory
    {
        public IRcloneProcessHandle Create()
        {
            return new RcloneProcessHandle(new Process());
        }
    }

    internal sealed class RcloneProcessHandle : IRcloneProcessHandle
    {
        private readonly Process process;
        private TextReader boundedOutput;
        private TextReader boundedError;
        private const int DiagnosticLineLimit = 16 * 1024;

        public RcloneProcessHandle(Process process)
        {
            this.process = process ?? throw new ArgumentNullException(nameof(process));
        }

        public ProcessStartInfo StartInfo
        {
            get => process.StartInfo;
            set => process.StartInfo = value;
        }

        public event DataReceivedEventHandler OutputDataReceived
        {
            add => process.OutputDataReceived += value;
            remove => process.OutputDataReceived -= value;
        }

        public event DataReceivedEventHandler ErrorDataReceived
        {
            add => process.ErrorDataReceived += value;
            remove => process.ErrorDataReceived -= value;
        }

        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        public bool Start() => process.Start();
        public void BeginOutputReadLine() => process.BeginOutputReadLine();
        public void BeginErrorReadLine() => process.BeginErrorReadLine();
        public bool WaitForExit(int milliseconds) => process.WaitForExit(milliseconds);
        public void WaitForExit() => process.WaitForExit();
        public void Kill() => process.Kill();
        internal void BeginBoundedOutputRead(Action<string> receive, Action failure)
        {
            boundedOutput = process.StandardOutput;
            StartBoundedRead(boundedOutput, receive, failure);
        }

        internal void BeginBoundedErrorRead(Action<string> receive, Action failure)
        {
            boundedError = process.StandardError;
            StartBoundedRead(boundedError, receive, failure);
        }

        internal static void StartBoundedRead(TextReader reader, Action<string> receive, Action failure)
        {
            // Start both pipes independently; a constantly readable pipe must
            // not prevent the other pipe from starting. All read faults are
            // reported to the owning process runner.
            Task.Run(async () =>
            {
                try
                {
                    await ReadBoundedLinesAsync(reader, receive, DiagnosticLineLimit).ConfigureAwait(false);
                    receive(null);
                }
                catch { failure(); }
            });
        }

        // Framework BeginOutputReadLine accumulates an entire line before the
        // callback. Bound the actual production reader, not just the log tail.
        // Oversized lines are omitted as a whole, never parsed as progress.
        internal static async Task ReadBoundedLinesAsync(TextReader reader, Action<string> receive, int limit)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            if (limit < 1) throw new ArgumentOutOfRangeException(nameof(limit));
            const string omitted = "[rclone diagnostic line omitted]";
            var buffer = new char[4096];
            var line = new StringBuilder(Math.Min(limit, 4096));
            var overflow = false;
            var skipLf = false;
            Action emit = () =>
            {
                receive(overflow ? omitted.Substring(0, Math.Min(limit, omitted.Length)) : line.ToString());
                line.Clear();
                overflow = false;
            };
            int count;
            while ((count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
            {
                for (var index = 0; index < count; index++)
                {
                    var character = buffer[index];
                    if (skipLf)
                    {
                        skipLf = false;
                        if (character == '\n') continue;
                    }
                    if (character == '\r' || character == '\n')
                    {
                        emit();
                        skipLf = character == '\r';
                    }
                    else if (!overflow)
                    {
                        if (line.Length == limit)
                        {
                            overflow = true;
                            line.Clear();
                        }
                        else line.Append(character);
                    }
                }
            }
            if (overflow || line.Length > 0) emit();
        }

        public void Dispose()
        {
            try { boundedOutput?.Dispose(); }
            finally
            {
                try { boundedError?.Dispose(); }
                finally { process.Dispose(); }
            }
        }
    }
}
