using NUnit.Framework;
using System;
using System.Linq;

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
    }
}
