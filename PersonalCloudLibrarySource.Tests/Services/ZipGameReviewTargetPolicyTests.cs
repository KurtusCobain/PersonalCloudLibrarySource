using NUnit.Framework;
using Playnite.SDK.Models;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class ZipGameReviewTargetPolicyTests
    {
        private string root;
        private string cache;
        private string source;
        private string cached;
        private Guid pluginId;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "PCLS-ZipGameMenu",
                Guid.NewGuid().ToString("N"));
            cache = Path.Combine(root, "cache");
            Directory.CreateDirectory(cache);
            source = Path.Combine(root, "sample.zip");
            cached = Path.Combine(cache, "game.zip");
            File.WriteAllText(source, "disposable dummy bytes");
            pluginId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        private PersonalCloudLibrarySourceSettings Settings(string provider = "LocalFile")
            => new PersonalCloudLibrarySourceSettings
            {
                Enabled = true,
                SourceProviderType = provider,
                LocalCacheFolder = cache
            };

        private GameCommandTarget Target(string sourcePath = null, string cachedPath = null)
            => new GameCommandTarget
            {
                Game = new Game("Fixture")
                {
                    Id = Guid.NewGuid(),
                    PluginId = pluginId,
                    GameId = "fixture"
                },
                Item = new PersonalCloudLibraryItem
                {
                    Id = "fixture",
                    Title = "Fixture",
                    SourceType = "file",
                    SourcePath = sourcePath ?? source
                },
                ResolvedLocalSourcePath = sourcePath ?? source,
                CacheDisplayPath = cachedPath ?? string.Empty
            };

        [Test]
        public void LocalZipSourceIsReviewableWithoutCopyingIt()
        {
            var resolved = new ZipGameReviewTargetPolicy().Resolve(
                Target(), Settings(), pluginId);
            Assert.That(resolved.Available, Is.True);
            Assert.That(resolved.ArchivePath, Is.EqualTo(source));
            Assert.That(resolved.FromLocalCache, Is.False);
            Assert.That(File.ReadAllText(source), Is.EqualTo("disposable dummy bytes"));
            Assert.That(Directory.GetFiles(cache), Is.Empty);
        }

        [Test]
        public void CachedZipPreferredAndAllowedWhenRemoteIsOffline()
        {
            File.WriteAllText(cached, "disposable cached ZIP");
            var target = Target();
            target.CacheDisplayPath = cached;
            var resolved = new ZipGameReviewTargetPolicy().Resolve(
                target, Settings("RcloneRemote"), pluginId);
            Assert.That(resolved.Available, Is.True);
            Assert.That(resolved.ArchivePath, Is.EqualTo(cached));
            Assert.That(resolved.FromLocalCache, Is.True);
        }

        [Test]
        public void RemoteZipWithoutCachedCopyIsNotFetchedForReview()
        {
            var resolved = new ZipGameReviewTargetPolicy().Resolve(
                Target(), Settings("RcloneRemote"), pluginId);
            Assert.That(resolved.Available, Is.False);
            Assert.That(Directory.GetFiles(cache), Is.Empty);
        }

        [Test]
        public void CacheFileOutsideConfiguredRootCannotMasqueradeAsOwnedCopy()
        {
            var target = Target();
            target.CacheDisplayPath = source;
            var resolved = new ZipGameReviewTargetPolicy().Resolve(
                target, Settings("RcloneRemote"), pluginId);
            Assert.That(resolved.Available, Is.False);
        }

        [Test]
        public void WrongPluginOrManifestItemIdentityIsRejected()
        {
            var target = Target();
            target.Game.PluginId = Guid.NewGuid();
            var policy = new ZipGameReviewTargetPolicy();
            Assert.That(policy.Resolve(target, Settings(), pluginId).Available, Is.False);
            target.Game.PluginId = pluginId;
            target.Item.Id = "different";
            Assert.That(policy.Resolve(target, Settings(), pluginId).Available, Is.False);
        }

        [Test]
        public void NonZipLocalSourceNeverShowsPackageReview()
        {
            var exe = Path.Combine(root, "sample.exe");
            File.WriteAllText(exe, "fixture executable");
            Assert.That(new ZipGameReviewTargetPolicy().Resolve(
                Target(exe), Settings(), pluginId).Available, Is.False);
        }

        [Test]
        public void DisabledSourceAndDirectoryPackageCannotShowReview()
        {
            var p = new ZipGameReviewTargetPolicy();
            var settings = Settings();
            settings.Enabled = false;
            Assert.That(p.Resolve(Target(), settings, pluginId).Available, Is.False);
            settings.Enabled = true;
            var target = Target();
            target.Item.SourceType = "directory";
            Assert.That(p.Resolve(target, settings, pluginId).Available, Is.False);
        }

        [Test]
        public void RemovedLocalArchiveCannotBeReviewedUsingStaleMenuTarget()
        {
            var policy = new ZipGameReviewTargetPolicy();
            var target = Target();
            Assert.That(policy.Resolve(target, Settings(), pluginId).Available, Is.True);
            File.Delete(source);
            Assert.That(policy.Resolve(target, Settings(), pluginId).Available, Is.False);
        }

        [Test]
        public void UnownedCacheFileIsNeverDeletedByResolvingReviewEligibility()
        {
            File.WriteAllText(cached, "user save content");
            var result = new ZipGameReviewTargetPolicy().Resolve(
                Target(cachedPath: cached), Settings(), pluginId);
            Assert.That(result.Available, Is.True);
            Assert.That(File.ReadAllText(cached), Is.EqualTo("user save content"));
        }
    }
}
