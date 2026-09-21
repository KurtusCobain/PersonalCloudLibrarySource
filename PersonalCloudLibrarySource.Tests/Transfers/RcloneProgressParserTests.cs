using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class RcloneProgressParserTests
    {
        [Test]
        public void TryParse_StatsLine_ReturnsTransferredAndTotalBytes()
        {
            long transferred;
            long total;

            var parsed = RcloneProgressParser.TryParse(
                "Transferred:        1.500 MiB / 3.000 MiB, 50%, 1.000 MiB/s, ETA 1s",
                out transferred,
                out total);

            Assert.That(parsed, Is.True);
            Assert.That(transferred, Is.EqualTo(1572864));
            Assert.That(total, Is.EqualTo(3145728));
        }

        [Test]
        public void TryParse_UnrelatedLine_ReturnsFalse()
        {
            long transferred;
            long total;

            var parsed = RcloneProgressParser.TryParse(
                "2026/07/12 00:00:00 INFO  : Starting transfer",
                out transferred,
                out total);

            Assert.That(parsed, Is.False);
            Assert.That(transferred, Is.EqualTo(0));
            Assert.That(total, Is.EqualTo(0));
        }
    }
}
