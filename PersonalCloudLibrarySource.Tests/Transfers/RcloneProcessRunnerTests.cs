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
        public void Run_CancellationKillsAndWaitsForChildProcess()
        {
            var process = new FakeProcessHandle();
            var runner = new RcloneProcessRunner(new FakeProcessFactory(process));
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

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
            cancellation.Cancel();

            var result = runner.Run(CreateRequest(), cancellation.Token, null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.WasCancelled, Is.False);
            Assert.That(result.Message, Does.Contain("could not stop"));
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

            public FakeProcessFactory(IRcloneProcessHandle process)
            {
                this.process = process;
            }

            public IRcloneProcessHandle Create()
            {
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

            public bool Start() => true;
            public void BeginOutputReadLine() { }
            public void BeginErrorReadLine() { }
            public bool WaitForExit(int milliseconds)
            {
                TimedWaitCount++;
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
