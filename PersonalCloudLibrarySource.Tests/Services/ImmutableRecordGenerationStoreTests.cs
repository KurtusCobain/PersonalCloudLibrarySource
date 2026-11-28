using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class ImmutableRecordGenerationStoreTests
    {
        private string root;
        private Guid key;
        private Type storeType;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-GenerationTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            key = Guid.NewGuid();
            storeType = typeof(TransferPartialPathPolicy).Assembly.GetType(
                "PersonalCloudLibrarySource.ImmutableRecordGenerationStore", true);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private object Store(Action<string> interrupt = null, Guid? logicalKey = null)
            => Activator.CreateInstance(storeType, new object[] { root, logicalKey ?? key, interrupt });

        private object Call(object store, string method, params object[] args)
        {
            try { return storeType.GetMethod(method).Invoke(store, args); }
            catch (TargetInvocationException ex) { throw ex.InnerException; }
        }

        private static T Property<T>(object head, string name)
            => (T)head.GetType().GetProperty(name).GetValue(head);

        private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
        private string Record(int sequence) => Path.Combine(root, sequence.ToString("D8") + ".record");

        [Test]
        public void AppendsWithoutReplacingAndRejectsStaleHead()
        {
            var store = Store();
            Assert.That(Call(store, "ReadHead"), Is.Null);
            var first = Call(store, "Append", "", Bytes("first"));
            var original = File.ReadAllBytes(Record(1));
            var digest = Property<string>(first, "Digest");
            var second = Call(Store(), "Append", digest, Bytes("second"));
            Assert.That(Property<int>(second, "Sequence"), Is.EqualTo(2));
            Assert.That(Property<byte[]>(second, "Payload"), Is.EqualTo(Bytes("second")));
            Assert.That(File.ReadAllBytes(Record(1)), Is.EqualTo(original));
            Assert.Throws<InvalidOperationException>(() => Call(store, "Append", digest, Bytes("stale")));
            Assert.That(File.Exists(Record(3)), Is.False);
            var read = Call(Store(), "ReadHead");
            Assert.That(Property<string>(read, "Digest"), Is.EqualTo(Property<string>(second, "Digest")));
        }

        [TestCase("staged")]
        [TestCase("published")]
        public void InterruptedAppendNeverSilentlyOverwritesOrRollsBack(string point)
        {
            var first = Call(Store(), "Append", "", Bytes("first"));
            var original = File.ReadAllBytes(Record(1));
            var digest = Property<string>(first, "Digest");
            var interrupted = Store(stage => { if (stage == point) throw new IOException("fixture interruption"); });
            Assert.Throws<IOException>(() => Call(interrupted, "Append", digest, Bytes("second")));
            Assert.That(File.ReadAllBytes(Record(1)), Is.EqualTo(original));
            var reopened = Store();
            if (point == "staged")
            {
                var pending = Path.Combine(root, "generation.pending");
                var evidence = File.ReadAllBytes(pending);
                Assert.Throws<IOException>(() => Call(reopened, "ReadHead"));
                Assert.Throws<IOException>(() => Call(reopened, "Append", digest, Bytes("retry")));
                Assert.That(File.ReadAllBytes(pending), Is.EqualTo(evidence));
                Assert.That(File.Exists(Record(2)), Is.False);
            }
            else
            {
                Assert.That(Property<byte[]>(Call(reopened, "ReadHead"), "Payload"), Is.EqualTo(Bytes("second")));
                Assert.Throws<InvalidOperationException>(() => Call(reopened, "Append", digest, Bytes("retry")));
            }
        }

        [TestCase("gap")]
        [TestCase("corrupt")]
        [TestCase("foreign")]
        [TestCase("wrong-key")]
        [TestCase("bom")]
        public void AmbiguousOrInvalidChainFailsClosed(string problem)
        {
            var first = Call(Store(), "Append", "", Bytes("first"));
            var second = Call(Store(), "Append", Property<string>(first, "Digest"), Bytes("second"));
            if (problem == "gap") File.Move(Record(1), Record(3));
            if (problem == "corrupt") File.AppendAllText(Record(1), "x");
            if (problem == "foreign") File.WriteAllText(Path.Combine(root, "unknown"), "preserve");
            if (problem == "bom") File.WriteAllText(Record(1), File.ReadAllText(Record(1)), new UTF8Encoding(true));
            var reopened = problem == "wrong-key" ? Store(null, Guid.NewGuid()) : Store();
            var evidence = File.ReadAllBytes(Record(2));
            Assert.Throws<IOException>(() => Call(reopened, "ReadHead"));
            Assert.Throws<IOException>(() => Call(reopened, "Append", Property<string>(second, "Digest"), Bytes("third")));
            Assert.That(File.ReadAllBytes(Record(2)), Is.EqualTo(evidence));
            Assert.That(File.Exists(Path.Combine(root, "generation.pending")), Is.False);
        }

        [Test]
        public void RejectsInvalidInputAndReturnsDefensivePayloadCopies()
        {
            var store = Store();
            Assert.Throws<ArgumentException>(() => Call(store, "Append", null, Bytes("x")));
            Assert.Throws<IOException>(() => Call(store, "Append", "", new byte[0]));
            Assert.Throws<IOException>(() => Call(store, "Append", "", new byte[1024 * 1024 + 1]));
            var payload = Bytes("original");
            var head = Call(store, "Append", "", payload);
            payload[0] = 0;
            Property<byte[]>(head, "Payload")[0] = 0;
            Assert.That(Property<byte[]>(head, "Payload"), Is.EqualTo(Bytes("original")));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StagedObjectSubstitutionOrOccupiedTargetPreservesEvidence(bool occupiedTarget)
        {
            var first = Call(Store(), "Append", "", Bytes("first"));
            var original = File.ReadAllBytes(Record(1));
            var digest = Property<string>(first, "Digest");
            var pending = Path.Combine(root, "generation.pending");
            var displaced = Path.Combine(root, "displaced-stage");
            var store = Store(point =>
            {
                if (point != "staged") return;
                if (occupiedTarget) File.WriteAllText(Record(2), "unrelated");
                else
                {
                    File.Move(pending, displaced);
                    File.WriteAllBytes(pending, File.ReadAllBytes(displaced));
                }
            });
            Assert.Throws<IOException>(() => Call(store, "Append", digest, Bytes("second")));
            Assert.That(File.ReadAllBytes(Record(1)), Is.EqualTo(original));
            Assert.That(File.Exists(pending), Is.True);
            if (occupiedTarget) Assert.That(File.ReadAllText(Record(2)), Is.EqualTo("unrelated"));
            else
            {
                Assert.That(File.Exists(Record(2)), Is.False);
                Assert.That(File.ReadAllBytes(pending), Is.EqualTo(File.ReadAllBytes(displaced)));
            }
            Assert.Throws<IOException>(() => Call(Store(), "ReadHead"));
        }

        [Test]
        public void NamespaceLimitRefusesWithoutDeletingAnyEntry()
        {
            for (var i = 1; i <= 65; i++) File.WriteAllText(Record(i), "preserve");
            var store = Store();
            Assert.Throws<IOException>(() => Call(store, "ReadHead"));
            Assert.Throws<IOException>(() => Call(store, "Append", "", Bytes("new")));
            Assert.That(Directory.GetFiles(root, "*.record").Length, Is.EqualTo(65));
            Assert.That(File.ReadAllText(Record(65)), Is.EqualTo("preserve"));
            Assert.That(File.Exists(Path.Combine(root, "generation.pending")), Is.False);
        }

        [Test]
        public void ExistingWriterLockRefusesCompetingReaderAndWriter()
        {
            var store = Store();
            Call(store, "ReadHead");
            var path = Path.Combine(root, "generation.lock");
            using (var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Throws<IOException>(() => Call(Store(), "ReadHead"));
                Assert.Throws<IOException>(() => Call(Store(), "Append", "", Bytes("x")));
            }
            Assert.That(Call(store, "ReadHead"), Is.Null);
        }
    }
}
