using NUnit.Framework;
using System;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class RcloneActivityTimeoutTests
    {
        [Test]
        public void SilentProcess_ExpiresConnectTimeoutBeforeInactivityWindow()
        {
            var start = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
            var timeout = new RcloneActivityTimeout(start, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));

            Assert.That(timeout.GetExpiredKind(start.AddSeconds(4)), Is.EqualTo(RcloneTimeoutKind.None));
            Assert.That(timeout.GetExpiredKind(start.AddSeconds(5)), Is.EqualTo(RcloneTimeoutKind.Connect));
        }

        [Test]
        public void ProgressingTransfer_CanOutliveOriginalTotalDuration()
        {
            var start = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
            var timeout = new RcloneActivityTimeout(start, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));

            timeout.RecordActivity(start.AddSeconds(4));
            timeout.RecordActivity(start.AddSeconds(12));
            timeout.RecordActivity(start.AddSeconds(20));

            Assert.That(timeout.GetExpiredKind(start.AddSeconds(29)), Is.EqualTo(RcloneTimeoutKind.None));
            Assert.That(timeout.GetExpiredKind(start.AddSeconds(30)), Is.EqualTo(RcloneTimeoutKind.Inactivity));
        }

        [Test]
        public void FirstOutputEndsConnectPhaseAndStartsInactivityWindow()
        {
            var start = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
            var timeout = new RcloneActivityTimeout(start, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(20));
            timeout.RecordActivity(start.AddSeconds(4));

            Assert.That(timeout.GetExpiredKind(start.AddSeconds(6)), Is.EqualTo(RcloneTimeoutKind.None));
            Assert.That(timeout.GetExpiredKind(start.AddSeconds(24)), Is.EqualTo(RcloneTimeoutKind.Inactivity));
        }
    }
}
