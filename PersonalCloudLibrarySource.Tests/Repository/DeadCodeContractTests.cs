using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class DeadCodeContractTests
    {
        private static readonly string[] DeadProductionTypeNames =
        {
            "RcloneFileCopier",
            "LocalFileCopier",
            "RcloneCopyResult",
            "LocalCopyResult",
            "CacheStatusService",
            "CacheStatusSnapshot"
        };

        [Test]
        public void ProductionSources_ContainNoDeadImplementationPath()
        {
            var repositoryRoot = FindRepositoryRoot();
            var productionRoot = Path.Combine(repositoryRoot, "PersonalCloudLibrarySource");
            var productionFiles = Directory.GetFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => !HasSegment(path, "bin") &&
                    !HasSegment(path, "obj") &&
                    !HasSegment(path, "packages"))
                .Concat(new[] { Path.Combine(productionRoot, "PersonalCloudLibrarySource.csproj") })
                .ToArray();

            var references = new List<string>();
            foreach (var file in productionFiles)
            {
                var text = File.ReadAllText(file);
                references.AddRange(DeadProductionTypeNames
                    .Where(text.Contains)
                    .Select(name => Path.GetFileName(file) + " references " + name));
            }

            Assert.That(references, Is.Empty,
                "Removed implementation paths must stay absent from production:" + Environment.NewLine +
                string.Join(Environment.NewLine, references));
        }

        [Test]
        public void TrackedFiles_ExcludeRestoredGeneratedAndTemporaryPayloads()
        {
            var repositoryRoot = FindRepositoryRoot();
            var trackedFiles = RunGit(repositoryRoot, "ls-files --cached -z")
                .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => path.Replace('\\', '/'));

            var forbidden = trackedFiles.Where(IsRestoredGeneratedOrTemporary).ToArray();

            Assert.That(forbidden, Is.Empty,
                "Restored, generated, and temporary payloads must not be tracked:" + Environment.NewLine +
                string.Join(Environment.NewLine, forbidden));
        }

        [Test]
        public void ReleasePackageContract_RejectsSourceTestScratchAndTemporaryFiles()
        {
            var repositoryRoot = FindRepositoryRoot();
            var version = File.ReadLines(Path.Combine(repositoryRoot, "PersonalCloudLibrarySource", "extension.yaml"))
                .Single(line => line.StartsWith("Version:", StringComparison.Ordinal))
                .Substring("Version:".Length)
                .Trim();
            var temporaryRoot = Path.Combine(Path.GetTempPath(), "pcls-dead-code-contract-" + Guid.NewGuid().ToString("N"));
            var packageRoot = Path.Combine(temporaryRoot, "package");
            var packagePath = Path.Combine(temporaryRoot, "PersonalCloudLibrarySource-" + version + ".pext");

            try
            {
                Directory.CreateDirectory(Path.Combine(packageRoot, "Localization"));
                Directory.CreateDirectory(Path.Combine(packageRoot, "Assets"));
                File.WriteAllText(Path.Combine(packageRoot, "PersonalCloudLibrarySource.dll"), "test");
                File.WriteAllText(Path.Combine(packageRoot, "extension.yaml"), "Version: " + version);
                File.WriteAllText(Path.Combine(packageRoot, "icon.png"), "test");
                File.WriteAllText(Path.Combine(packageRoot, "Localization", "en_US.xaml"), "test");
                File.WriteAllText(Path.Combine(packageRoot, "Assets", "pcls-logo-wide.png"), "test");
                File.WriteAllText(Path.Combine(packageRoot, "LeakedSource.cs"), "// must not ship");
                Directory.CreateDirectory(Path.Combine(packageRoot, "Tests"));
                Directory.CreateDirectory(Path.Combine(packageRoot, ".superpowers"));
                Directory.CreateDirectory(Path.Combine(packageRoot, "temp"));
                File.WriteAllText(Path.Combine(packageRoot, "Tests", "fixture.dat"), "must not ship");
                File.WriteAllText(Path.Combine(packageRoot, ".superpowers", "scratch.dat"), "must not ship");
                File.WriteAllText(Path.Combine(packageRoot, "temp", "working.dat"), "must not ship");
                ZipFile.CreateFromDirectory(packageRoot, packagePath);

                var result = RunPowerShell(
                    repositoryRoot,
                    ".\\tools\\test-release-baseline.ps1 -PackagePath '" + packagePath.Replace("'", "''") + "'");

                Assert.That(result.ExitCode, Is.Not.EqualTo(0), result.Output);
                Assert.That(result.Output, Does.Contain("forbidden file"));
                Assert.That(result.Output, Does.Contain("Tests/fixture.dat"));
                Assert.That(result.Output, Does.Contain(".superpowers/scratch.dat"));
                Assert.That(result.Output, Does.Contain("temp/working.dat"));
            }
            finally
            {
                if (Directory.Exists(temporaryRoot))
                {
                    Directory.Delete(temporaryRoot, true);
                }
            }
        }

        private static bool IsRestoredGeneratedOrTemporary(string path)
        {
            var segments = path.Split('/');
            return segments.Any(segment =>
                    segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                    segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                    segment.Equals("packages", StringComparison.OrdinalIgnoreCase) ||
                    segment.Equals("dist", StringComparison.OrdinalIgnoreCase)) ||
                path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(path).StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSegment(string path, string segment)
        {
            return path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(value => value.Equals(segment, StringComparison.OrdinalIgnoreCase));
        }

        private static string RunGit(string workingDirectory, string arguments)
        {
            var result = RunProcess("git", arguments, workingDirectory);
            Assert.That(result.ExitCode, Is.EqualTo(0), result.Output);
            return result.Output;
        }

        private static ProcessResult RunPowerShell(string workingDirectory, string command)
        {
            return RunProcess(
                "powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"& { " + command + " }\"",
                workingDirectory);
        }

        private static ProcessResult RunProcess(string fileName, string arguments, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ProcessResult(process.ExitCode, output + error);
            }
        }

        private static string FindRepositoryRoot()
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

            Assert.Fail("Repository root was not found from " + TestContext.CurrentContext.TestDirectory);
            return string.Empty;
        }

        private sealed class ProcessResult
        {
            public ProcessResult(int exitCode, string output)
            {
                ExitCode = exitCode;
                Output = output;
            }

            public int ExitCode { get; private set; }
            public string Output { get; private set; }
        }
    }
}
