using NUnit.Framework;
using System;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public sealed class GameLauncherCandidatePolicyTests
    {
        [Test]
        public void RanksMatchingGameExecutableAboveInstallerAndRedist()
        {
            var preview = new GameLauncherCandidatePolicy().Preview(new[]
            {
                "Setup.exe", "redist/vcredist_x64.exe", "My Game.exe",
                "tools/CrashReporter.exe", "readme.txt"
            }, "My Game");

            Assert.That(preview.SuggestedRelativePath, Is.EqualTo("My Game.exe"));
            Assert.That(preview.Candidates[0].Kind,
                Is.EqualTo(LauncherCandidateKind.LikelyGame));
            Assert.That(preview.Candidates.Single(x => x.RelativePath == "Setup.exe").Kind,
                Is.EqualTo(LauncherCandidateKind.InstallerOrHelper));
            Assert.That(preview.Candidates.Any(x => x.RelativePath == "readme.txt"), Is.False);
            Assert.That(preview.RequiresUserConfirmation, Is.True);
            Assert.That(preview.CanExecuteUnattended, Is.False);
        }

        [Test]
        public void TwoPlausibleLaunchersRemainAmbiguousInsteadOfSelectingOne()
        {
            var preview = new GameLauncherCandidatePolicy().Preview(new[]
            {
                "MyGame_DX11.exe", "MyGame_Vulkan.exe"
            }, "MyGame");

            Assert.That(preview.Candidates, Has.Count.EqualTo(2));
            Assert.That(preview.SuggestedRelativePath, Is.Empty);
            Assert.That(preview.Candidates.All(x => x.Kind == LauncherCandidateKind.LikelyGame),
                Is.True);
        }

        [Test]
        public void AStandaloneHelperNeverBecomesRecommendedLauncher()
        {
            var preview = new GameLauncherCandidatePolicy().Preview(new[]
            {
                "setup.exe", "unins000.exe", "dxsetup.exe"
            }, "Game");

            Assert.That(preview.Candidates, Has.Count.EqualTo(3));
            Assert.That(preview.SuggestedRelativePath, Is.Empty);
            Assert.That(preview.Candidates.All(
                x => x.Kind == LauncherCandidateKind.InstallerOrHelper), Is.True);
        }

        [Test]
        public void UnsafePathAndDuplicateCandidateAreIgnored()
        {
            var preview = new GameLauncherCandidatePolicy().Preview(new[]
            {
                "../escape.exe", "C:/Windows/system.exe",
                "Game.exe", "game.EXE", "game/CON.exe"
            }, "Game");

            Assert.That(preview.Candidates, Has.Count.EqualTo(1));
            Assert.That(preview.Candidates[0].RelativePath, Is.EqualTo("Game.exe"));
            Assert.That(preview.CanExecuteUnattended, Is.False);
        }

        [Test]
        public void ScriptsAndUnknownTitlesAreNotAutomaticallyRecommended()
        {
            var preview = new GameLauncherCandidatePolicy().Preview(new[]
            {
                "run.cmd", "start.bat", "readme.md"
            }, "Unknown Title");

            Assert.That(preview.SuggestedRelativePath, Is.Empty);
            Assert.That(preview.Candidates, Has.Count.EqualTo(2));
            Assert.That(preview.RequiresUserConfirmation, Is.True);
        }

        [Test]
        public void EmptyInputsAndMissingExecutableReturnNoGuess()
        {
            var policy = new GameLauncherCandidatePolicy();
            Assert.That(policy.Preview(null, "Game").Candidates, Is.Empty);
            Assert.That(policy.Preview(new[] { "data.bin", "folder/game.sav" }, "Game")
                .SuggestedRelativePath, Is.Empty);
        }
    }
}
