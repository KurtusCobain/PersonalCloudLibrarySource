using NUnit.Framework;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class RcloneLocalBinaryIntegrationTests
    {
        [Test]
        [NonParallelizable]
        public void OptInRealRclone_CancelledFileLeavesNoSidecarsAndRetryPreservesOtherFiles()
        {
            var rclone = Environment.GetEnvironmentVariable("PCLS_TEST_RCLONE_EXE");
            if (string.IsNullOrWhiteSpace(rclone))
                Assert.Ignore("Set PCLS_TEST_RCLONE_EXE for the disposable local-only cancellation fixture.");
            Assert.That(File.Exists(rclone), Is.True);
            var root = Path.Combine(Path.GetTempPath(), "PCLS-RcloneCancelOptIn", Guid.NewGuid().ToString("N"));
            var source = Path.Combine(root, "source.bin");
            var cache = Path.Combine(root, "cache");
            var destination = Path.Combine(cache, "payload.bin");
            var sentinel = Path.Combine(cache, "unrelated.txt");
            var previousLimit = Environment.GetEnvironmentVariable("RCLONE_BWLIMIT");
            try
            {
                Directory.CreateDirectory(cache);
                var payload = new byte[4 * 1024 * 1024];
                payload[0] = 0x5A;
                payload[payload.Length - 1] = 0xA5;
                File.WriteAllBytes(source, payload);
                File.WriteAllText(sentinel, "preserve unrelated content");
                var settings = new PersonalCloudLibrarySourceSettingsV3
                {
                    RcloneExecutablePath = rclone,
                    RcloneRemoteName = ":local:",
                    RcloneTimeoutSeconds = 30
                };
                var adapter = new RcloneTransferAdapter(new RcloneProcessRunner());
                Environment.SetEnvironmentVariable("RCLONE_BWLIMIT", "256k");
                var sawProgress = false;
                using (var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                {
                    // Redirected console progress may not flush until completion.
                    // Cancel while a real on-disk staging file still exists.
                    var watcher = Task.Run(() =>
                    {
                        while (!cancellation.IsCancellationRequested)
                        {
                            foreach (var path in Directory.GetFiles(cache, "payload.bin.pcls-partial-*"))
                            {
                                try
                                {
                                    if (new FileInfo(path).Length > 0)
                                    {
                                        sawProgress = true;
                                        cancellation.Cancel();
                                        return;
                                    }
                                }
                                catch (IOException) { }
                            }
                            Thread.Sleep(10);
                        }
                    });
                    var cancelled = adapter.Copy(settings, source.Replace('\\', '/'), destination,
                        false, Guid.NewGuid(), cancellation.Token, null);
                    cancellation.Cancel();
                    Assert.That(watcher.Wait(5000), Is.True);
                    Assert.That(sawProgress, Is.True, "Cancel only after actual rclone transfer progress.");
                    Assert.That(cancelled.Cancelled, Is.True, cancelled.Message);
                }
                Assert.That(File.Exists(destination), Is.False, "Cancelled bytes must never be published.");
                Assert.That(Directory.GetFiles(cache), Is.EquivalentTo(new[] { sentinel }),
                    "Cancellation must also remove rclone's own randomly suffixed temporary upload.");
                Environment.SetEnvironmentVariable("RCLONE_BWLIMIT", previousLimit);
                var retried = adapter.Copy(settings, source.Replace('\\', '/'), destination,
                    false, Guid.NewGuid(), CancellationToken.None, null);
                Assert.That(retried.Succeeded, Is.True, retried.Message);
                using (var hash = SHA256.Create())
                {
                    var expected = hash.ComputeHash(payload);
                    Assert.That(hash.ComputeHash(File.ReadAllBytes(source)), Is.EqualTo(expected));
                    Assert.That(hash.ComputeHash(File.ReadAllBytes(destination)), Is.EqualTo(expected));
                }
                Assert.That(File.ReadAllText(sentinel), Is.EqualTo("preserve unrelated content"));
                Assert.That(Directory.GetFiles(cache), Is.EquivalentTo(new[] { sentinel, destination }));
            }
            finally
            {
                Environment.SetEnvironmentVariable("RCLONE_BWLIMIT", previousLimit);
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Test]
        public void OptInRealRclone_LocalBackendCreatesInventoriedDirectoryWithoutCloudAccess()
        {
            // Never invoke a real process in ordinary CI or without explicit
            // user-provided executable path. ":local:" uses no cloud account.
            var rclone = Environment.GetEnvironmentVariable("PCLS_TEST_RCLONE_EXE");
            if (string.IsNullOrWhiteSpace(rclone))
                Assert.Ignore("Set PCLS_TEST_RCLONE_EXE to an installed rclone.exe to enable the disposable local-only fixture.");
            Assert.That(File.Exists(rclone), Is.True, "The opt-in rclone executable path must exist.");

            var root = Path.Combine(Path.GetTempPath(), "PCLS-RcloneLocalOptIn",
                Guid.NewGuid().ToString("N"));
            var source = Path.Combine(root, "source");
            var cache = Path.Combine(root, "cache");
            var destination = Path.Combine(cache, "real-rclone-game");
            try
            {
                Directory.CreateDirectory(Path.Combine(source, "assets"));
                Directory.CreateDirectory(cache);
                File.WriteAllText(Path.Combine(source, "play.exe"), "local backend test");
                File.WriteAllText(Path.Combine(source, "assets", "data.bin"), "texture");
                var settings = new PersonalCloudLibrarySourceSettingsV3
                {
                    RcloneExecutablePath = rclone,
                    RcloneRemoteName = ":local:",
                    RcloneTimeoutSeconds = 60
                };
                var adapter = new RcloneTransferAdapter(new RcloneProcessRunner());
                var result = adapter.Copy(settings, source.Replace('\\', '/'),
                    destination, true, CancellationToken.None, null);

                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(File.ReadAllText(Path.Combine(destination, "play.exe")),
                    Is.EqualTo("local backend test"));
                Assert.That(Directory.Exists(destination + ".pcls-partial"), Is.False);

                var store = new CacheOwnershipReceiptStore(Path.Combine(root, "receipts"));
                store.RecordCompletedWithInventory(cache, "real-rclone-game", destination);
                CacheOwnershipReceipt receipt;
                Assert.That(store.TryRead(cache, "real-rclone-game", out receipt), Is.True);
                Assert.That(receipt.Version, Is.EqualTo(2));
                bool matches;
                Assert.That(store.TryCompareInventory(cache, "real-rclone-game", destination, out matches), Is.True);
                Assert.That(matches, Is.True);

                File.WriteAllText(Path.Combine(destination, "personal.sav"), "keep");
                Assert.That(store.TryCompareInventory(cache, "real-rclone-game", destination, out matches), Is.True);
                Assert.That(matches, Is.False);
            }
            finally
            {
                // A uniquely generated disposable subtree only; never use an
                // arbitrary user-provided cache folder or configured remote.
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }
}
