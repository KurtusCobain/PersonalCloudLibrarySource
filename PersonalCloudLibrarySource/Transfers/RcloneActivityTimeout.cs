using System;

namespace PersonalCloudLibrarySource
{
    public enum RcloneTimeoutKind
    {
        None = 0,
        Connect = 1,
        Inactivity = 2
    }

    public sealed class RcloneActivityTimeout
    {
        private readonly object syncRoot = new object();
        private readonly DateTime startedAt;
        private readonly TimeSpan connectTimeout;
        private readonly TimeSpan inactivityTimeout;
        private DateTime lastActivityAt;
        private bool connected;
        private long maxTransferred = -1;
        private long maxTotal = -1;

        public RcloneActivityTimeout(
            DateTime startedAt,
            TimeSpan connectTimeout,
            TimeSpan inactivityTimeout)
        {
            if (connectTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(connectTimeout));
            }

            if (inactivityTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(inactivityTimeout));
            }

            this.startedAt = startedAt;
            this.connectTimeout = connectTimeout;
            this.inactivityTimeout = inactivityTimeout;
            lastActivityAt = startedAt;
        }

        public void RecordActivity(DateTime timestamp)
        {
            lock (syncRoot)
            {
                connected = true;
                if (timestamp > lastActivityAt)
                {
                    lastActivityAt = timestamp;
                }
            }
        }

        // Initial output establishes activity; unchanged statistics do not
        // indefinitely extend the inactivity deadline.
        public void RecordObservation(DateTime timestamp, long? transferred, long? total)
        {
            lock (syncRoot)
            {
                var advanced = (transferred.HasValue && transferred.Value > maxTransferred) ||
                    (total.HasValue && total.Value > maxTotal);
                if (transferred.HasValue) maxTransferred = Math.Max(maxTransferred, transferred.Value);
                if (total.HasValue) maxTotal = Math.Max(maxTotal, total.Value);
                if (!connected || advanced)
                {
                    connected = true;
                    if (timestamp > lastActivityAt) lastActivityAt = timestamp;
                }
            }
        }

        public RcloneTimeoutKind GetExpiredKind(DateTime timestamp)
        {
            lock (syncRoot)
            {
                if (!connected)
                {
                    return timestamp - startedAt >= connectTimeout
                        ? RcloneTimeoutKind.Connect
                        : RcloneTimeoutKind.None;
                }

                return timestamp - lastActivityAt >= inactivityTimeout
                    ? RcloneTimeoutKind.Inactivity
                    : RcloneTimeoutKind.None;
            }
        }
    }
}
