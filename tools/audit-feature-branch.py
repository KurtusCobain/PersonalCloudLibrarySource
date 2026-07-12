from pathlib import Path


def replace_once(text, old, new, label):
    if new in text:
        return text
    if old not in text:
        raise SystemExit(f"Patch target missing for {label}: {old}")
    return text.replace(old, new, 1)


def ensure_after(path, anchor, line, label):
    text = path.read_text(encoding="utf-8-sig")
    if line in text:
        return
    if anchor not in text:
        raise SystemExit(f"Project anchor missing for {label}: {anchor}")
    path.write_text(text.replace(anchor, anchor + "\n" + line, 1), encoding="utf-8-sig")


navigation_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.Navigation.cs")
navigation = navigation_path.read_text(encoding="utf-8-sig")
navigation = replace_once(
    navigation,
    """DataContext = new CloudLibraryDashboardViewModel(
                    dashboardStateStore,
                    navigationService,
                    GetTransferManager(),
                    GetTransferExecutor(),
                    settings.Settings)""",
    "DataContext = new CloudLibraryDashboardViewModel(dashboardStateStore, navigationService)",
    "dashboard constructor consistency",
)
navigation_path.write_text(navigation, encoding="utf-8-sig")

executor_path = Path("PersonalCloudLibrarySource/Transfers/CloudTransferExecutor.cs")
executor_path.write_text(r'''using System;
using System.IO;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudTransferExecutor
    {
        private readonly CloudTransferManager manager;
        private readonly LocalTransferAdapter localAdapter;
        private readonly RcloneTransferAdapter rcloneAdapter;

        public CloudTransferExecutor(
            CloudTransferManager manager,
            LocalTransferAdapter localAdapter)
            : this(
                manager,
                localAdapter,
                new RcloneTransferAdapter(new RcloneProcessRunner()))
        {
        }

        public CloudTransferExecutor(
            CloudTransferManager manager,
            LocalTransferAdapter localAdapter,
            RcloneTransferAdapter rcloneAdapter)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.localAdapter = localAdapter ?? throw new ArgumentNullException(nameof(localAdapter));
            this.rcloneAdapter = rcloneAdapter ?? throw new ArgumentNullException(nameof(rcloneAdapter));
        }

        public CloudTransferExecutionResult ExecuteLocal(Guid jobId, bool isDirectory)
        {
            var job = manager.GetJob(jobId);
            if (!WaitForExecutionTurn(job))
            {
                return CloudTransferExecutionResult.CancelledResult();
            }

            try
            {
                manager.Transition(job.Id, CloudTransferState.Transferring);
                var result = isDirectory
                    ? localAdapter.CopyDirectory(
                        job.Source,
                        job.Destination,
                        job.CancellationToken,
                        (transferred, total) => UpdateProgressIfActive(job, transferred, total))
                    : localAdapter.CopyFile(
                        job.Source,
                        job.Destination,
                        job.CancellationToken,
                        (transferred, total) => UpdateProgressIfActive(job, transferred, total));

                return CompleteTransfer(job, result, isDirectory);
            }
            catch (OperationCanceledException)
            {
                TransitionToCancelledIfNeeded(job);
                return CloudTransferExecutionResult.CancelledResult();
            }
            catch (Exception ex)
            {
                TransitionToFailedIfNeeded(job, ex.Message);
                return CloudTransferExecutionResult.Failure(ex.Message, ex);
            }
        }

        public CloudTransferExecutionResult ExecuteRclone(
            Guid jobId,
            PersonalCloudLibrarySourceSettings settings)
        {
            var job = manager.GetJob(jobId);
            if (!WaitForExecutionTurn(job))
            {
                return CloudTransferExecutionResult.CancelledResult();
            }

            try
            {
                manager.Transition(job.Id, CloudTransferState.Connecting);
                manager.Transition(job.Id, CloudTransferState.Transferring);
                var result = rcloneAdapter.Copy(
                    settings,
                    job.Source,
                    job.Destination,
                    job.IsDirectory,
                    job.CancellationToken,
                    (transferred, total) => UpdateProgressIfActive(job, transferred, total));

                return CompleteTransfer(job, result, job.IsDirectory);
            }
            catch (OperationCanceledException)
            {
                TransitionToCancelledIfNeeded(job);
                return CloudTransferExecutionResult.CancelledResult();
            }
            catch (Exception ex)
            {
                TransitionToFailedIfNeeded(job, ex.Message);
                return CloudTransferExecutionResult.Failure(ex.Message, ex);
            }
        }

        private CloudTransferExecutionResult CompleteTransfer(
            CloudTransferJob job,
            CloudTransferExecutionResult result,
            bool isDirectory)
        {
            if (result.Cancelled)
            {
                TransitionToCancelledIfNeeded(job);
                return result;
            }

            if (!result.Succeeded)
            {
                TransitionToFailedIfNeeded(job, result.Message);
                return result;
            }

            if (job.IsTerminal)
            {
                return job.State == CloudTransferState.Cancelled
                    ? CloudTransferExecutionResult.CancelledResult()
                    : CloudTransferExecutionResult.Failure(job.ErrorSummary);
            }

            manager.Transition(job.Id, CloudTransferState.Verifying);
            if (!VerifyDestination(job.Destination, isDirectory, result.TotalBytes))
            {
                var message = "Transferred data did not pass destination verification.";
                manager.Transition(job.Id, CloudTransferState.Failed, message);
                return CloudTransferExecutionResult.Failure(message);
            }

            manager.Transition(job.Id, CloudTransferState.Finalizing);
            manager.Transition(job.Id, CloudTransferState.Completed);
            return result;
        }

        private bool WaitForExecutionTurn(CloudTransferJob job)
        {
            while (job.State == CloudTransferState.Queued)
            {
                if (job.CancellationToken.IsCancellationRequested)
                {
                    TransitionToCancelledIfNeeded(job);
                    return false;
                }

                Thread.Sleep(50);
            }

            return job.State == CloudTransferState.Preparing;
        }

        private void UpdateProgressIfActive(CloudTransferJob job, long transferred, long? total)
        {
            if (job.IsActive)
            {
                manager.UpdateProgress(job.Id, transferred, total);
            }
        }

        private void TransitionToCancelledIfNeeded(CloudTransferJob job)
        {
            if (!job.IsTerminal)
            {
                manager.Transition(job.Id, CloudTransferState.Cancelled);
            }
        }

        private void TransitionToFailedIfNeeded(CloudTransferJob job, string message)
        {
            if (!job.IsTerminal)
            {
                manager.Transition(job.Id, CloudTransferState.Failed, message);
            }
        }

        private static bool VerifyDestination(string destination, bool isDirectory, long? expectedBytes)
        {
            if (isDirectory)
            {
                if (!Directory.Exists(destination))
                {
                    return false;
                }

                if (!expectedBytes.HasValue)
                {
                    return true;
                }

                var actualBytes = 0L;
                foreach (var file in Directory.GetFiles(destination, "*", SearchOption.AllDirectories))
                {
                    actualBytes += new FileInfo(file).Length;
                }

                return actualBytes == expectedBytes.Value;
            }

            if (!File.Exists(destination))
            {
                return false;
            }

            return !expectedBytes.HasValue || new FileInfo(destination).Length == expectedBytes.Value;
        }
    }
}
''', encoding="utf-8")

tests_path = Path("PersonalCloudLibrarySource.Tests/Transfers/CloudTransferExecutorTests.cs")
tests_path.write_text(r'''using NUnit.Framework;
using System;
using System.IO;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class CloudTransferExecutorTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-TransferExecutorTests", Guid.NewGuid().ToString("N"));
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

        [Test]
        public void ExecuteLocalFile_Success_TransitionsJobToCompleted()
        {
            var source = Path.Combine(testRoot, "source.bin");
            var destination = Path.Combine(testRoot, "cache", "game.bin");
            File.WriteAllText(source, "content");
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", source, destination, "LocalFolder");
            var executor = new CloudTransferExecutor(manager, new LocalTransferAdapter());

            var result = executor.ExecuteLocal(job.Id, false);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(job.State, Is.EqualTo(CloudTransferState.Completed));
            Assert.That(File.Exists(destination), Is.True);
            Assert.That(job.BytesTransferred, Is.EqualTo(new FileInfo(destination).Length));
        }

        [Test]
        public void ExecuteLocalFile_Failure_TransitionsJobToFailedAndStartsNextJob()
        {
            var missingSource = Path.Combine(testRoot, "missing.bin");
            var destination = Path.Combine(testRoot, "cache", "missing.bin");
            var manager = new CloudTransferManager(1);
            var failedJob = manager.Enqueue(Guid.NewGuid(), "Missing", missingSource, destination, "LocalFolder");
            var nextJob = manager.Enqueue(Guid.NewGuid(), "Next", "source-2", "dest-2", "LocalFolder");
            var executor = new CloudTransferExecutor(manager, new LocalTransferAdapter());

            var result = executor.ExecuteLocal(failedJob.Id, false);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(failedJob.State, Is.EqualTo(CloudTransferState.Failed));
            Assert.That(failedJob.ErrorSummary, Does.Contain("does not exist"));
            Assert.That(nextJob.State, Is.EqualTo(CloudTransferState.Preparing));
        }

        [Test]
        public void ExecuteLocalFile_CancelledJob_DoesNotStartCopy()
        {
            var source = Path.Combine(testRoot, "source.bin");
            var destination = Path.Combine(testRoot, "cache", "game.bin");
            File.WriteAllText(source, "content");
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(Guid.NewGuid(), "Game", source, destination, "LocalFolder");
            manager.Cancel(job.Id);
            var executor = new CloudTransferExecutor(manager, new LocalTransferAdapter());

            var result = executor.ExecuteLocal(job.Id, false);

            Assert.That(result.Cancelled, Is.True);
            Assert.That(job.State, Is.EqualTo(CloudTransferState.Cancelled));
            Assert.That(File.Exists(destination), Is.False);
        }

        [Test]
        public void ExecuteRcloneFile_Success_TransitionsJobToCompleted()
        {
            var destination = Path.Combine(testRoot, "cache", "cloud-game.bin");
            var runner = new FakeRcloneProcessRunner(request =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath));
                File.WriteAllText(request.DestinationPath, "cloud content");
                return RcloneProcessResult.Success("ok");
            });
            var manager = new CloudTransferManager(1);
            var job = manager.Enqueue(
                Guid.NewGuid(),
                "Cloud Game",
                "library/cloud-game.bin",
                destination,
                PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType);
            var executor = new CloudTransferExecutor(
                manager,
                new LocalTransferAdapter(),
                new RcloneTransferAdapter(runner));
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                RcloneExecutablePath = "rclone",
                RcloneRemoteName = "games",
                RcloneTimeoutSeconds = 30
            };

            var result = executor.ExecuteRclone(job.Id, settings);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(job.State, Is.EqualTo(CloudTransferState.Completed));
            Assert.That(File.Exists(destination), Is.True);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("cloud content"));
            Assert.That(runner.RunCount, Is.EqualTo(1));
        }

        private sealed class FakeRcloneProcessRunner : IRcloneProcessRunner
        {
            private readonly Func<RcloneTransferRequest, RcloneProcessResult> handler;

            public FakeRcloneProcessRunner(Func<RcloneTransferRequest, RcloneProcessResult> handler)
            {
                this.handler = handler;
            }

            public int RunCount { get; private set; }

            public RcloneProcessResult Run(
                RcloneTransferRequest request,
                CancellationToken cancellationToken,
                Action<long, long?> progress)
            {
                RunCount++;
                progress?.Invoke(512, 1024);
                return handler(request);
            }
        }
    }
}
''', encoding="utf-8")

project_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.csproj")
ensure_after(
    project_path,
    '    <Compile Include="Dashboard\\CacheStatusService.cs" />',
    '    <Compile Include="Dashboard\\DashboardActivityService.cs" />',
    "dashboard activity service registration",
)
ensure_after(
    project_path,
    '    <Compile Include="Transfers\\RcloneTransferModels.cs" />',
    '    <Compile Include="Transfers\\TransferActivityTracker.cs" />',
    "transfer activity tracker registration",
)

test_project_path = Path("PersonalCloudLibrarySource.Tests/PersonalCloudLibrarySource.Tests.csproj")
ensure_after(
    test_project_path,
    '    <Compile Include="Dashboard\\FriendlySourceNameProviderTests.cs" />',
    '    <Compile Include="Dashboard\\DashboardActivityServiceTests.cs" />',
    "dashboard activity tests registration",
)
ensure_after(
    test_project_path,
    '    <Compile Include="Transfers\\LocalTransferAdapterTests.cs" />',
    '    <Compile Include="Transfers\\RcloneCommandBuilderTests.cs" />',
    "rclone command tests registration",
)
ensure_after(
    test_project_path,
    '    <Compile Include="Transfers\\RcloneCommandBuilderTests.cs" />',
    '    <Compile Include="Transfers\\RcloneProgressParserTests.cs" />',
    "rclone progress tests registration",
)
ensure_after(
    test_project_path,
    '    <Compile Include="Transfers\\RcloneProgressParserTests.cs" />',
    '    <Compile Include="Transfers\\RcloneTransferAdapterTests.cs" />',
    "rclone adapter tests registration",
)
ensure_after(
    test_project_path,
    '    <Compile Include="Transfers\\RcloneTransferAdapterTests.cs" />',
    '    <Compile Include="Transfers\\TransferActivityTrackerTests.cs" />',
    "transfer activity tests registration",
)

print("Feature branch repair patch applied.")
