using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class RcloneProcessRunnerTests
    {
        [Test]
        public void DiagnosticOutput_RetainsOnlyBoundedTailForHugeSingleLine()
        {
            var append = typeof(RcloneProcessRunner).GetMethod(
                "AppendLineBounded",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(append, Is.Not.Null);
            var tail = new System.Text.StringBuilder();
            append.Invoke(null, new object[] { tail, new string('X', 300000), 1024 });
            Assert.That(tail.Length, Is.LessThanOrEqualTo(1024));
            Assert.That(tail.ToString(), Does.Contain("XXX"));
            append.Invoke(null, new object[] { tail, "FINAL ERROR", 1024 });
            Assert.That(tail.Length, Is.LessThanOrEqualTo(1024));
            Assert.That(tail.ToString(), Does.Contain("FINAL ERROR"));
        }

        [Test]
        public void BoundedProductionLineReaderOmitsOversizeLineAndPreservesFollowingData()
        {
            var handleType = typeof(RcloneProcessFactory).Assembly.GetType("PersonalCloudLibrarySource.RcloneProcessHandle");
            var read = handleType.GetMethod("ReadBoundedLinesAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(read, Is.Not.Null);
            var lines = new System.Collections.Generic.List<string>();
            var prefix = new string('p', 4095);
            using (var source = new StringReader(prefix + "\r\n" + new string('x', 300000) +
                "\rnext\n\nlast"))
            {
                var task = (System.Threading.Tasks.Task)read.Invoke(null,
                    new object[] { source, (Action<string>)lines.Add, 4096 });
                task.GetAwaiter().GetResult();
            }
            Assert.That(lines, Is.EqualTo(new[] { prefix, "[rclone diagnostic line omitted]", "next", "", "last" }));
        }

        private static IRcloneProcessHandle CreateContainedHandle()
        {
            var type = typeof(RcloneProcessRunner).Assembly.GetType("PersonalCloudLibrarySource.WindowsJobProcessHandle");
            Assert.That(type, Is.Not.Null);
            return (IRcloneProcessHandle)Activator.CreateInstance(type, true);
        }

        [Test]
        public void ProductionLauncherWaitsForEntireJobAndTerminatesSurvivingDescendant()
        {
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            using (var process = CreateContainedHandle())
            {
                var type = process.GetType();
                var rootExited = type.GetProperty("RootExited",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(rootExited, Is.Not.Null);
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "/d /c start \"\" /b ping.exe -n 30 127.0.0.1 >nul",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                Assert.That(process.Start(), Is.True);
                Assert.That(SpinWait.SpinUntil(() => (bool)rootExited.GetValue(process),
                    TimeSpan.FromSeconds(10)), Is.True, "The fixture root should exit first.");
                Assert.That(process.WaitForExit(100), Is.False, "A live descendant prevents completion.");
                Assert.That(process.HasExited, Is.False);
                process.Kill();
                Assert.That(process.WaitForExit(5000), Is.True);
                Assert.That(process.HasExited, Is.True);
            }
        }

        [Test]
        public void ProductionContainedLauncherDrainsBothExplicitPipes()
        {
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            using (var process = CreateContainedHandle())
            using (var outputDone = new ManualResetEventSlim())
            using (var errorDone = new ManualResetEventSlim())
            {
                var output = new System.Collections.Concurrent.ConcurrentQueue<string>();
                var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
                var failed = 0;
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "-NoProfile -NonInteractive -Command \"[Console]::WriteLine('out'); [Console]::Error.WriteLine('err')\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                Assert.That(process.Start(), Is.True);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                Action failure = () => { Interlocked.Exchange(ref failed, 1); outputDone.Set(); errorDone.Set(); };
                Action<string> stdout = line => { if (line == null) outputDone.Set(); else output.Enqueue(line); };
                Action<string> stderr = line => { if (line == null) errorDone.Set(); else errors.Enqueue(line); };
                process.GetType().GetMethod("BeginBoundedOutputRead", flags).Invoke(process, new object[] { stdout, failure });
                process.GetType().GetMethod("BeginBoundedErrorRead", flags).Invoke(process, new object[] { stderr, failure });
                Assert.That(process.WaitForExit(10000), Is.True);
                Assert.That(outputDone.Wait(5000), Is.True);
                Assert.That(errorDone.Wait(5000), Is.True);
                Assert.That(Volatile.Read(ref failed), Is.Zero);
                Assert.That(output.ToArray(), Is.EqualTo(new[] { "out" }));
                Assert.That(errors.ToArray(), Is.EqualTo(new[] { "err" }));
                Assert.That(process.ExitCode, Is.Zero);
            }
        }

        [Test]
        public void ProductionBoundedPipeReaderDrainsOversizeLineFromDisposableChild()
        {
            var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            using (var process = new RcloneProcessFactory().Create())
            using (var done = new ManualResetEventSlim())
            {
                var lines = new System.Collections.Concurrent.ConcurrentQueue<string>();
                var failed = 0;
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "-NoProfile -NonInteractive -Command \"[Console]::WriteLine('x'*100000); [Console]::WriteLine('tail')\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                var started = false;
                try
                {
                    started = process.Start();
                    Assert.That(started, Is.True);
                    var begin = process.GetType().GetMethod("BeginBoundedOutputRead",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    Assert.That(begin, Is.Not.Null);
                    Action<string> receive = line => { if (line == null) done.Set(); else lines.Enqueue(line); };
                    Action failure = () => { Interlocked.Exchange(ref failed, 1); done.Set(); };
                    begin.Invoke(process, new object[] { receive, failure });
                    Assert.That(process.WaitForExit(10000), Is.True);
                    Assert.That(done.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    Assert.That(Volatile.Read(ref failed), Is.Zero);
                    Assert.That(lines.ToArray(), Is.EqualTo(new[] { "[rclone diagnostic line omitted]", "tail" }));
                }
                finally
                {
                    if (started && !process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
            }
        }

        [Test]
        public void Run_CancellationKillsAndWaitsForChildProcess()
        {
            var process = new FakeProcessHandle();
            var runner = new RcloneProcessRunner(new FakeProcessFactory(process));
            var cancellation = new CancellationTokenSource();
            process.OnTimedWait = () => cancellation.Cancel();

            var result = runner.Run(
                new RcloneTransferRequest
                {
                    ExecutablePath = "fake-rclone",
                    RemoteName = "games",
                    RemoteSourcePath = "game.zip",
                    DestinationPath = "partial.zip",
                    ConnectTimeoutSeconds = 30,
                    InactivityTimeoutSeconds = 30
                },
                cancellation.Token,
                null);

            Assert.That(result.WasCancelled, Is.True);
            Assert.That(process.KillCount, Is.EqualTo(1));
            Assert.That(process.TimedWaitCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(process.HasExited, Is.True);
            Assert.That(process.Disposed, Is.True);
        }

        [Test]
        public void Run_KillFailureStillPerformsBoundedWaitAndDisposesChildHandle()
        {
            var process = new FakeProcessHandle { ThrowOnKill = true };
            var runner = new RcloneProcessRunner(new FakeProcessFactory(process));
            var cancellation = new CancellationTokenSource();
            process.OnTimedWait = () => cancellation.Cancel();

            var result = runner.Run(CreateRequest(), cancellation.Token, null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.WasCancelled, Is.False);
            Assert.That(result.Message, Does.Contain("could not stop"));
            Assert.That(result.ProcessExitConfirmed, Is.False);
            Assert.That(process.TimedWaitCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(process.Disposed, Is.True);
        }

        [Test]
        public void ProductionProcessHandle_KillAndBoundedWaitObserveDisposableChildExit()
        {
            var executable = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            Assert.That(File.Exists(executable), Is.True, "Windows PowerShell is required for the disposable child-process smoke test.");

            IRcloneProcessHandle process = null;
            var started = false;
            try
            {
                process = new RcloneProcessFactory().Create();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = "-NoProfile -NonInteractive -Command \"Start-Sleep -Seconds 30\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                started = process.Start();
                Assert.That(started, Is.True);
                process.Kill();
                Assert.That(process.WaitForExit(5000), Is.True);
                Assert.That(process.HasExited, Is.True);
            }
            finally
            {
                if (process != null)
                {
                    try
                    {
                        if (started && !process.HasExited)
                        {
                            try
                            {
                                process.Kill();
                            }
                            finally
                            {
                                process.WaitForExit(5000);
                            }
                        }
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
        }

        [Test]
        public void Run_PreCancelledDoesNotStartChild()
        {
            var process = new FakeProcessHandle();
            var result = new RcloneProcessRunner(new FakeProcessFactory(process)).Run(
                CreateRequest(), new CancellationToken(true), null);
            Assert.That(result.WasCancelled, Is.True);
            Assert.That(process.StartCount, Is.Zero);
        }

        [Test]
        public void Run_CancellationDuringFactorySetupDoesNotStartChild()
        {
            var process = new FakeProcessHandle();
            using (var cancellation = new CancellationTokenSource())
            {
                var runner = new RcloneProcessRunner(new FakeProcessFactory(process, cancellation.Cancel));
                var result = runner.Run(CreateRequest(), cancellation.Token, null);
                Assert.That(result.WasCancelled, Is.True);
                Assert.That(process.StartCount, Is.Zero);
                Assert.That(process.Disposed, Is.True);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Run_LaunchExceptionAttemptsStopAndNeverAssumesChildAbsent(bool killFails)
        {
            var process = new FakeProcessHandle { ThrowOnStart = true, ThrowOnKill = killFails };
            var result = new RcloneProcessRunner(new FakeProcessFactory(process)).Run(
                CreateRequest(), CancellationToken.None, null);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(process.StartCount, Is.EqualTo(1));
            Assert.That(process.KillCount, Is.EqualTo(1));
            Assert.That(process.TimedWaitCount, Is.EqualTo(1));
            Assert.That(result.ProcessExitConfirmed, Is.EqualTo(!killFails));
            Assert.That(result.Message, Does.Contain("IOException"));
            Assert.That(result.Message, Does.Contain("HRESULT 0x"));
            Assert.That(result.Message, Does.Not.Contain("native launch may have occurred"));
            Assert.That(process.Disposed, Is.True);
        }

        [Test]
        public void Run_ReadSetupFailureStopsStartedChild()
        {
            var process = new FakeProcessHandle { ThrowOnBeginRead = true };
            var result = new RcloneProcessRunner(new FakeProcessFactory(process)).Run(
                CreateRequest(), CancellationToken.None, null);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.ProcessExitConfirmed, Is.True);
            Assert.That(process.KillCount, Is.EqualTo(1));
            Assert.That(process.Disposed, Is.True);
        }

        private static RcloneTransferRequest CreateRequest()
        {
            return new RcloneTransferRequest
            {
                ExecutablePath = "fake-rclone",
                RemoteName = "games",
                RemoteSourcePath = "game.zip",
                DestinationPath = "partial.zip",
                ConnectTimeoutSeconds = 30,
                InactivityTimeoutSeconds = 30
            };
        }

        private sealed class FakeProcessFactory : IRcloneProcessFactory
        {
            private readonly IRcloneProcessHandle process;
            private readonly Action onCreate;

            public FakeProcessFactory(IRcloneProcessHandle process, Action onCreate = null)
            {
                this.process = process;
                this.onCreate = onCreate;
            }

            public IRcloneProcessHandle Create()
            {
                onCreate?.Invoke();
                return process;
            }
        }

        private sealed class FakeProcessHandle : IRcloneProcessHandle
        {
            public ProcessStartInfo StartInfo { get; set; }
            public event DataReceivedEventHandler OutputDataReceived { add { } remove { } }
            public event DataReceivedEventHandler ErrorDataReceived { add { } remove { } }
            public bool HasExited { get; private set; }
            public int ExitCode => HasExited ? -1 : 0;
            public int KillCount { get; private set; }
            public int BlockingWaitCount { get; private set; }
            public int TimedWaitCount { get; private set; }
            public bool Disposed { get; private set; }
            public bool ThrowOnKill { get; set; }
            public Action OnTimedWait { get; set; }
            public int StartCount { get; private set; }
            public bool ThrowOnBeginRead { get; set; }
            public bool ThrowOnStart { get; set; }

            public bool Start()
            {
                StartCount++;
                if (ThrowOnStart) throw new IOException("fixture: native launch may have occurred");
                return true;
            }
            public void BeginOutputReadLine() { if (ThrowOnBeginRead) throw new IOException("fixture"); }
            public void BeginErrorReadLine() { }
            public bool WaitForExit(int milliseconds)
            {
                TimedWaitCount++;
                OnTimedWait?.Invoke();
                return HasExited;
            }

            public void WaitForExit()
            {
                BlockingWaitCount++;
                HasExited = true;
            }

            public void Kill()
            {
                KillCount++;
                if (ThrowOnKill)
                {
                    throw new InvalidOperationException("kill failed");
                }

                HasExited = true;
            }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
