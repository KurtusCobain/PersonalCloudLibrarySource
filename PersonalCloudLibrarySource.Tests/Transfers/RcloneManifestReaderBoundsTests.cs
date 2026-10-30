using NUnit.Framework;
using System;
using System.Reflection;
using System.Text;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class RcloneManifestReaderBoundsTests
    {
        [Test]
        public void StreamBuffer_RejectedLine_DoesNotGrowDestination()
        {
            var buffer = new StringBuilder();
            Assert.That(Append(buffer, "short", 20), Is.True);
            var before = buffer.ToString();

            Assert.That(Append(buffer, new string('x', 25), 20), Is.False);
            Assert.That(buffer.ToString(), Is.EqualTo(before));
        }

        [Test]
        public void StreamBuffer_ExactlyAtBoundary_AllowsThenRefusesExtraContent()
        {
            var buffer = new StringBuilder();
            var line = "abc";
            var limit = line.Length + Environment.NewLine.Length;

            Assert.That(Append(buffer, line, limit), Is.True);
            Assert.That(buffer.ToString(), Is.EqualTo(line + Environment.NewLine));
            Assert.That(Append(buffer, "more", limit), Is.False);
            Assert.That(buffer.Length, Is.EqualTo(limit));
        }

        [Test]
        public void StreamBuffer_NullLine_IsHarmless()
        {
            var buffer = new StringBuilder();
            Assert.That(Append(buffer, null, 10), Is.True);
            Assert.That(buffer.Length, Is.Zero);
        }

        private static bool Append(StringBuilder buffer, string line, int limit)
        {
            var method = typeof(RcloneManifestReader).GetMethod(
                "TryAppendLineWithinLimit", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Expected streamed manifest bound helper.");
            return (bool)method.Invoke(null, new object[] { buffer, line, limit });
        }
    }
}
