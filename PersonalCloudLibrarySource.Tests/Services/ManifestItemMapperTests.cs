using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class ManifestItemMapperTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-ManifestMapperTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Map_NestedDirectoryDestination_RejectsRegardlessOfManifestOrder(bool parentFirst)
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                LocalCacheFolder = Path.Combine(testRoot, "cache")
            };
            var parent = new PersonalCloudLibraryItem
            {
                Id = "parent", Title = "Parent", SourceType = "directory",
                InstallDirectory = "shared"
            };
            var child = new PersonalCloudLibraryItem
            {
                Id = "child", Title = "Child", SourceType = "file",
                CachePath = Path.Combine("shared", "game.exe")
            };
            var items = parentFirst ? new[] { parent, child } : new[] { child, parent };
            Assert.That(() => new ManifestItemMapper().Map(items, settings, new List<string>()),
                Throws.InvalidOperationException.With.Message.Contains("overlapping cache destinations"));
        }

        [TestCase(true, "file")]
        [TestCase(false, "file")]
        [TestCase(true, "directory")]
        [TestCase(false, "directory")]
        public void Map_FileAncestorDestinationRejectsEveryOrderAndChildType(bool parentFirst, string childType)
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                LocalCacheFolder = Path.Combine(testRoot, "cache")
            };
            var parent = new PersonalCloudLibraryItem
            {
                Id = "parent", Title = "Parent", SourceType = "file",
                CachePath = "shared"
            };
            var child = new PersonalCloudLibraryItem
            {
                Id = "child", Title = "Child", SourceType = childType,
                CachePath = Path.Combine("SHARED", "game.exe"),
                InstallDirectory = Path.Combine("SHARED", "package")
            };
            var items = parentFirst ? new[] { parent, child } : new[] { child, parent };
            Assert.That(() => new ManifestItemMapper().Map(items, settings, new List<string>()),
                Throws.InvalidOperationException.With.Message.Contains("overlapping cache destinations"));
            Assert.That(Directory.Exists(settings.LocalCacheFolder), Is.False);
        }

        [Test]
        public void Map_SiblingDestinations_AreAllowed()
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                LocalCacheFolder = Path.Combine(testRoot, "cache")
            };
            var items = new[]
            {
                new PersonalCloudLibraryItem
                {
                    Id = "first", Title = "First", SourceType = "directory",
                    InstallDirectory = "shared"
                },
                new PersonalCloudLibraryItem
                {
                    Id = "second", Title = "Second", SourceType = "file",
                    CachePath = "shared-extra/game.exe"
                }
            };
            Assert.That(new ManifestItemMapper().Map(items, settings, new List<string>()), Has.Count.EqualTo(2));
        }

        [TestCase(1000)]
        [TestCase(10000)]
        public void ParseAndMap_LargeLocalFixtureRecordsRepeatableTimingAndManagedMemory(int count)
        {
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                LocalCacheFolder = Path.Combine(testRoot, "cache"),
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneRemoteName = "fixture",
                TreatMissingFilesAsUninstalled = true,
                AllowDownloads = true
            };
            var builder = new System.Text.StringBuilder("{\"version\":3,\"items\":[");
            for (var i = 0; i < count; i++)
            {
                if (i != 0) builder.Append(',');
                builder.Append("{\"id\":\"g").Append(i).Append("\",\"title\":\"Fixture ")
                    .Append(i).Append("\",\"sourcePath\":\"g").Append(i)
                    .Append(".exe\",\"cachePath\":\"g").Append(i).Append(".exe\"}");
            }
            var json = builder.Append("]}").ToString();
            var parser = new ManifestParserValidator();
            var mapper = new ManifestItemMapper();
            // Warm up the managed parser and mapper separately from the samples.
            var warm = parser.Parse("{\"version\":3,\"items\":[{\"id\":\"warm\",\"title\":\"Warm\",\"cachePath\":\"warm.exe\"}]}");
            Assert.That(warm.Succeeded, Is.True);
            mapper.Map(warm.ValidItems, settings, null);

            for (var sample = 1; sample <= 3; sample++)
                MeasureCatalogSample(parser, mapper, settings, json, count, sample);
            Assert.That(Directory.Exists(settings.LocalCacheFolder), Is.False);
        }

        // Keep each sample's parsed and mapped objects within its own stack
        // frame so the next baseline does not retain the previous workload.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void MeasureCatalogSample(ManifestParserValidator parser,
            ManifestItemMapper mapper, PersonalCloudLibrarySourceSettings settings,
            string json, int count, int sample)
        {
            var before = GC.GetTotalMemory(true);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var parsed = parser.Parse(json);
            var parseMs = timer.Elapsed.TotalMilliseconds;
            Assert.That(parsed.Succeeded, Is.True, parsed.Error);
            Assert.That(parsed.Issues, Is.Empty);
            timer.Restart();
            var games = mapper.Map(parsed.ValidItems, settings, null);
            var mapMs = timer.Elapsed.TotalMilliseconds;
            Assert.That(games.Count, Is.EqualTo(count));
            Assert.That(games[0].GameId, Is.EqualTo("g0"));
            Assert.That(games[count - 1].GameId, Is.EqualTo("g" + (count - 1)));
            Assert.That(games.TrueForAll(game => !game.IsInstalled), Is.True);
            var retained = GC.GetTotalMemory(true) - before;
            GC.KeepAlive(parsed);
            GC.KeepAlive(games);
            Console.WriteLine("PCLS_BENCHMARK catalog items={0} sample={1} json_chars={2} parse_ms={3:F3} map_ms={4:F3} retained_managed_bytes_delta={5}",
                count, sample, json.Length, parseMs, mapMs, retained);
        }

        [Test]
        public void Map_RemoteOnlyItem_IsUninstalledWithoutPlayAction()
        {
            var item = new PersonalCloudLibraryItem { Id = "one", Title = "One", SourcePath = "one.exe", CachePath = "one.exe" };
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneRemoteName = "games",
                LocalCacheFolder = Path.Combine(testRoot, "cache"),
                TreatMissingFilesAsUninstalled = true,
                AllowDownloads = true
            };

            var games = new ManifestItemMapper().Map(new[] { item }, settings, new List<string>());

            Assert.That(games.Count, Is.EqualTo(1));
            Assert.That(games[0].IsInstalled, Is.False);
            Assert.That(games[0].GameActions, Is.Null.Or.Empty);
        }

        [Test]
        public void Map_CachedDirectoryWithoutLaunchFile_IsInstalledButNotPlayable()
        {
            var directory = Path.Combine(testRoot, "cache", "one");
            Directory.CreateDirectory(directory);
            var item = new PersonalCloudLibraryItem
            {
                Id = "one",
                Title = "One",
                SourceType = "directory",
                InstallDirectory = "one",
                LaunchFile = "missing.exe"
            };
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                LocalCacheFolder = Path.Combine(testRoot, "cache"),
                TreatMissingFilesAsUninstalled = true
            };

            var games = new ManifestItemMapper().Map(new[] { item }, settings, new List<string>());

            Assert.That(games[0].IsInstalled, Is.True);
            Assert.That(games[0].GameActions, Is.Null.Or.Empty);
        }

        [Test]
        public void Map_RemoteDirectory_DoesNotTreatExistingCacheRootAsItsInstall()
        {
            var cacheRoot = Path.Combine(testRoot, "cache");
            Directory.CreateDirectory(cacheRoot);
            var item = new PersonalCloudLibraryItem
            {
                Id = "remote-directory",
                Title = "Remote Directory",
                SourceType = "directory",
                SourcePath = "remote-directory"
            };
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                SourceProviderType = PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType,
                RcloneRemoteName = "games",
                LocalCacheFolder = cacheRoot,
                TreatMissingFilesAsUninstalled = true
            };

            var games = new ManifestItemMapper().Map(new[] { item }, settings, new List<string>());

            Assert.That(games[0].IsInstalled, Is.False);
            Assert.That(games[0].InstallDirectory, Is.EqualTo(Path.Combine(cacheRoot, item.Id)));
        }

        [Test]
        public void Map_SharedCacheRoot_CachedFileSizesAreOwnBytesOnly()
        {
            var cache = Path.Combine(testRoot, "cache");
            Directory.CreateDirectory(cache);
            File.WriteAllText(Path.Combine(cache, "one.exe"), "one");
            File.WriteAllText(Path.Combine(cache, "two.exe"), "twotwo");
            File.WriteAllBytes(Path.Combine(cache, "unrelated.bin"), new byte[512 * 1024]);

            var items = new[]
            {
                new PersonalCloudLibraryItem { Id = "one", Title = "One", SourceType = "file", CachePath = "one.exe" },
                new PersonalCloudLibraryItem { Id = "two", Title = "Two", SourceType = "file", CachePath = "two.exe" }
            };
            var settings = new PersonalCloudLibrarySourceSettingsV3
            {
                LocalCacheFolder = cache,
                TreatMissingFilesAsUninstalled = true
            };
            var mapped = new ManifestItemMapper().Map(items, settings, new List<string>());

            Assert.That(mapped, Has.Count.EqualTo(2));
            Assert.That(mapped[0].IsInstalled, Is.True);
            Assert.That(mapped[1].IsInstalled, Is.True);
            Assert.That(mapped[0].InstallDirectory, Is.Empty);
            Assert.That(mapped[1].InstallDirectory, Is.Empty);
            Assert.That(mapped[0].InstallSize, Is.EqualTo((ulong)3));
            Assert.That(mapped[1].InstallSize, Is.EqualTo((ulong)6));
            Assert.That(mapped[0].GameActions[0].WorkingDir, Is.EqualTo(cache));
            Assert.That(mapped[1].GameActions[0].WorkingDir, Is.EqualTo(cache));
        }

        [Test]
        public void Map_ExplicitFileInstallDirectory_PreservesPath()
        {
            var cache = Path.Combine(testRoot, "cache");
            var install = Path.Combine(cache, "explicit");
            Directory.CreateDirectory(install);
            File.WriteAllText(Path.Combine(install, "one.exe"), "one");
            var item = new PersonalCloudLibraryItem
            {
                Id = "one", Title = "One", SourceType = "file",
                InstallDirectory = "explicit", CachePath = "explicit/one.exe"
            };
            var mapped = new ManifestItemMapper().Map(new[] { item },
                new PersonalCloudLibrarySourceSettingsV3 { LocalCacheFolder = cache },
                new List<string>())[0];

            Assert.That(mapped.InstallDirectory, Is.EqualTo(install));
            Assert.That(mapped.InstallSize, Is.Null);
            Assert.That(mapped.GameActions[0].WorkingDir, Is.EqualTo(install));
        }

        [Test]
        public void Map_CachedFile_ExposesStandardPlayPathAndWorkingDirectory()
        {
            var cache = Path.Combine(testRoot, "cache");
            var launch = Path.Combine(cache, "one", "play.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(launch));
            File.WriteAllText(launch, "stub");
            var item = new PersonalCloudLibraryItem { Id = "one", Title = "One", CachePath = "one/play.exe" };
            var settings = new PersonalCloudLibrarySourceSettingsV3 { LocalCacheFolder = cache, TreatMissingFilesAsUninstalled = true };

            var game = new ManifestItemMapper().Map(new[] { item }, settings, new List<string>())[0];

            Assert.That(game.IsInstalled, Is.True);
            Assert.That(game.GameActions, Has.Count.EqualTo(1));
            Assert.That(game.GameActions[0].IsPlayAction, Is.True);
            Assert.That(game.GameActions[0].Path, Is.EqualTo(launch));
            Assert.That(game.GameActions[0].WorkingDir, Is.EqualTo(Path.GetDirectoryName(launch)));
        }
    }
}
