using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class ManifestGenerationPreviewTests
    {
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
