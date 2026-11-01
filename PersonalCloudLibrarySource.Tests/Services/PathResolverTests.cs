using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class PathResolverTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-PathResolverTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot))
            {
                Directory.Delete(testRoot, true);
            }
        }

        [Test]
        public void SourceResolver_ValidRelativePath_StaysUnderLibraryRoot()
        {
            var settings = LocalSettings();
            var result = new SourcePathResolver().ResolveLocal(settings, Path.Combine("games", "one.exe"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Path, Is.EqualTo(Path.Combine(testRoot, "source", "games", "one.exe")));
        }

        [TestCase("..\\outside.exe")]
        [TestCase("games\\..\\..\\outside.exe")]
        public void SourceResolver_Traversal_IsRejected(string sourcePath)
        {
            var result = new SourcePathResolver().ResolveLocal(LocalSettings(), sourcePath);

            Assert.That(result.Succeeded, Is.False);
        }

        [Test]
        public void SourceResolver_RootedManifestPath_IsRejected()
        {
            var result = new SourcePathResolver().ResolveLocal(LocalSettings(), Path.Combine(testRoot, "outside.exe"));

            Assert.That(result.Succeeded, Is.False);
        }

        [Test]
        public void SourceResolver_LocalFileProvider_AllowsDocumentedAbsoluteSourcePath()
        {
            var path = Path.Combine(testRoot, "absolute.exe");
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFileProviderType,
                LocalManifestPath = Path.Combine(testRoot, "manifest.json")
            };

            var result = new SourcePathResolver().ResolveLocal(settings, path);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Path, Is.EqualTo(path));
        }

        [Test]
        public void CacheResolver_SiblingPrefix_IsNotInsideCache()
        {
            var cache = Path.Combine(testRoot, "cache");
            var sibling = Path.Combine(testRoot, "cache-evil", "game.exe");

            Assert.That(PathBoundary.IsContained(cache, sibling), Is.False);
        }

        [Test]
        public void CacheResolver_RelativeLaunchPath_StaysUnderCache()
        {
            var item = new PersonalCloudLibraryItem { CachePath = Path.Combine("game", "play.exe") };
            var settings = new PersonalCloudLibrarySourceSettingsV3 { LocalCacheFolder = Path.Combine(testRoot, "cache") };

            var result = new CachePathResolver().Resolve(item, settings);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.LaunchPath, Is.EqualTo(Path.Combine(testRoot, "cache", "game", "play.exe")));
        }

        [Test]
        public void CacheResolver_RelativeTraversal_IsRejected()
        {
            var item = new PersonalCloudLibraryItem { CachePath = Path.Combine("..", "outside", "play.exe") };
            var settings = new PersonalCloudLibrarySourceSettingsV3 { LocalCacheFolder = Path.Combine(testRoot, "cache") };

            var result = new CachePathResolver().Resolve(item, settings);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("escapes"));
        }

        [TestCase("../outside")]
        [TestCase("../../outside")]
        [TestCase(@"..\outside")]
        [TestCase(@"C:\outside")]
        [TestCase("C:evil")]
        [TestCase("CON")]
        [TestCase("NUL")]
        [TestCase("game/child")]
        [TestCase(@"game\child")]
        public void CacheResolver_UnsafeFallbackId_RejectsBeforeInstall(string id)
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                LocalCacheFolder = Path.Combine(testRoot, "cache")
            };
            var item = new PersonalCloudLibraryItem { Id = id, SourcePath = "file.exe" };

            var result = new CachePathResolver().Resolve(item, settings);

            Assert.That(result.Succeeded, Is.False, id);
            Assert.That(result.Error, Is.Not.Empty);
        }

        [Test]
        public void CacheResolver_NormalFallbackId_StaysInsideCache()
        {
            var root = Path.Combine(testRoot, "cache");
            var result = new CachePathResolver().Resolve(
                new PersonalCloudLibraryItem { Id = "game-safe", SourcePath = "game.exe" },
                new PersonalCloudLibrarySourceSettingsV3 { LocalCacheFolder = root });

            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(PathBoundary.IsContained(root, result.DestinationFile), Is.True);
            Assert.That(PathBoundary.IsContained(root, result.DestinationDirectory), Is.True);
        }

        [Test]
        public void CacheResolver_RootedCachePathOutsideRoot_FailsWithoutAnyResolvedPaths()
        {
            var item = new PersonalCloudLibraryItem
            {
                Id = "external", CachePath = Path.Combine(testRoot, "unrelated", "play.exe")
            };
            var result = new CachePathResolver().Resolve(item,
                new PersonalCloudLibrarySourceSettingsV3 { LocalCacheFolder = Path.Combine(testRoot, "cache") });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("escapes"));
            Assert.That(result.LaunchPath, Is.Empty);
            Assert.That(result.InstallDirectory, Is.Empty);
            Assert.That(result.DestinationFile, Is.Empty);
            Assert.That(result.DestinationDirectory, Is.Empty);
        }

        [Test]
        public void CacheResolver_RootedInstallDirectoryOutsideRoot_FailsClosed()
        {
            var item = new PersonalCloudLibraryItem
            {
                Id = "external", InstallDirectory = Path.Combine(testRoot, "unrelated"),
                LaunchFile = "play.exe"
            };
            var result = new CachePathResolver().Resolve(item,
                new PersonalCloudLibrarySourceSettingsV3 { LocalCacheFolder = Path.Combine(testRoot, "cache") });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("escapes"));
            Assert.That(result.LaunchPath, Is.Empty);
        }

        [Test]
        public void CacheResolver_LegacyAbsolutePathInsideCache_RemainsSupported()
        {
            var cache = Path.Combine(testRoot, "cache");
            var cached = Path.Combine(cache, "existing", "play.exe");
            var result = new CachePathResolver().Resolve(
                new PersonalCloudLibraryItem { Id = "legacy", CachePath = cached },
                new PersonalCloudLibrarySourceSettingsV3 { LocalCacheFolder = cache });
            Assert.That(result.Succeeded, Is.True, result.Error);
            Assert.That(result.LaunchPath, Is.EqualTo(cached));
        }

        [Test]
        public void CacheResolver_RootedLaunchFile_IsRejected()
        {
            var item = new PersonalCloudLibraryItem
            {
                InstallDirectory = "game",
                LaunchFile = Path.Combine(testRoot, "outside.exe")
            };
            var settings = new PersonalCloudLibrarySourceSettingsV3 { LocalCacheFolder = Path.Combine(testRoot, "cache") };

            var result = new CachePathResolver().Resolve(item, settings);

            Assert.That(result.Succeeded, Is.False);
        }

        [Test]
        public void CacheResolver_RelativeCandidateWithoutCacheRoot_FailsClosed()
        {
            var result = new CachePathResolver().Resolve(
                new PersonalCloudLibraryItem { Id = "game", CachePath = Path.Combine("game", "play.exe") },
                new PersonalCloudLibrarySourceSettingsV3 { LocalCacheFolder = string.Empty });

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("LocalCacheFolder"));
        }

        private PersonalCloudLibrarySourceSettingsV3 LocalSettings()
        {
            return new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.LocalFolderProviderType,
                LocalLibraryRoot = Path.Combine(testRoot, "source")
            };
        }
    }
}
