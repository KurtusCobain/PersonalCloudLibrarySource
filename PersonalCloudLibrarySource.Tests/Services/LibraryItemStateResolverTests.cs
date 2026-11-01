using NUnit.Framework;
using Playnite.SDK.Models;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class LibraryItemStateResolverTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(Path.GetTempPath(), "PCLS-StateResolverTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }

        [Test]
        public void Resolve_FileItem_RequiresLaunchFileAndProvidesPlayAction()
        {
            var launch = Path.Combine(testRoot, "game.exe");
            File.WriteAllText(launch, "game");

            var state = new LibraryItemStateResolver().Resolve(new PersonalCloudLibraryItem { SourceType = "file" }, launch, testRoot);

            Assert.That(state.IsCached, Is.True);
            Assert.That(state.HasPlayAction, Is.True);
            Assert.That(state.PlayPath, Is.EqualTo(launch));
        }

        [Test]
        public void Resolve_DirectoryItem_InstalledWithoutLaunchFileButNotPlayable()
        {
            var directory = Path.Combine(testRoot, "directory-game");
            Directory.CreateDirectory(directory);

            var state = new LibraryItemStateResolver().Resolve(new PersonalCloudLibraryItem { SourceType = "directory" }, Path.Combine(directory, "missing.exe"), directory);

            Assert.That(state.IsCached, Is.True);
            Assert.That(state.HasPlayAction, Is.False);
            Assert.That(state.WorkingDirectory, Is.EqualTo(directory));
        }

        [Test]
        public void Resolve_MissingFileAndDirectory_IsRemoteOnly()
        {
            var state = new LibraryItemStateResolver().Resolve(new PersonalCloudLibraryItem { SourceType = "directory" }, Path.Combine(testRoot, "missing.exe"), Path.Combine(testRoot, "missing"));

            Assert.That(state.IsCached, Is.False);
            Assert.That(state.HasPlayAction, Is.False);
        }

        [TestCase(false, false, false, true)]
        [TestCase(false, false, true, false)]
        [TestCase(false, true, false, true)]
        [TestCase(false, true, true, false)]
        [TestCase(true, false, false, true)]
        [TestCase(true, false, true, true)]
        [TestCase(true, true, false, true)]
        [TestCase(true, true, true, true)]
        public void Resolve_InstalledStateMatrix(
            bool contentExists,
            bool directoryItem,
            bool treatMissingAsUninstalled,
            bool expectedInstalled)
        {
            var install = Path.Combine(testRoot, "matrix-install");
            var launch = Path.Combine(install, "play.exe");
            if (contentExists)
            {
                Directory.CreateDirectory(install);
                if (!directoryItem) File.WriteAllText(launch, "game");
            }
            var item = new PersonalCloudLibraryItem { SourceType = directoryItem ? "directory" : "file" };

            var state = new LibraryItemStateResolver().Resolve(item, launch, install, treatMissingAsUninstalled);

            Assert.That(state.IsInstalled, Is.EqualTo(expectedInstalled));
            Assert.That(state.IsCached, Is.EqualTo(contentExists));
        }

        [Test]
        public void StateApplicator_AppliesAndClearsPlayniteMetadata()
        {
            var game = new Game("Example")
            {
                GameActions = new System.Collections.ObjectModel.ObservableCollection<GameAction>
                {
                    new GameAction { Name = "Manual", Type = GameActionType.URL, Path = "https://example.invalid", IsPlayAction = false }
                }
            };
            var item = new PersonalCloudLibraryItem { SourceType = "file" };
            var install = Path.Combine(testRoot, "apply");
            var launch = Path.Combine(install, "play.exe");
            Directory.CreateDirectory(install);
            File.WriteAllText(launch, "game");
            var state = new LibraryItemStateResolver().Resolve(item, launch, install, true);
            var applicator = new LibraryItemStateApplicator();

            applicator.Apply(game, state);
            Assert.That(game.IsInstalled, Is.True);
            Assert.That(game.InstallDirectory, Is.Empty,
                "Playnite must not recursively count the shared parent folder for an implicit file entry.");
            Assert.That(game.InstallSize, Is.EqualTo((ulong)4));
            Assert.That(game.GameActions, Has.Count.EqualTo(2));
            Assert.That(game.GameActions, Has.Exactly(1).Matches<GameAction>(action => action.IsPlayAction));
            Assert.That(game.GameActions[1].WorkingDir, Is.EqualTo(install),
                "Launch action still uses the real file parent directory.");

            applicator.ApplyUninstalled(game);
            Assert.That(game.IsInstalled, Is.False);
            Assert.That(game.InstallDirectory, Is.Null.Or.Empty);
            Assert.That(game.InstallSize, Is.Null);
            Assert.That(game.GameActions, Has.Count.EqualTo(1));
            Assert.That(game.GameActions[0].Name, Is.EqualTo("Manual"));
        }

        [Test]
        public void Resolve_ExplicitFileInstallDirectory_RetainsPlayniteDirectory()
        {
            var install = Path.Combine(testRoot, "explicit");
            var launch = Path.Combine(install, "one.exe");
            Directory.CreateDirectory(install);
            File.WriteAllText(launch, "file");
            var item = new PersonalCloudLibraryItem
            {
                SourceType = "file", InstallDirectory = "explicit"
            };

            var state = new LibraryItemStateResolver().Resolve(item, launch, install);

            Assert.That(state.WorkingDirectory, Is.EqualTo(install));
            Assert.That(state.PlayniteInstallDirectory, Is.EqualTo(install),
                "Do not discard a manifest author's explicit game directory.");
            Assert.That(state.OwnedFileSize, Is.Null,
                "Explicit directory footprints remain under Playnite's normal directory scanner.");
        }

        [Test]
        public void Resolve_ImplicitFileSize_DoesNotCountSharedCacheSiblings()
        {
            var first = Path.Combine(testRoot, "first.exe");
            var second = Path.Combine(testRoot, "second.exe");
            var unrelated = Path.Combine(testRoot, "unrelated.bin");
            File.WriteAllText(first, "one");
            File.WriteAllText(second, "twotwo");
            File.WriteAllBytes(unrelated, new byte[512 * 1024]);
            var item = new PersonalCloudLibraryItem { SourceType = "file" };

            var firstState = new LibraryItemStateResolver().Resolve(item, first, testRoot);
            var secondState = new LibraryItemStateResolver().Resolve(item, second, testRoot);

            Assert.That(firstState.PlayniteInstallDirectory, Is.Empty);
            Assert.That(secondState.PlayniteInstallDirectory, Is.Empty);
            Assert.That(firstState.OwnedFileSize, Is.EqualTo((ulong)3));
            Assert.That(secondState.OwnedFileSize, Is.EqualTo((ulong)6));
            Assert.That(firstState.WorkingDirectory, Is.EqualTo(testRoot));
            Assert.That(secondState.WorkingDirectory, Is.EqualTo(testRoot));
            Assert.That(File.Exists(unrelated), Is.True);
        }

        [Test]
        public void Reconcile_DirectoryWithLaunchRemoved_RemainsCachedAndInstalled()
        {
            var install = Path.Combine(testRoot, "directory-remains");
            Directory.CreateDirectory(install);
            var game = new Game("Directory");
            var state = new LibraryItemStateApplicator().Reconcile(
                game,
                new PersonalCloudLibraryItem { SourceType = "directory" },
                Path.Combine(install, "missing.exe"),
                install,
                true);

            Assert.That(state.IsCached, Is.True);
            Assert.That(game.IsInstalled, Is.True);
            Assert.That(game.InstallDirectory, Is.EqualTo(install));
            Assert.That(game.GameActions, Is.Empty);
        }

        [Test]
        public void Reconcile_FileWithLaunchRemoved_ClearsCachedMetadata()
        {
            var install = Path.Combine(testRoot, "file-parent-remains");
            Directory.CreateDirectory(install);
            var game = new Game("File") { IsInstalled = true, InstallDirectory = install };
            var state = new LibraryItemStateApplicator().Reconcile(
                game,
                new PersonalCloudLibraryItem { SourceType = "file" },
                Path.Combine(install, "missing.exe"),
                install,
                true);

            Assert.That(state.IsCached, Is.False);
            Assert.That(game.IsInstalled, Is.False);
            Assert.That(game.InstallDirectory, Is.Empty);
            Assert.That(game.GameActions, Is.Empty);
        }
    }
}
