using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class RcloneManifestReaderBoundsTests
    {
        [Test]
        public void ChunkReader_PreservesExactTextAndBoundary()
        {
            var json = "{\n  \"items\": []\r\n}";
            Assert.That(Read(new StringReader(json), json.Length).GetAwaiter().GetResult(), Is.EqualTo(json));
        }

        [Test]
        public void ChunkReader_RejectsOversizedSingleLine()
        {
            using (var reader = new CountingReader(1000000))
            {
                Assert.Throws<InvalidDataException>(() => Read(reader, 10000).GetAwaiter().GetResult());
                Assert.That(reader.TotalRead, Is.EqualTo(10001));
                Assert.That(reader.MaxRequest, Is.LessThanOrEqualTo(8192));
            }
        }

        [Test]
        public void ChunkReader_HandlesEmptyAndZeroBudget()
        {
            Assert.That(Read(new StringReader(string.Empty), 0).GetAwaiter().GetResult(), Is.Empty);
            Assert.Throws<InvalidDataException>(() => Read(new StringReader("x"), 0).GetAwaiter().GetResult());
        }

        private static Task<string> Read(TextReader reader, int limit)
        {
            var method = typeof(RcloneManifestReader).GetMethod(
                "ReadBoundedAsync", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (Task<string>)method.Invoke(null, new object[] { reader, limit });
        }

        private sealed class CountingReader : TextReader
        {
            private int remaining;
            public int TotalRead { get; private set; }
            public int MaxRequest { get; private set; }
            public CountingReader(int length) { remaining = length; }
            public override Task<int> ReadAsync(char[] buffer, int index, int count)
            {
                MaxRequest = Math.Max(MaxRequest, count);
                var read = Math.Min(remaining, count);
                for (var i = 0; i < read; i++) buffer[index + i] = 'x';
                remaining -= read;
                TotalRead += read;
                return Task.FromResult(read);
            }
        }
    }
}
