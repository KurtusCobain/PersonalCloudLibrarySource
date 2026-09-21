using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PersonalCloudLibrarySource.Tests.Dashboard
{
    [TestFixture]
    public class DashboardActivityServiceTests
    {
        [Test]
        public void Add_PrependsNewestRecordAndLimitsHistoryToFifty()
        {
            var service = new DashboardActivityService();
            var firstTimestamp = new DateTime(2026, 7, 12, 1, 0, 0, DateTimeKind.Utc);

            for (var index = 0; index < 55; index++)
            {
                service.Add(
                    DashboardActivityKind.Library,
                    "Event " + index,
                    firstTimestamp.AddMinutes(index));
            }

            Assert.That(service.Records.Count, Is.EqualTo(50));
            Assert.That(service.Records.First().Message, Is.EqualTo("Event 54"));
            Assert.That(service.Records.Last().Message, Is.EqualTo("Event 5"));
        }

        [Test]
        public void Add_EmptyMessage_DoesNotCreateRecord()
        {
            var service = new DashboardActivityService();

            service.Add(DashboardActivityKind.Warning, "   ");

            Assert.That(service.Records, Is.Empty);
        }

        [Test]
        public void Add_TrimsAndFlattensMultilineMessages()
        {
            var service = new DashboardActivityService();

            service.Add(
                DashboardActivityKind.TransferCompleted,
                "  Example Game\r\ncompleted successfully.  ");

            Assert.That(service.Records.Single().Message, Is.EqualTo("Example Game completed successfully."));
        }

        [Test]
        public void Add_RaisesChangedEventOnce()
        {
            var service = new DashboardActivityService();
            var changedCount = 0;
            service.Changed += (sender, args) => changedCount++;

            service.Add(DashboardActivityKind.TransferFailed, "Example Game failed.");

            Assert.That(changedCount, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentWorkerAdds_RemainCappedAndRaiseOneChangePerAcceptedRecord()
        {
            var service = new DashboardActivityService();
            var changedCount = 0;
            service.Changed += (sender, args) => Interlocked.Increment(ref changedCount);

            Parallel.For(0, 100, index =>
                service.Add(DashboardActivityKind.TransferCompleted, "Event " + index));

            Assert.That(service.Records, Has.Count.EqualTo(50));
            Assert.That(changedCount, Is.EqualTo(100));
        }

        [Test]
        public void OutOfOrderOldRecord_IsEvictedAfterTimestampOrderingNotArrivalOrdering()
        {
            var service = new DashboardActivityService();
            var baseline = new DateTime(2026, 7, 13, 2, 0, 0, DateTimeKind.Utc);
            for (var index = 0; index < 50; index++)
            {
                service.Add(DashboardActivityKind.Library, "Recent " + index, baseline.AddMinutes(index));
            }

            service.Add(DashboardActivityKind.Library, "Ancient late arrival", baseline.AddDays(-1));

            Assert.That(service.Records, Has.Count.EqualTo(50));
            Assert.That(service.Records.Select(record => record.Message), Does.Not.Contain("Ancient late arrival"));
            Assert.That(service.Records.First().Message, Is.EqualTo("Recent 49"));
        }
    }
}
