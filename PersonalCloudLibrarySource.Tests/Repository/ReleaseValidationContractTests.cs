using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class ReleaseValidationContractTests
    {
        [Test]
        public void FocusedReleaseValidatorContractsPass()
        {
            var result = RunPowerShell(".\\tools\\test-release-validation.ps1");
            Assert.That(result.ExitCode, Is.EqualTo(0), result.Output);
            var summary = Regex.Match(result.Output, @"Release validator tests: (\d+) passed, (\d+) failed\.");
            Assert.That(summary.Success, Is.True, result.Output);
            Assert.That(int.Parse(summary.Groups[1].Value), Is.GreaterThanOrEqualTo(7), result.Output);
            Assert.That(int.Parse(summary.Groups[2].Value), Is.Zero, result.Output);
            Console.WriteLine(summary.Value);
        }

        [Test]
        public void InternalCandidateHasNoOneTimePublicationWorkflowAndUsesPinnedToolbox()
        {
            var root = Root();
            Assert.That(File.Exists(Path.Combine(root, ".github", "workflows", "publish-v032.yml")), Is.False,
                "The retired public-release workflow must not accompany the internal candidate.");
            var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "build.yml"));
            StringAssert.Contains("9AF9A0DB330C97879697BEAC9F819C16F9F544E712261A83FA4E62A8F9A2C787", workflow);
            StringAssert.Contains("Pinned official Playnite archive identity mismatch.", workflow);
            StringAssert.Contains("validate-release.ps1 -Candidate -ToolboxPath", workflow);
            StringAssert.DoesNotContain("contents: write", workflow);
            StringAssert.DoesNotContain("gh release create", workflow);
        }

        [Test]
        public void OfficialToolboxSyntaxAndPrerequisiteAreDocumented()
        {
            var root = Root();
            var validator = File.ReadAllText(Path.Combine(root, "tools", "validate-release.ps1"));
            StringAssert.Contains("& $toolbox pack $stage $officialOutput", validator);
            StringAssert.Contains("& $toolbox verify addon $addonPath", validator);
            StringAssert.Contains("& $toolbox verify installer $installerPath", validator);
            StringAssert.Contains("Test-OfficialToolboxOutput", validator);
            StringAssert.DoesNotContain("Copy-Item -LiteralPath $officialPackage", validator);
            StringAssert.Contains("PREREQUISITE_MISSING", validator);

            var development = File.ReadAllText(Path.Combine(root, "DEVELOPMENT.md"));
            StringAssert.Contains("PLAYNITE_TOOLBOX", development);
            StringAssert.Contains("validate-release.ps1", development);
            StringAssert.Contains("exit code 2", development);
            StringAssert.DoesNotContain("Download Toolbox", development);
        }

        private static ProcessResult RunPowerShell(string command)
        {
            var start = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"& { " + command + " }\"",
                WorkingDirectory = Root(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ProcessResult(process.ExitCode, output);
            }
        }

        private static string Root()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "PersonalCloudLibrarySource", "PersonalCloudLibrarySource.sln")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            Assert.Fail("Repository root was not found.");
            return string.Empty;
        }

        private sealed class ProcessResult
        {
            public ProcessResult(int exitCode, string output) { ExitCode = exitCode; Output = output; }
            public int ExitCode { get; private set; }
            public string Output { get; private set; }
        }
    }
}
