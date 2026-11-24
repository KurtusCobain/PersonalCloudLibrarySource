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
