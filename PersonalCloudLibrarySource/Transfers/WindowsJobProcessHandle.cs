using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace PersonalCloudLibrarySource
{
    // The shipping transfer runner uses this factory; the original public
    // process factory and injected process interface remain compatibility APIs.
    internal sealed class WindowsJobProcessFactory : IRcloneProcessFactory
    {
        public IRcloneProcessHandle Create() => new WindowsJobProcessHandle();
    }

    // A lifetime boundary, not a sandbox. The child is placed in an unnamed
    // kill-on-close job at creation, before its initial thread can execute.
    // Never fall back to Process.Start when job/handle attributes fail.
    internal sealed class WindowsJobProcessHandle : IRcloneProcessHandle
    {
        private SafeFileHandle job;
        private SafeFileHandle root;
        private StreamReader output;
        private StreamReader error;
        private bool memberConfirmed;
        private bool started;
        private bool disposed;

        public ProcessStartInfo StartInfo { get; set; }

        // The native implementation is used only with the bounded-reader path.
        // The public legacy/injected factory still implements these event APIs.
        public event DataReceivedEventHandler OutputDataReceived
        {
            add { throw new NotSupportedException("Use bounded output reads."); }
            remove { throw new NotSupportedException("Use bounded output reads."); }
        }
        public event DataReceivedEventHandler ErrorDataReceived
        {
            add { throw new NotSupportedException("Use bounded error reads."); }
            remove { throw new NotSupportedException("Use bounded error reads."); }
        }
        public void BeginOutputReadLine() { throw new NotSupportedException("Use bounded output reads."); }
        public void BeginErrorReadLine() { throw new NotSupportedException("Use bounded error reads."); }

        internal void BeginBoundedOutputRead(Action<string> receive, Action failure)
            => RcloneProcessHandle.StartBoundedRead(output, receive, failure);
        internal void BeginBoundedErrorRead(Action<string> receive, Action failure)
            => RcloneProcessHandle.StartBoundedRead(error, receive, failure);

        internal TextReader StandardOutput => output;
        internal TextReader StandardError => error;

        public bool Start() => Start(CancellationToken.None);

        internal bool Start(CancellationToken token)
        {
            if (disposed || started) throw new InvalidOperationException("Process handle cannot be reused.");
            started = true;
            token.ThrowIfCancellationRequested();
            if (StartInfo == null || StartInfo.UseShellExecute)
                throw new InvalidOperationException("Contained execution requires direct executable launch.");
            var executable = ResolveExecutable(StartInfo.FileName);
            var arguments = new StringBuilder(RcloneCommandBuilder.QuoteArgument(executable) +
                (string.IsNullOrWhiteSpace(StartInfo.Arguments) ? "" : " " + StartInfo.Arguments));
            job = CreateJobObject(IntPtr.Zero, null);
            if (job.IsInvalid) throw NativeError("CreateJobObject");
            var limits = new ExtendedLimits();
            limits.Basic.LimitFlags = 0x00002000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE; no breakaway.
            if (!SetInformationJobObject(job, 9, ref limits, (uint)Marshal.SizeOf(typeof(ExtendedLimits))))
                throw NativeError("SetInformationJobObject");

            using (var stdout = new ChildPipe(false))
            using (var stderr = new ChildPipe(false))
            using (var stdin = new ChildPipe(true))
            using (var attributes = new StartupAttributes())
            {
                attributes.Add(0x00020002u, new[] { stdin.Child.DangerousGetHandle(),
                    stdout.Child.DangerousGetHandle(), stderr.Child.DangerousGetHandle() });
                // PROC_THREAD_ATTRIBUTE_JOB_LIST: assign as part of creation,
                // including if the parent is already in a compatible host job.
                attributes.Add(0x0002000Du, new[] { job.DangerousGetHandle() });
                var startup = new StartupInfoEx();
                startup.Info.Size = (uint)Marshal.SizeOf(typeof(StartupInfoEx));
                startup.Info.Flags = 0x00000100; // STARTF_USESTDHANDLES.
                startup.Info.StandardInput = stdin.Child.DangerousGetHandle();
                startup.Info.StandardOutput = stdout.Child.DangerousGetHandle();
                startup.Info.StandardError = stderr.Child.DangerousGetHandle();
                startup.Attributes = attributes.Pointer;

                token.ThrowIfCancellationRequested();
                ProcessInformation info;
                // EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW | CREATE_SUSPENDED.
                if (!CreateProcess(executable, arguments, IntPtr.Zero, IntPtr.Zero, true,
                    0x08080004u, IntPtr.Zero,
                    string.IsNullOrWhiteSpace(StartInfo.WorkingDirectory) ? null : StartInfo.WorkingDirectory,
                    ref startup, out info))
                    throw NativeError("CreateProcess (contained)");
                root = new SafeFileHandle(info.Process, true);
                using (var thread = new SafeFileHandle(info.Thread, true))
                {
                    bool inJob;
                    if (!IsProcessInJob(root, job, out inJob) || !inJob)
                        throw new IOException("Native launch did not establish job membership.");
                    memberConfirmed = true;
                    output = OpenReader(stdout.TakeParent(), StartInfo.StandardOutputEncoding ?? Encoding.UTF8);
                    error = OpenReader(stderr.TakeParent(), StartInfo.StandardErrorEncoding ?? Encoding.UTF8);
                    stdin.Parent.Dispose(); // EOF, not the host's standard input.
                    token.ThrowIfCancellationRequested();
                    if (ResumeThread(thread) != 1u)
                        throw NativeError("ResumeThread");
                }
            }
            return true;
        }

        private static StreamReader OpenReader(SafeFileHandle handle, Encoding encoding)
        {
            try { return new StreamReader(new FileStream(handle, FileAccess.Read, 4096, false), encoding, true); }
            catch { handle.Dispose(); throw; }
        }

        // Explicit paths or absolute PATH entries only. Do not implicitly
        // search the current working directory for an unqualified executable.
        private static string ResolveExecutable(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new IOException("Executable path is empty.");
            if (IsAbsolute(name))
            {
                var full = Path.GetFullPath(name);
                if (!File.Exists(full)) throw new FileNotFoundException("Configured executable was not found.");
                return full;
            }
            if (name.IndexOfAny(new[] { '\\', '/', ':', '"' }) >= 0)
                throw new IOException("Executable must be an absolute path or PATH name.");
            var candidateName = Path.HasExtension(name) ? name : name + ".exe";
            foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                var directory = entry.Trim().Trim('"');
                if (!IsAbsolute(directory)) continue;
                var candidate = Path.GetFullPath(Path.Combine(directory, candidateName));
                if (File.Exists(candidate)) return candidate;
            }
            throw new FileNotFoundException("Configured executable was not found on PATH.");
        }

        private static bool IsAbsolute(string value) => !string.IsNullOrWhiteSpace(value) &&
            (value.StartsWith(@"\\", StringComparison.Ordinal) ||
                (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' &&
                    (value[2] == '\\' || value[2] == '/')));

        internal bool RootExited
        {
            get
            {
                if (root == null || root.IsInvalid) return true;
                var result = WaitForSingleObject(root, 0);
                if (result == 0) return true;
                if (result == 258) return false;
                throw NativeError("WaitForSingleObject");
            }
        }

        public bool HasExited
        {
            get
            {
                if (!RootExited) return false;
                if (job == null || job.IsInvalid) return true;
                Accounting accounting;
                uint returned;
                if (!QueryInformationJobObject(job, 1, out accounting,
                    (uint)Marshal.SizeOf(typeof(Accounting)), out returned))
                    throw NativeError("QueryInformationJobObject");
                return accounting.ActiveProcesses == 0;
            }
        }

        public int ExitCode
        {
            get
            {
                if (root == null || !RootExited) throw new InvalidOperationException("Root process has not exited.");
                uint code;
                if (!GetExitCodeProcess(root, out code)) throw NativeError("GetExitCodeProcess");
                return unchecked((int)code);
            }
        }

        public bool WaitForExit(int milliseconds)
        {
            if (milliseconds < -1) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            var clock = Stopwatch.StartNew();
            while (!HasExited)
            {
                if (milliseconds >= 0 && clock.ElapsedMilliseconds >= milliseconds) return false;
                Thread.Sleep(milliseconds < 0 ? 20 :
                    (int)Math.Max(1L, Math.Min(20L, milliseconds - clock.ElapsedMilliseconds)));
            }
            return true;
        }
        public void WaitForExit() { WaitForExit(-1); }

        public void Kill()
        {
            // A failed membership check leaves an unresumed root. Stop that
            // root explicitly as well as the job, without using its PID.
            if (!memberConfirmed && root != null && !RootExited && !TerminateProcess(root, 1))
                throw NativeError("TerminateProcess (unresumed root)");
            if (job != null && !job.IsInvalid && !TerminateJobObject(job, 1))
                throw NativeError("TerminateJobObject");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // Close the job before pipe readers so descendants cannot keep
            // writing while host handles are being released.
            try
            {
                if (!memberConfirmed && root != null && !RootExited)
                    TerminateProcess(root, 1);
            }
            finally
            {
                try { job?.Dispose(); }
                finally
                {
                    try { root?.Dispose(); }
                    finally
                    {
                        try { output?.Dispose(); }
                        finally { error?.Dispose(); }
                    }
                }
            }
        }

        private static Win32Exception NativeError(string operation)
            => new Win32Exception(Marshal.GetLastWin32Error(), operation + " failed.");

        private sealed class ChildPipe : IDisposable
        {
            public SafeFileHandle Child { get; private set; }
            public SafeFileHandle Parent { get; private set; }

            public ChildPipe(bool input)
            {
                var security = new SecurityAttributes
                {
                    Length = (uint)Marshal.SizeOf(typeof(SecurityAttributes)),
                    Inherit = 1
                };
                SafeFileHandle read, write;
                if (!CreatePipe(out read, out write, ref security, 0))
                {
                    var failure = NativeError("CreatePipe");
                    read?.Dispose(); write?.Dispose();
                    throw failure;
                }
                Child = input ? read : write;
                Parent = input ? write : read;
                if (!SetHandleInformation(Parent, 1u, 0u)) // HANDLE_FLAG_INHERIT
                {
                    var failure = NativeError("SetHandleInformation");
                    Dispose();
                    throw failure;
                }
            }

            public SafeFileHandle TakeParent()
            {
                var result = Parent;
                Parent = null;
                return result;
            }

            public void Dispose()
            {
                try { Child?.Dispose(); }
                finally { Parent?.Dispose(); }
            }
        }

        private sealed class StartupAttributes : IDisposable
        {
            private readonly System.Collections.Generic.List<IntPtr> values =
                new System.Collections.Generic.List<IntPtr>();
            private bool initialized;
            public IntPtr Pointer { get; private set; }

            public StartupAttributes()
            {
                UIntPtr size = UIntPtr.Zero;
                InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref size);
                if (size == UIntPtr.Zero) throw NativeError("InitializeProcThreadAttributeList (size)");
                Pointer = Marshal.AllocHGlobal(new IntPtr(checked((long)size.ToUInt64())));
                if (!InitializeProcThreadAttributeList(Pointer, 2, 0, ref size))
                {
                    var failure = NativeError("InitializeProcThreadAttributeList");
                    Dispose();
                    throw failure;
                }
                initialized = true;
            }

            public void Add(uint attribute, IntPtr[] handles)
            {
                var bytes = checked(IntPtr.Size * handles.Length);
                var value = Marshal.AllocHGlobal(bytes);
                values.Add(value);
                Marshal.Copy(handles, 0, value, handles.Length);
                if (!UpdateProcThreadAttribute(Pointer, 0, new UIntPtr(attribute), value,
                    new UIntPtr((uint)bytes), IntPtr.Zero, IntPtr.Zero))
                    throw NativeError("UpdateProcThreadAttribute");
            }

            public void Dispose()
            {
                if (initialized) DeleteProcThreadAttributeList(Pointer);
                initialized = false;
                foreach (var value in values) Marshal.FreeHGlobal(value);
                values.Clear();
                if (Pointer != IntPtr.Zero) Marshal.FreeHGlobal(Pointer);
                Pointer = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            public uint Length;
            public IntPtr Descriptor;
            public int Inherit;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StartupInfo
        {
            public uint Size;
            public IntPtr Reserved, Desktop, Title;
            public uint X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
            public ushort Show, Reserved2Length;
            public IntPtr Reserved2, StandardInput, StandardOutput, StandardError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct StartupInfoEx
        {
            public StartupInfo Info;
            public IntPtr Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr Process, Thread;
            public uint ProcessId, ThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimits
        {
            public long PerProcessUserTime, PerJobUserTime;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSet, MaximumWorkingSet;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOps, WriteOps, OtherOps, ReadBytes, WriteBytes, OtherBytes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimits
        {
            public BasicLimits Basic;
            public IoCounters Io;
            public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Accounting
        {
            public long TotalUser, TotalKernel, PeriodUser, PeriodKernel;
            public uint PageFaults, TotalProcesses, ActiveProcesses, TerminatedProcesses;
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        private static extern SafeFileHandle CreateJobObject(IntPtr security, string name);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(SafeFileHandle job, int kind, ref ExtendedLimits limits, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryInformationJobObject(SafeFileHandle job, int kind, out Accounting accounting, uint size, out uint returned);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsProcessInJob(SafeFileHandle process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool result);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(SafeFileHandle job, uint code);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(SafeFileHandle process, uint code);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(SafeFileHandle thread);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(SafeFileHandle handle, uint timeout);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetExitCodeProcess(SafeFileHandle process, out uint code);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreatePipe(out SafeFileHandle read, out SafeFileHandle write, ref SecurityAttributes security, uint size);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetHandleInformation(SafeFileHandle handle, uint mask, uint flags);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref UIntPtr size);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, UIntPtr attribute, IntPtr value, UIntPtr size, IntPtr previous, IntPtr returned);
        [DllImport("kernel32.dll")]
        private static extern void DeleteProcThreadAttributeList(IntPtr list);
        [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcess(string application, StringBuilder arguments, IntPtr processSecurity,
            IntPtr threadSecurity, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint flags, IntPtr environment,
            string directory, ref StartupInfoEx startup, out ProcessInformation information);
    }
}
