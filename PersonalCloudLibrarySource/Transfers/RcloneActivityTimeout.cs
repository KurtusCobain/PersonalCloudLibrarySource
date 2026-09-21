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
