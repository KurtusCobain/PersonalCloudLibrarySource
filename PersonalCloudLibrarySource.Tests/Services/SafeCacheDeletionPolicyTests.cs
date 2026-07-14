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
