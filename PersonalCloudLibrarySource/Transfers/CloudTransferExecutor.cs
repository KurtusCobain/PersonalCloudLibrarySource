using System;
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
