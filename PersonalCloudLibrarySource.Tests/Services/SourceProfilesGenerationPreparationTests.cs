using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class SourceProfilesGenerationPreparationTests
    {
        private string root;
        private string legacy;
        private string generations;
        private Type serviceType;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-GenerationPreparation", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            legacy = Path.Combine(root, SourceProfilesFileStore.FileName);
            generations = Path.Combine(root, "source-profiles.generations.v1");
            serviceType = typeof(SourceProfilesFileStore).Assembly.GetType(
                "PersonalCloudLibrarySource.SourceProfilesGenerationPreparation", true);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private object Service(Action<string> checkpoint = null)
            => Activator.CreateInstance(serviceType, new object[] { root, checkpoint });

        private object Call(object service, string method, params object[] args)
        {
            try { return serviceType.GetMethod(method).Invoke(service, args); }
            catch (TargetInvocationException ex) { throw ex.InnerException; }
        }

        private void WriteLegacy(string name = "Original")
        {
            var json = new SourceProfilesConfigurationCodec().Serialize(new[]
            {
                new PclsSourceProfileDefinition
                {
                    ProfileId = "9eac75ad-97f2-41e2-8c18-177d387ee029",
                    DisplayName = name, ProviderType = "LocalFile",
                    LocalManifestPath = "fixture.json", IsLegacySingleSource = true
                }
            });
            File.WriteAllText(legacy, json + "\r\n", new UTF8Encoding(true));
        }

        [Test]
        public void PreviewDoesNotCreateFilesAndPreparePreservesExactLegacyBytes()
        {
            WriteLegacy();
            var bytes = File.ReadAllBytes(legacy);
            var service = Service();
            var review = Call(service, "Preview");
            Assert.That(Directory.GetFileSystemEntries(root).Length, Is.EqualTo(1));
            var digest = Call(service, "Prepare", review);
            Assert.That(Call(Service(), "InspectPrepared"), Is.EqualTo(digest));
            Assert.That(File.ReadAllBytes(legacy), Is.EqualTo(bytes));
            Assert.That(new SourceProfilesFileStore().Load(root).Succeeded, Is.True);
            var record = File.ReadAllText(Path.Combine(generations, "00000001.record")).Split('\n');
            var envelope = Encoding.UTF8.GetString(Convert.FromBase64String(record[5])).Split('\n');
            Assert.That(Convert.FromBase64String(envelope[5]), Is.EqualTo(bytes));
            Assert.Throws<IOException>(() => Call(service, "Prepare", review));
            Assert.That(Directory.GetFiles(generations, "*.record").Length, Is.EqualTo(1));
        }

        [TestCase("content")]
        [TestCase("identity")]
        public void ChangedLegacyAfterPreviewRefusesBeforeCreatingGeneration(string change)
        {
            WriteLegacy();
            var service = Service();
            var review = Call(service, "Preview");
            if (change == "content") WriteLegacy("Changed");
            else
            {
                var displaced = legacy + ".displaced";
                File.Move(legacy, displaced);
                File.WriteAllBytes(legacy, File.ReadAllBytes(displaced));
            }
            var bytes = File.ReadAllBytes(legacy);
            Assert.Throws<InvalidOperationException>(() => Call(service, "Prepare", review));
            Assert.That(Directory.Exists(generations), Is.False);
            Assert.That(File.ReadAllBytes(legacy), Is.EqualTo(bytes));
        }

        [TestCase("missing")]
        [TestCase("invalid")]
        [TestCase("encoding")]
        [TestCase("oversized")]
        public void InvalidLegacyCannotBePreviewed(string kind)
        {
            if (kind == "invalid") File.WriteAllText(legacy, "{}");
            if (kind == "encoding") File.WriteAllBytes(legacy, new byte[] { 0xff, 0xfe, 0xff });
            if (kind == "oversized") File.WriteAllBytes(legacy, new byte[512 * 1024 + 1]);
            Assert.Catch<IOException>(() => Call(Service(), "Preview"));
            Assert.That(Directory.Exists(generations), Is.False);
            Assert.That(File.Exists(legacy + ".write-lock"), Is.False);
        }

        [TestCase("directory-created")]
        [TestCase("staged")]
        [TestCase("published")]
        public void InterruptedPreparationPreservesLegacyAndRequiresReconciliation(string point)
        {
            WriteLegacy();
            var bytes = File.ReadAllBytes(legacy);
            var service = Service(stage => { if (stage == point) throw new IOException("fixture interruption"); });
            var review = Call(service, "Preview");
            Assert.Throws<IOException>(() => Call(service, "Prepare", review));
            Assert.That(File.ReadAllBytes(legacy), Is.EqualTo(bytes));
            Assert.That(Directory.Exists(generations), Is.True);
            Assert.Throws<IOException>(() => Call(Service(), "Prepare", review));
            if (point == "published")
                Assert.That(Call(Service(), "InspectPrepared"), Is.Not.Null);
            else
                Assert.Throws<IOException>(() => Call(Service(), "InspectPrepared"));
            Assert.That(File.ReadAllBytes(legacy), Is.EqualTo(bytes));
        }

        [Test]
        public void OlderWriterCanStillChangeLegacyAndReconciliationRefuses()
        {
            WriteLegacy();
            var service = Service();
            Call(service, "Prepare", Call(service, "Preview"));
            WriteLegacy("Old writer changed profiles");
            Assert.Throws<IOException>(() => Call(Service(), "InspectPrepared"));
            Assert.That(Directory.GetFiles(generations, "*.record").Length, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OccupiedGenerationNamespaceIsNeverAdopted(bool directory)
        {
            WriteLegacy();
            var service = Service();
            var review = Call(service, "Preview");
            var original = File.ReadAllBytes(legacy);
            if (directory)
            {
                Directory.CreateDirectory(generations);
                File.WriteAllText(Path.Combine(generations, "unrelated.txt"), "preserve");
            }
            else File.WriteAllText(generations, "preserve");
            Assert.Throws<IOException>(() => Call(service, "Prepare", review));
            Assert.That(File.ReadAllBytes(legacy), Is.EqualTo(original));
            Assert.That(File.ReadAllText(directory ? Path.Combine(generations, "unrelated.txt") : generations),
                Is.EqualTo("preserve"));
            if (directory) Assert.That(Directory.GetFileSystemEntries(generations).Length, Is.EqualTo(1));
        }

        [Test]
        public void ReviewFromAnotherDirectoryCannotAuthorizePreparation()
        {
            WriteLegacy();
            var review = Call(Service(), "Preview");
            var other = Path.Combine(root, "other");
            Directory.CreateDirectory(other);
            var otherLegacy = Path.Combine(other, SourceProfilesFileStore.FileName);
            File.Copy(legacy, otherLegacy);
            var otherService = Activator.CreateInstance(serviceType, new object[] { other, null });
            Assert.Throws<InvalidOperationException>(() => Call(otherService, "Prepare", review));
            Assert.That(Directory.GetFileSystemEntries(other).Length, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(otherLegacy), Is.EqualTo(File.ReadAllBytes(legacy)));
        }

        [Test]
        public void CompetingLegacyWriterRefusesPreparation()
        {
            WriteLegacy();
            var service = Service();
            var review = Call(service, "Preview");
            using (var held = new FileStream(legacy + ".write-lock", FileMode.CreateNew,
                FileAccess.ReadWrite, FileShare.None))
            {
                Assert.Throws<IOException>(() => Call(service, "Prepare", review));
                Assert.That(Directory.Exists(generations), Is.False);
            }
        }
    }
}
