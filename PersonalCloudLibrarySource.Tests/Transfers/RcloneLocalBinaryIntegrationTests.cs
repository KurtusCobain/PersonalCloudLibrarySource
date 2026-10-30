using NUnit.Framework;
using System;
using System.IO;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class RcloneLocalBinaryIntegrationTests
    {
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
