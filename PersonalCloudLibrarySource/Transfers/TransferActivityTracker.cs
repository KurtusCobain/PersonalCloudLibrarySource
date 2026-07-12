using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    public sealed class TransferActivityTracker
    {
        private readonly object syncRoot = new object();
        private readonly HashSet<Guid> processedJobIds = new HashSet<Guid>();

        public IReadOnlyList<DashboardActivityRecord> CollectNew(IEnumerable<CloudTransferJob> sourceJobs)
        {
            var results = new List<DashboardActivityRecord>();
            var jobs = sourceJobs ?? Enumerable.Empty<CloudTransferJob>();

            lock (syncRoot)
            {
                foreach (var job in jobs
                    .Where(value => value != null && value.IsTerminal)
                    .OrderBy(value => value.CompletedAt ?? value.CreatedAt))
                {
                    if (!processedJobIds.Add(job.Id))
                    {
                        continue;
                    }

                    var record = CreateRecord(job);
                    if (record != null)
                    {
                        results.Add(record);
                    }
                }
            }

            return results.AsReadOnly();
        }

        private static DashboardActivityRecord CreateRecord(CloudTransferJob job)
        {
            var displayName = string.IsNullOrWhiteSpace(job.DisplayName)
                ? "Unnamed game"
                : job.DisplayName.Trim();
            var timestamp = job.CompletedAt ?? DateTime.UtcNow;

            switch (job.State)
            {
                case CloudTransferState.Completed:
                    return new DashboardActivityRecord
                    {
                        Kind = DashboardActivityKind.TransferCompleted,
                        Message = displayName + " is ready to play.",
                        TimestampUtc = timestamp,
                        GameId = job.GameId
                    };

                case CloudTransferState.Failed:
                    return new DashboardActivityRecord
                    {
                        Kind = DashboardActivityKind.TransferFailed,
                        Message = displayName + " failed: " +
                                  (string.IsNullOrWhiteSpace(job.ErrorSummary)
                                      ? "Transfer failed."
                                      : job.ErrorSummary.Trim()),
                        TimestampUtc = timestamp,
                        GameId = job.GameId
                    };

                case CloudTransferState.Cancelled:
                    return new DashboardActivityRecord
                    {
                        Kind = DashboardActivityKind.TransferCancelled,
                        Message = displayName + " transfer was cancelled.",
                        TimestampUtc = timestamp,
                        GameId = job.GameId
                    };

                default:
                    return null;
            }
        }
    }
}
