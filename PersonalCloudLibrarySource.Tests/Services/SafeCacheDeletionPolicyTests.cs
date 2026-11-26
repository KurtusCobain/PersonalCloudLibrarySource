using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class SafeCacheDeletionPolicyTests
    {
        private string testRoot;
        private string cacheRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-DeletionPolicyTests", Guid.NewGuid().ToString("N"));
            cacheRoot = Path.Combine(testRoot, "cache");
            Directory.CreateDirectory(cacheRoot);
        }

        [TearDown]
        public void TearDown()
        {
            var full = Path.GetFullPath(testRoot);
            var fixtureBase = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "PCLS-DeletionPolicyTests")) + Path.DirectorySeparatorChar;
            Assert.That(full.StartsWith(fixtureBase, StringComparison.OrdinalIgnoreCase), Is.True, "Fixture cleanup escaped its disposable root.");
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }

        [Test]
        public void Authorize_ChildFile_IsAllowed()
        {
            var target = Path.Combine(cacheRoot, "game", "play.exe");

            var result = new SafeCacheDeletionPolicy().Authorize(cacheRoot, target, false);

            Assert.That(result.Allowed, Is.True, result.Reason);
        }

        [Test]
        public void Authorize_SiblingPrefix_IsOutsideCache()
        {
            var result = new SafeCacheDeletionPolicy().Authorize(cacheRoot, Path.Combine(testRoot, "cache-evil", "game"), false);

            Assert.That(result.Allowed, Is.False);
            Assert.That(result.Reason, Does.Contain("outside"));
        }

        [Test]
        public void Authorize_OutsideCache_RequiresExplicitOptIn()
        {
            var outside = Path.Combine(testRoot, "outside", "game.exe");

            Assert.That(new SafeCacheDeletionPolicy().Authorize(cacheRoot, outside, false).Allowed, Is.False);
            Assert.That(new SafeCacheDeletionPolicy().Authorize(cacheRoot, outside, true).Allowed, Is.True);
        }

        [Test]
        public void Authorize_CacheRootAndVolumeRoot_AreRefused()
        {
            var policy = new SafeCacheDeletionPolicy();

            Assert.That(policy.Authorize(cacheRoot, cacheRoot, true).Allowed, Is.False);
            Assert.That(policy.Authorize(cacheRoot, Path.GetPathRoot(cacheRoot), true).Allowed, Is.False);
        }

        [TestCase(@"\\?\C:\game.exe")]
        [TestCase(@"\\.\C:\game.exe")]
        [TestCase(@"C:\game.exe:alternate")]
        [TestCase(@"C:\folder\NUL.txt")]
        [TestCase(@"C:\folder\game.exe.")]
        public void Authorize_UnsupportedWindowsAlias_IsRefused(string target)
        {
            Assert.That(new SafeCacheDeletionPolicy().Authorize(cacheRoot, target, true).Allowed, Is.False);
        }

        [Test]
        public void Authorize_OutsideOptInNeverAuthorizesCacheAncestor()
        {
            var policy = new SafeCacheDeletionPolicy();
            Assert.That(policy.Authorize(cacheRoot, testRoot, true).Allowed, Is.False);
            Assert.That(policy.Authorize(cacheRoot, Path.Combine(cacheRoot, ".."), true).Allowed, Is.False);
        }

        [Test]
        public void Authorize_UncShareRoot_IsRefusedWithoutAccessingShare()
        {
            var result = new SafeCacheDeletionPolicy().Authorize(cacheRoot, @"\\server\share\", true);

            Assert.That(result.Allowed, Is.False);
            Assert.That(result.Reason, Does.Contain("root"));
        }

        [Test]
        public void Authorize_RelativeCurrentDirectory_IsRefusedEvenWithOutsideOptIn()
        {
            var result = new SafeCacheDeletionPolicy().Authorize(string.Empty, ".", true);

            Assert.That(result.Allowed, Is.False);
            Assert.That(result.Reason, Does.Contain("fully rooted"));
        }

        [Test]
        public void Authorize_JunctionTargetAndAncestor_AreRefusedEvenWithOptIn()
        {
            var real = Path.Combine(testRoot, "real");
            var junction = Path.Combine(cacheRoot, "junction");
            Directory.CreateDirectory(real);
            if (!TryCreateJunction(junction, real)) Assert.Ignore("Junction creation is unavailable on this Windows host.");

            try
            {
                var policy = new SafeCacheDeletionPolicy();
                Assert.That(policy.Authorize(cacheRoot, junction, true).Allowed, Is.False);
                Assert.That(policy.Authorize(cacheRoot, Path.Combine(junction, "child"), true).Allowed, Is.False);
            }
            finally
            {
                var junctionFull = Path.GetFullPath(junction);
                Assert.That(PathBoundary.IsContained(testRoot, junctionFull), Is.True);
                if (Directory.Exists(junctionFull)) Directory.Delete(junctionFull, false);
            }
        }

        [Test]
        public void Executor_DeletesAuthorizedFileAndDirectoryOnlyInsideFixture()
        {
            var file = Path.Combine(cacheRoot, "file.bin");
            var directory = Path.Combine(cacheRoot, "directory");
            File.WriteAllText(file, "data");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "child.bin"), "data");
            var executor = new SafeCacheDeletionExecutor();

            Assert.That(executor.Delete(cacheRoot, file, false).Allowed, Is.True);
            Assert.That(executor.Delete(cacheRoot, directory, false).Allowed, Is.True);
            Assert.That(File.Exists(file), Is.False);
            Assert.That(Directory.Exists(directory), Is.False);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW",
            CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool CreateHardLinkNative(string link, string existing, IntPtr reserved);

        [Test]
        public void FileOnlyExecutor_OrdinaryNestedFileDeletionPreservesSiblingAndAncestor()
        {
            var directory = Path.Combine(cacheRoot, "nested", "game");
            Directory.CreateDirectory(directory);
            var target = Path.Combine(directory, "play.exe");
            var sibling = Path.Combine(directory, "keep.sav");
            File.WriteAllText(target, "fixture");
            File.WriteAllText(sibling, "preserve");
            var result = new SafeCacheDeletionExecutor().DeleteFileOnly(cacheRoot, target, false);
            Assert.That(result.Allowed, Is.True, result.Reason);
            Assert.That(File.Exists(target), Is.False);
            Assert.That(File.ReadAllText(sibling), Is.EqualTo("preserve"));
            Assert.That(Directory.Exists(directory), Is.True);
            // The guard must release its directory handles on return.
            var moved = directory + "-moved";
            Directory.Move(directory, moved);
            Assert.That(File.ReadAllText(Path.Combine(moved, "keep.sav")), Is.EqualTo("preserve"));
        }

        [Test]
        public void InspectedPathIdentityCheckRefusesDifferentOrdinaryFile()
        {
            var first = Path.Combine(cacheRoot, "first.bin");
            var second = Path.Combine(cacheRoot, "second.bin");
            File.WriteAllText(first, "first");
            File.WriteAllText(second, "second");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var open = typeof(TransferPartialPathPolicy).GetMethod("OpenPrivateFileForReview", flags);
            var verify = typeof(TransferPartialPathPolicy).GetMethod("RequireInspectedFilePath", flags);
            Assert.That(open, Is.Not.Null);
            Assert.That(verify, Is.Not.Null);
            using (var stream = (FileStream)open.Invoke(null, new object[] { first, true }))
            {
                Assert.DoesNotThrow(() => verify.Invoke(null, new object[] { first, stream.SafeFileHandle }));
                var failure = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
                    verify.Invoke(null, new object[] { second, stream.SafeFileHandle }));
                Assert.That(failure.InnerException, Is.TypeOf<IOException>());
            }
            Assert.That(File.ReadAllText(first), Is.EqualTo("first"));
            Assert.That(File.ReadAllText(second), Is.EqualTo("second"));
        }

        [Test]
        public void FileOnlyExecutor_RefusesHardlinkToOriginalSource()
        {
            var original = Path.Combine(testRoot, "original.bin");
            var alias = Path.Combine(cacheRoot, "alias.bin");
            File.WriteAllText(original, "original fixture");
            Assert.That(CreateHardLinkNative(alias, original, IntPtr.Zero), Is.True);
            var result = new SafeCacheDeletionExecutor().DeleteFileOnly(cacheRoot, alias, false);
            Assert.That(result.Allowed, Is.False);
            Assert.That(File.ReadAllText(alias), Is.EqualTo("original fixture"));
            Assert.That(File.ReadAllText(original), Is.EqualTo("original fixture"));
        }

        [Test]
        public void DirectoryExecutor_RefusesHardlinkBeforeDeletingOrdinaryFiles()
        {
            var original = Path.Combine(testRoot, "original.bin");
            var cached = Path.Combine(cacheRoot, "cached");
            Directory.CreateDirectory(cached);
            var ordinary = Path.Combine(cached, "ordinary.bin");
            var alias = Path.Combine(cached, "alias.bin");
            File.WriteAllText(original, "preserve original");
            File.WriteAllText(ordinary, "preserve ordinary");
            Assert.That(CreateHardLinkNative(alias, original, IntPtr.Zero), Is.True);
            var result = new SafeCacheDeletionExecutor().Delete(cacheRoot, cached, false);
            Assert.That(result.Allowed, Is.False);
            Assert.That(File.ReadAllText(original), Is.EqualTo("preserve original"));
            Assert.That(File.ReadAllText(alias), Is.EqualTo("preserve original"));
            Assert.That(File.ReadAllText(ordinary), Is.EqualTo("preserve ordinary"));
        }

        [Test]
        public void DirectoryExecutor_RefusesLockedDescendantBeforeDeletingOrdinaryFiles()
        {
            var cached = Path.Combine(cacheRoot, "cached");
            Directory.CreateDirectory(cached);
            var ordinary = Path.Combine(cached, "ordinary.bin");
            var locked = Path.Combine(cached, "locked.bin");
            File.WriteAllText(ordinary, "preserve ordinary");
            File.WriteAllText(locked, "preserve locked");
            using (var stream = File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var result = new SafeCacheDeletionExecutor().Delete(cacheRoot, cached, false);
                Assert.That(result.Allowed, Is.False);
                Assert.That(File.ReadAllText(ordinary), Is.EqualTo("preserve ordinary"));
            }
            Assert.That(File.ReadAllText(locked), Is.EqualTo("preserve locked"));
        }

        [Test]
        public void FileOnlyExecutor_NeverInvokesDirectoryRemoval()
        {
            var replacement = Path.Combine(cacheRoot, "replaced.exe");
            Directory.CreateDirectory(replacement);
            var save = Path.Combine(replacement, "keep.sav");
            File.WriteAllText(save, "keep");
            var mutation = false;
            var executor = new SafeCacheDeletionExecutor(new SafeCacheDeletionPolicy(),
                ignored => mutation = true, ignored => mutation = true);
            Assert.That(executor.DeleteFileOnly(cacheRoot, replacement, false).Allowed, Is.False);
            Assert.That(mutation, Is.False);
            Assert.That(File.ReadAllText(save), Is.EqualTo("keep"));
        }

        [Test]
        public void Executor_RefusesRecursiveDeleteWhenDescendantIsJunction()
        {
            var original = Path.Combine(testRoot, "original");
            Directory.CreateDirectory(original);
            var precious = Path.Combine(original, "keep.txt");
            File.WriteAllText(precious, "keep");
            var cached = Path.Combine(cacheRoot, "cached");
            Directory.CreateDirectory(cached);
            File.WriteAllText(Path.Combine(cached, "ordinary.txt"), "data");
            var junction = Path.Combine(cached, "linked");
            if (!TryCreateJunction(junction, original))
                Assert.Ignore("Junction creation is unavailable on this Windows host.");

            try
            {
                var result = new SafeCacheDeletionExecutor().Delete(cacheRoot, cached, false);
                Assert.That(result.Allowed, Is.False);
                Assert.That(result.Reason, Does.Contain("linked"));
                Assert.That(Directory.Exists(cached), Is.True);
                Assert.That(File.ReadAllText(precious), Is.EqualTo("keep"));
            }
            finally
            {
                if (Directory.Exists(junction)) Directory.Delete(junction, false);
            }
        }

        [Test]
        public void Executor_BoundedDescendantInspectionRefusesOversizedTree()
        {
            var cached = Path.Combine(cacheRoot, "huge");
            Directory.CreateDirectory(cached);
            File.WriteAllText(Path.Combine(cached, "one"), "1");
            File.WriteAllText(Path.Combine(cached, "two"), "2");
            var scan = typeof(SafeCacheDeletionExecutor).GetMethod(
                "GetUnsafeDescendantReason",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.That(scan, Is.Not.Null);
            var result = (string)scan.Invoke(null, new object[] { cached, 1 });
            Assert.That(result, Does.Contain("too large"));
            Assert.That(Directory.Exists(cached), Is.True);
        }

        [Test]
        public void Executor_RelativeTarget_IsRefusedWithoutDeletingAnything()
        {
            var mutationAttempted = false;
            var executor = new SafeCacheDeletionExecutor(
                new SafeCacheDeletionPolicy(),
                ignored => mutationAttempted = true,
                ignored => mutationAttempted = true);

            var result = executor.Delete(string.Empty, ".", true);

            Assert.That(result.Allowed, Is.False);
            Assert.That(result.Reason, Does.Contain("fully rooted"));
            Assert.That(mutationAttempted, Is.False);
        }

        private static bool TryCreateJunction(string junction, string target)
        {
            var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + junction + "\" \"" + target + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            });
            process.WaitForExit();
            return process.ExitCode == 0 && Directory.Exists(junction);
        }
    }
}
