using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class ManifestGenerationPreviewTests
    {
        [Test]
        public void Generator_RefusesCatalogExceedingImporterItemLimit()
        {
            var method = typeof(ManifestGenerationService).GetMethod(
                "ValidateGeneratedItemCount",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.DoesNotThrow(() => method.Invoke(null,
                new object[] { ManifestParserValidator.MaxManifestItems }));
            var error = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                method.Invoke(null, new object[] { ManifestParserValidator.MaxManifestItems + 1 }));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(error.InnerException.Message, Does.Contain("Choose a smaller library folder"));
        }

        [Test]
        public void Generation_RejectsOutputTooLargeInUtf8BeforeWrite()
        {
            var method = typeof(ManifestGenerationService).GetMethod(
                "ValidateSerializedManifest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var text = new string('\u00e9', ManifestParserValidator.MaxManifestCharacters / 2 + 1);
            var error = Assert.Throws<System.Reflection.TargetInvocationException>(() => method.Invoke(null, new object[] { text }));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(error.InnerException.Message, Does.Contain("importer size limit"));
        }

        [Test]
        public void Generation_RefusesReportOverwritingManifest()
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-ManifestCollision-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                var target = Path.Combine(root, "catalog.json");
                File.WriteAllText(target, "existing");
                File.WriteAllText(Path.Combine(root, "game.nes"), "fixture");
                Assert.Throws<InvalidOperationException>(() => new ManifestGenerationService().Generate(
                    new ManifestGenerationOptions { SourceRoot = root, OutputPath = target, ReportPath = target }));
                Assert.That(File.ReadAllText(target), Is.EqualTo("existing"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW",
            CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool CreateHardLinkNative(string link, string existing, IntPtr reserved);

        [Test]
        public void SafeWriter_RefusesHardlinkedOutputBeforeStagingOrBackup()
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-Writer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var target = Path.Combine(root, "manifest.json");
                var alias = Path.Combine(root, "alias.json");
                var backups = Path.Combine(root, "backups");
                File.WriteAllText(target, "original");
                Assert.That(CreateHardLinkNative(alias, target, IntPtr.Zero), Is.True);
                Assert.Throws<IOException>(() => new SafeFileWriteService().WriteAllText(
                    target, "replacement", backups, true));
                Assert.That(File.ReadAllText(target), Is.EqualTo("original"));
                Assert.That(File.ReadAllText(alias), Is.EqualTo("original"));
                Assert.That(Directory.GetFiles(root, "*.tmp"), Is.Empty);
                Assert.That(Directory.Exists(backups), Is.False);
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void SafeWriter_RepeatedUpdatesPreserveEveryBackup()
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-BackupFixture-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                var target = Path.Combine(root, "catalog.json");
                var backups = Path.Combine(root, "backup");
                var writer = new SafeFileWriteService();
                writer.WriteAllText(target, "one", backups, true);
                writer.WriteAllText(target, "two", backups, true);
                writer.WriteAllText(target, "three", backups, true);
                Assert.That(File.ReadAllText(target), Is.EqualTo("three"));
                Assert.That(Directory.GetFiles(backups, "*.bak").Select(File.ReadAllText),
                    Is.EquivalentTo(new[] { "one", "two" }));
                Assert.That(Directory.GetFiles(root, "*.tmp"), Is.Empty);
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void Preview_RealFolder_IsReadOnly_AndUsesSameDiscoveryAsGeneration()
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-Preview-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "TestGame.exe"), "dummy fixture");
                File.WriteAllText(Path.Combine(root, "ignored.txt"), "not a game");
                var destination = Path.Combine(root, "generated.json");
                var reportPath = Path.Combine(root, "generated-report.txt");

                var report = new ManifestGenerationService().Preview(
                    new ManifestGenerationOptions
                    {
                        SourceRoot = root, OutputPath = destination, ReportPath = reportPath
                    }, CancellationToken.None);

                Assert.That(report.ItemCount, Is.EqualTo(1));
                Assert.That(report.Manifest.Items[0].Title, Is.EqualTo("TestGame"));
                Assert.That(report.ScannedEntryCount, Is.EqualTo(2));
                Assert.That(File.Exists(destination), Is.False);
                Assert.That(File.Exists(reportPath), Is.False);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void Preview_PackagePriorityAndAncestorBoundariesRemainStable()
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-ManifestFixture-" + Guid.NewGuid().ToString("N"));
            try
            {
                var package = Path.Combine(root, "PC", "Game");
                var similarlyNamed = Path.Combine(root, "PC", "GameExtra");
                Directory.CreateDirectory(package);
                Directory.CreateDirectory(similarlyNamed);
                Directory.CreateDirectory(Path.Combine(package, "Data"));
                foreach (var name in new[] { "z.m3u", "a.m3u", "a.exe" })
                    File.WriteAllText(Path.Combine(package, name), "fixture");
                File.WriteAllText(Path.Combine(package, "Data", "asset.nes"), "fixture");
                File.WriteAllText(Path.Combine(similarlyNamed, "standalone.nes"), "fixture");
                var result = new ManifestGenerationService().Preview(new ManifestGenerationOptions
                    { SourceRoot = root }, CancellationToken.None);
                Assert.That(result.ItemCount, Is.EqualTo(2));
                Assert.That(result.Manifest.Items.Single(item => item.SourceType == "directory").LaunchFile,
                    Is.EqualTo("a.m3u"));
                Assert.That(result.Manifest.Items.Single(item => item.SourceType == "file").Title,
                    Is.EqualTo("standalone"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void Preview_LargeSyntheticCatalogPreservesAllItems()
        {
            const int packages = 2500;
            var timer = Stopwatch.StartNew();
            var result = new ManifestGenerationService(new LargePackageFileSystem(packages)).Preview(
                new ManifestGenerationOptions { SourceRoot = @"C:\Games" }, CancellationToken.None);
            timer.Stop();
            Assert.That(result.ItemCount, Is.EqualTo(packages));
            Assert.That(result.DetectedDirectoryItemCount, Is.EqualTo(packages));
            Assert.That(result.ScannedEntryCount, Is.EqualTo(packages * 3));
            Assert.That(result.Manifest.Items.Select(item => item.Id).Distinct().Count(), Is.EqualTo(packages));
            Console.WriteLine("PCLS_BENCHMARK manifest_preview packages={0} entries={1} elapsed_ms={2:F3}",
                packages, result.ScannedEntryCount, timer.Elapsed.TotalMilliseconds);
        }

        private sealed class LargePackageFileSystem : IManifestGenerationFileSystem
        {
            private readonly int count;
            public LargePackageFileSystem(int count) { this.count = count; }
            public bool DirectoryExists(string path) => true;
            public IEnumerable<string> EnumerateDirectories(string path) =>
                path == @"C:\Games" ? Enumerable.Range(0, count).Select(index => Path.Combine(path, "Game-" + index))
                    : Enumerable.Empty<string>();
            public IEnumerable<string> EnumerateFiles(string path) =>
                path == @"C:\Games" ? Enumerable.Empty<string>() : new[]
                    { Path.Combine(path, "game.exe"), Path.Combine(path, "data.nes") };
        }

        [Test]
        public void Preview_RejectsCollidingGeneratedIdsWithoutChangingExistingManifest()
        {
            var root = Path.Combine(Path.GetTempPath(), "PCLS-IdentityCollision-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "A+B.nes"), "first");
                File.WriteAllText(Path.Combine(root, "A-B.nes"), "second");
                var target = Path.Combine(root, "catalog.json");
                File.WriteAllText(target, "existing");
                var options = new ManifestGenerationOptions { SourceRoot = root, OutputPath = target, NoReport = true };
                var previewError = Assert.Throws<InvalidOperationException>(() =>
                    new ManifestGenerationService().Preview(options, CancellationToken.None));
                Assert.That(previewError.Message, Does.Contain("IDs collide"));
                Assert.Throws<InvalidOperationException>(() => new ManifestGenerationService().Generate(options));
                Assert.That(File.ReadAllText(target), Is.EqualTo("existing"));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Preview_RefusesMalformedOrRepeatedEnumeration(bool repeated)
        {
            var error = Assert.Throws<IOException>(() =>
                new ManifestGenerationService(new InvalidFileSystem(repeated)).Preview(
                    new ManifestGenerationOptions { SourceRoot = @"C:\Games" }, CancellationToken.None));
            Assert.That(error.Message, Does.Contain("outside, repeated or non-child"));
        }

        private sealed class InvalidFileSystem : IManifestGenerationFileSystem
        {
            private readonly bool repeated;
            public InvalidFileSystem(bool repeated) { this.repeated = repeated; }
            public bool DirectoryExists(string path) => true;
            public IEnumerable<string> EnumerateDirectories(string path) =>
                Enumerable.Empty<string>();
            public IEnumerable<string> EnumerateFiles(string path)
            {
                if (repeated)
                {
                    yield return Path.Combine(path, "game.nes");
                    yield return Path.Combine(path, "GAME.NES");
                }
                else yield return Path.Combine(path, "..", "outside.nes");
            }
        }

        [Test]
        public void Preview_FailsCleanly_OnExcessiveEntriesWithoutWriting()
        {
            var service = new ManifestGenerationService(new OversizedFileSystem());
            var error = Assert.Throws<InvalidOperationException>(() =>
                service.Preview(new ManifestGenerationOptions
                {
                    SourceRoot = @"C:\Games", OutputPath = @"C:\games-preview-should-never-exist.json"
                }, CancellationToken.None));
            Assert.That(error.Message, Does.Contain("scan limit"));
        }

        [Test]
        public void Preview_CancelledBeforeScan_DoesNotReadOrWrite()
        {
            var ct = new CancellationTokenSource();
            ct.Cancel();
            Assert.Throws<OperationCanceledException>(() =>
                new ManifestGenerationService().Preview(
                    new ManifestGenerationOptions { SourceRoot = @"C:\Games" }, ct.Token));
        }

        private sealed class OversizedFileSystem : IManifestGenerationFileSystem
        {
            public bool DirectoryExists(string path) => true;
            public IEnumerable<string> EnumerateDirectories(string path)
            {
                return Enumerable.Empty<string>();
            }
            public IEnumerable<string> EnumerateFiles(string path)
            {
                return Enumerable.Range(0, 10001)
                    .Select(index => Path.Combine(path, "game-" + index + ".exe"));
            }
        }
    }
}
