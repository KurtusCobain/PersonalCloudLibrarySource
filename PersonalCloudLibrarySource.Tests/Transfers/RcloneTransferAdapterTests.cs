using NUnit.Framework;
using System;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class RcloneTransferAdapterTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-RcloneTransferTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, true);
            }
        }

        [TestCase(FileAttributes.Normal, true)]
        [TestCase(FileAttributes.Directory, true)]
        [TestCase(FileAttributes.ReparsePoint, false)]
        [TestCase(FileAttributes.Directory | FileAttributes.ReparsePoint, false)]
        public void StagedTransfers_RefuseLinkedTemporaryTargets(
            FileAttributes attributes, bool expected)
        {
            var method = typeof(RcloneTransferAdapter).GetMethod(
                "IsSafeStagedEntry",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That((bool)method.Invoke(null, new object[] { attributes }), Is.EqualTo(expected));
        }

        [Test]
        public void CopyFile_Success_MovesVerifiedPartialFileIntoFinalDestination()
        {
            var destination = Path.Combine(testRoot, "cache", "game.zip");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath));
                File.WriteAllText(request.DestinationPath, "cloud content");
                return RcloneProcessResult.Success("ok");
            });
            var adapter = new RcloneTransferAdapter(runner);
            var settings = CreateSettings();

            var result = adapter.Copy(
                settings,
                "library/game.zip",
                destination,
                false,
                CancellationToken.None,
                null);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(destination), Is.True);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("cloud content"));
            Assert.That(File.Exists(destination + ".pcls-partial"), Is.False);
            Assert.That(runner.LastRequest.DestinationPath, Is.EqualTo(destination + ".pcls-partial"));
        }

        [Test]
        public void CopyDirectory_Success_MovesVerifiedPartialDirectoryIntoFinalDestination()
        {
            var destination = Path.Combine(testRoot, "cache", "Disc Game");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(Path.Combine(request.DestinationPath, "nested"));
                File.WriteAllText(Path.Combine(request.DestinationPath, "nested", "game.cue"), "cue");
                return RcloneProcessResult.Success("ok");
            });
            var adapter = new RcloneTransferAdapter(runner);

            var result = adapter.Copy(
                CreateSettings(),
                "library/Disc Game",
                destination,
                true,
                CancellationToken.None,
                null);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(Path.Combine(destination, "nested", "game.cue")), Is.True);
            Assert.That(Directory.Exists(destination + ".pcls-partial"), Is.False);
        }

        [Test]
        public void CopyFile_ZeroByteFile_IsCommittedSuccessfully()
        {
            var destination = Path.Combine(testRoot, "cache", "empty.dat");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath));
                File.WriteAllBytes(request.DestinationPath, new byte[0]);
                return RcloneProcessResult.Success("ok");
            });

            var result = new RcloneTransferAdapter(runner).Copy(
                CreateSettings(), "library/empty.dat", destination,
                false, CancellationToken.None, null);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(destination), Is.True);
            Assert.That(new FileInfo(destination).Length, Is.EqualTo(0));
            Assert.That(File.Exists(destination + ".pcls-partial"), Is.False);
        }

        [Test]
        public void CopyDirectory_EmptyDirectory_IsCommittedSuccessfully()
        {
            var destination = Path.Combine(testRoot, "cache", "empty-folder");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(request.DestinationPath);
                return RcloneProcessResult.Success("ok");
            });

            var result = new RcloneTransferAdapter(runner).Copy(
                CreateSettings(), "library/empty-folder", destination,
                true, CancellationToken.None, null);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(Directory.Exists(destination), Is.True);
            Assert.That(Directory.GetFileSystemEntries(destination).Length, Is.EqualTo(0));
            Assert.That(Directory.Exists(destination + ".pcls-partial"), Is.False);
        }

        [Test]
        public void CopyDirectory_OnlyZeroByteFiles_SucceedsAndPreservesFiles()
        {
            var destination = Path.Combine(testRoot, "cache", "zero-byte-folder");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(request.DestinationPath);
                File.WriteAllBytes(Path.Combine(request.DestinationPath, "marker.txt"), new byte[0]);
                return RcloneProcessResult.Success("ok");
            });

            var result = new RcloneTransferAdapter(runner).Copy(
                CreateSettings(), "library/zero-byte-folder", destination,
                true, CancellationToken.None, null);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(File.Exists(Path.Combine(destination, "marker.txt")), Is.True);
            Assert.That(new FileInfo(Path.Combine(destination, "marker.txt")).Length, Is.EqualTo(0));
        }

        [Test]
        public void Copy_Cancelled_RemovesPartialDataAndLeavesFinalDestinationMissing()
        {
            var destination = Path.Combine(testRoot, "cache", "game.zip");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath));
                File.WriteAllText(request.DestinationPath, "partial");
                return RcloneProcessResult.Cancelled("cancelled");
            });
            var adapter = new RcloneTransferAdapter(runner);

            var result = adapter.Copy(
                CreateSettings(),
                "library/game.zip",
                destination,
                false,
                CancellationToken.None,
                null);

            Assert.That(result.Cancelled, Is.True);
            Assert.That(File.Exists(destination), Is.False);
            Assert.That(File.Exists(destination + ".pcls-partial"), Is.False);
        }

        [Test]
        public void Copy_PreexistingStagingIsPreservedWithoutStartingProcess()
        {
            var destination = Path.Combine(testRoot, "game.zip");
            var id = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, id);
            File.WriteAllText(partial, "preserve");
            var called = false;
            var runner = new FakeRcloneProcessRunner(request =>
            {
                called = true;
                return RcloneProcessResult.Success("unexpected");
            });
            var result = new RcloneTransferAdapter(runner).Copy(CreateSettings(), "game.zip",
                destination, false, id, CancellationToken.None, null);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(called, Is.False);
            Assert.That(File.ReadAllText(partial), Is.EqualTo("preserve"));
        }

        [Test]
        public void Copy_ExistingDestination_FailsBeforeStartingRclone()
        {
            var destination = Path.Combine(testRoot, "game.zip");
            File.WriteAllText(destination, "existing");
            var runner = new FakeRcloneProcessRunner(request => RcloneProcessResult.Success("unexpected"));
            var adapter = new RcloneTransferAdapter(runner);

            var result = adapter.Copy(
                CreateSettings(),
                "library/game.zip",
                destination,
                false,
                CancellationToken.None,
                null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("already exists"));
            Assert.That(runner.RunCount, Is.EqualTo(0));
            Assert.That(File.ReadAllText(destination), Is.EqualTo("existing"));
        }

        [Test]
        public void Copy_JobOwnedPartialPathIsPassedToRunnerAndRemovedAfterFinalization()
        {
            var destination = Path.Combine(testRoot, "cache", "owned.zip");
            var jobId = Guid.NewGuid();
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath));
                File.WriteAllText(request.DestinationPath, "owned");
                return RcloneProcessResult.Success("ok");
            });

            var result = new RcloneTransferAdapter(runner).Copy(
                CreateSettings(),
                "library/owned.zip",
                destination,
                false,
                jobId,
                CancellationToken.None,
                null);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(runner.LastRequest.DestinationPath, Is.EqualTo(TransferPartialPathPolicy.Create(destination, jobId)));
            Assert.That(File.Exists(runner.LastRequest.DestinationPath), Is.False);
        }

        [Test]
        public void Copy_CancellationCleanupFailure_IsReportedAsFailureNotCancellation()
        {
            var destination = Path.Combine(testRoot, "cache", "locked.zip");
            var jobId = Guid.NewGuid();
            FileStream lockStream = null;
            try
            {
                var runner = new FakeRcloneProcessRunner(request =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath));
                    lockStream = new FileStream(request.DestinationPath, FileMode.Open, FileAccess.Write, FileShare.None);
                    lockStream.WriteByte(1);
                    lockStream.Flush();
                    return RcloneProcessResult.Cancelled("cancelled");
                });

                var result = new RcloneTransferAdapter(runner).Copy(
                    CreateSettings(),
                    "library/locked.zip",
                    destination,
                    false,
                    jobId,
                    CancellationToken.None,
                    null);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Cancelled, Is.False);
                Assert.That(result.Message, Does.Contain("cleanup"));
            }
            finally
            {
                lockStream?.Dispose();
                var partial = TransferPartialPathPolicy.Create(destination, jobId);
                if (File.Exists(partial))
                {
                    File.Delete(partial);
                }
            }
        }

        [Test]
        public void Copy_FileStagingReplacedByDirectoryPreservesContentsAndRequiresRecovery()
        {
            var destination = Path.Combine(testRoot, "cache", "game.zip");
            var id = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, id);
            var sentinel = Path.Combine(partial, "keep.txt");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                File.Delete(request.DestinationPath);
                Directory.CreateDirectory(request.DestinationPath);
                File.WriteAllText(sentinel, "preserve");
                return RcloneProcessResult.Success("unexpected type");
            }) { ReportProgress = false };
            var result = new RcloneTransferAdapter(runner).Copy(CreateSettings(), "game.zip",
                destination, false, id, CancellationToken.None, null);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RequiresManualRecovery, Is.True);
            Assert.That(File.ReadAllText(sentinel), Is.EqualTo("preserve"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Copy_FileIdentityReplacementIsNeverPublishedOrDeleted(bool cancel)
        {
            var destination = Path.Combine(testRoot, "game.zip");
            var id = Guid.NewGuid();
            var original = Path.Combine(testRoot, "original-stage.bin");
            var partial = TransferPartialPathPolicy.Create(destination, id);
            var runner = new FakeRcloneProcessRunner(request =>
            {
                File.WriteAllText(request.DestinationPath, "original");
                File.Move(request.DestinationPath, original);
                File.WriteAllText(request.DestinationPath, "original");
                return cancel ? RcloneProcessResult.Cancelled("fixture") : RcloneProcessResult.Success("fixture");
            });
            var result = new RcloneTransferAdapter(runner).Copy(CreateSettings(), "game.zip",
                destination, false, id, CancellationToken.None, null);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RequiresManualRecovery, Is.True);
            Assert.That(File.ReadAllText(partial), Is.EqualTo("original"));
            Assert.That(File.ReadAllText(original), Is.EqualTo("original"));
            Assert.That(File.Exists(destination), Is.False);
        }

        [Test]
        public void Copy_UnconfirmedChildExitPreservesStagingEvenWhenCancelled()
        {
            var destination = Path.Combine(testRoot, "cache", "uncertain.zip");
            var jobId = Guid.NewGuid();
            using (var cancellation = new CancellationTokenSource())
            {
                var runner = new FakeRcloneProcessRunner(request =>
                {
                    File.WriteAllText(request.DestinationPath, "possibly active");
                    cancellation.Cancel();
                    return RcloneProcessResult.Failure("child not stopped", processExitConfirmed: false);
                }) { ReportProgress = false };
                var result = new RcloneTransferAdapter(runner).Copy(CreateSettings(),
                    "library/uncertain.zip", destination, false, jobId, cancellation.Token, null);
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Cancelled, Is.False);
                Assert.That(File.Exists(destination), Is.False);
                Assert.That(File.ReadAllText(TransferPartialPathPolicy.Create(destination, jobId)),
                    Is.EqualTo("possibly active"));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Copy_SameLengthEditAfterProcessExitIsPreserved(bool cancel)
        {
            var destination = Path.Combine(testRoot, "file.bin");
            var job = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, job);
            var runner = new FakeRcloneProcessRunner(request =>
            {
                File.WriteAllText(request.DestinationPath, "original");
                return RcloneProcessResult.Success("ok");
            }) { ReportProgress = false };
            using (var cancellation = new CancellationTokenSource())
            {
                var result = new RcloneTransferAdapter(runner).Copy(CreateSettings(), "file.bin", destination,
                    false, job, cancellation.Token, null, state =>
                    {
                        if (state != CloudTransferState.Finalizing) return;
                        File.WriteAllText(partial, "modified");
                        if (cancel) cancellation.Cancel();
                    });
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.RequiresManualRecovery, Is.True);
                Assert.That(File.ReadAllText(partial), Is.EqualTo("modified"));
                Assert.That(File.Exists(destination), Is.False);
            }
        }

        [Test]
        public void CopyDirectory_ZeroByteInjectionAfterProcessExitCannotPublish()
        {
            var destination = Path.Combine(testRoot, "game");
            var job = Guid.NewGuid();
            var partial = TransferPartialPathPolicy.Create(destination, job);
            var foreign = Path.Combine(partial, "foreign.bin");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                File.WriteAllText(Path.Combine(request.DestinationPath, "game.bin"), "download");
                return RcloneProcessResult.Success("ok");
            });
            var result = new RcloneTransferAdapter(runner).Copy(CreateSettings(), "game", destination,
                true, job, CancellationToken.None, null, state =>
                {
                    if (state == CloudTransferState.Finalizing) File.WriteAllBytes(foreign, new byte[0]);
                });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RequiresManualRecovery, Is.True);
            Assert.That(File.Exists(foreign), Is.True);
            Assert.That(Directory.Exists(destination), Is.False);
        }

        [Test]
        public void Copy_DestinationAppearsAtFinalizationPreservesStaging()
        {
            var destination = Path.Combine(testRoot, "cache", "final.zip");
            var jobId = Guid.NewGuid();
            var runner = new FakeRcloneProcessRunner(request =>
            {
                File.WriteAllText(request.DestinationPath, "download");
                return RcloneProcessResult.Success("ok");
            });
            var result = new RcloneTransferAdapter(runner).Copy(CreateSettings(),
                "library/final.zip", destination, false, jobId, CancellationToken.None, null,
                state =>
                {
                    if (state == CloudTransferState.Finalizing) File.WriteAllText(destination, "foreign");
                });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RequiresManualRecovery, Is.True);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("foreign"));
            Assert.That(File.ReadAllText(TransferPartialPathPolicy.Create(destination, jobId)), Is.EqualTo("download"));
        }

        [Test]
        public void Copy_ThrowingFinalProgressCallback_FailsBeforeCommitAndCleansPartial()
        {
            var destination = Path.Combine(testRoot, "cache", "progress.zip");
            var jobId = Guid.NewGuid();
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath));
                File.WriteAllText(request.DestinationPath, "verified cloud content");
                return RcloneProcessResult.Success("ok");
            })
            {
                ReportProgress = false
            };

            var result = new RcloneTransferAdapter(runner).Copy(
                CreateSettings(),
                "library/progress.zip",
                destination,
                false,
                jobId,
                CancellationToken.None,
                (transferred, total) => throw new InvalidOperationException("progress consumer failed"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("progress consumer failed"));
            Assert.That(File.Exists(destination), Is.False);
            Assert.That(File.Exists(TransferPartialPathPolicy.Create(destination, jobId)), Is.False);
        }

        [Test]
        public void CopyDirectory_NestedJunctionFailsClosedWithoutDeletingExternalFiles()
        {
            var valuable = Path.Combine(testRoot, "valuable");
            Directory.CreateDirectory(valuable);
            var keep = Path.Combine(valuable, "keep.txt");
            File.WriteAllText(keep, "untouched");
            // Check junction support before starting the fake rclone execution.
            var probe = Path.Combine(testRoot, "junction-probe");
            if (!TryCreateJunction(probe, valuable))
                Assert.Ignore("Windows directory junction creation is unavailable.");
            Directory.Delete(probe, false);

            var destination = Path.Combine(testRoot, "cache", "linked-game");
            string staged = null;
            string junction = null;
            try
            {
                var runner = new FakeRcloneProcessRunner(request =>
                {
                    staged = request.DestinationPath;
                    Directory.CreateDirectory(staged);
                    junction = Path.Combine(staged, "unsafe-link");
                    if (!TryCreateJunction(junction, valuable))
                        throw new IOException("Junction became unavailable during test.");
                    return RcloneProcessResult.Success("synthetic success with linked descendant");
                });
                var result = new RcloneTransferAdapter(runner).Copy(
                    CreateSettings(), "library/linked-game", destination,
                    true, CancellationToken.None, null);

                Assert.That(result.Succeeded, Is.False);
                Assert.That(Directory.Exists(destination), Is.False);
                Assert.That(File.ReadAllText(keep), Is.EqualTo("untouched"));
                Assert.That(result.Message, Does.Contain("cleanup"));
            }
            finally
            {
                // Remove only the junction object before deleting the test-owned
                // staging folder, so fixture cleanup never traverses its target.
                if (!string.IsNullOrWhiteSpace(junction) && Directory.Exists(junction))
                    Directory.Delete(junction, false);
                if (!string.IsNullOrWhiteSpace(staged) && Directory.Exists(staged))
                    Directory.Delete(staged, true);
            }
        }

        private static bool TryCreateJunction(string junction, string destination)
        {
            using (var process = Process.Start(new ProcessStartInfo(
                "cmd.exe", "/c mklink /J \"" + junction + "\" \"" + destination + "\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            }))
            {
                process.WaitForExit();
                return process.ExitCode == 0 && Directory.Exists(junction);
            }
        }

        private static PersonalCloudLibrarySourceSettingsV3 CreateSettings()
        {
            return new PersonalCloudLibrarySourceSettingsV3
            {
                RcloneExecutablePath = "rclone",
                RcloneRemoteName = "games",
                RcloneTimeoutSeconds = 30
            };
        }

        private sealed class FakeRcloneProcessRunner : IRcloneProcessRunner
        {
            private readonly Func<RcloneTransferRequest, RcloneProcessResult> handler;

            public FakeRcloneProcessRunner(Func<RcloneTransferRequest, RcloneProcessResult> handler)
            {
                this.handler = handler;
            }

            public int RunCount { get; private set; }
            public RcloneTransferRequest LastRequest { get; private set; }
            public bool ReportProgress { get; set; } = true;

            public RcloneProcessResult Run(
                RcloneTransferRequest request,
                CancellationToken cancellationToken,
                Action<long, long?> progress)
            {
                RunCount++;
                LastRequest = request;
                if (ReportProgress)
                {
                    progress?.Invoke(512, 1024);
                }
                return handler(request);
            }
        }
    }
}
