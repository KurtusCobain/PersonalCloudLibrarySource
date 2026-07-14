using System;
using System.Diagnostics;

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
        public void Dispose() => process.Dispose();
    }
}
