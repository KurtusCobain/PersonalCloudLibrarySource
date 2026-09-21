using System;
using System.Collections.Generic;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    public enum DashboardActivityKind
    {
        Library = 0,
        Manifest = 1,
        TransferStarted = 2,
        TransferCompleted = 3,
        TransferFailed = 4,
        TransferCancelled = 5,
        Source = 6,
        Verification = 7,
        Warning = 8
    }

    public sealed class DashboardActivityRecord
    {
        public DashboardActivityKind Kind { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }
        public Guid? GameId { get; set; }
        public string DisplayTime => TimestampUtc.ToLocalTime().ToString("g");
    }

    public sealed class DashboardActivityService
    {
        private const int MaximumRecordCount = 50;
        private readonly object syncRoot = new object();
        private readonly List<DashboardActivityRecord> records = new List<DashboardActivityRecord>();

        public event EventHandler Changed;

        public IReadOnlyList<DashboardActivityRecord> Records
        {
            get
            {
                lock (syncRoot)
                {
                    return records.ToList().AsReadOnly();
                }
            }
        }

        public void Add(
            DashboardActivityKind kind,
            string message,
            DateTime? timestampUtc = null,
            Guid? gameId = null)
        {
            var normalized = NormalizeMessage(message);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            lock (syncRoot)
            {
                var record = new DashboardActivityRecord
                {
                    Kind = kind,
                    Message = normalized,
                    TimestampUtc = timestampUtc?.ToUniversalTime() ?? DateTime.UtcNow,
                    GameId = gameId
                };
                var insertIndex = records.FindIndex(existing =>
                    existing.TimestampUtc <= record.TimestampUtc);
                if (insertIndex < 0)
                {
                    records.Add(record);
                }
                else
                {
                    records.Insert(insertIndex, record);
                }

                if (records.Count > MaximumRecordCount)
                {
                    records.RemoveRange(MaximumRecordCount, records.Count - MaximumRecordCount);
                }
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        private static string NormalizeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            var parts = message
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts).Trim();
        }
    }
}
