using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;

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

        [TestCase("PersonalCloudLibrarySource")]
        [TestCase("PersonalCloudLibrarySource.Tests")]
        public void ExplicitCompileInventoryMatchesTrackedSources(string projectName)
        {
            var root = FindRepositoryRoot();
            var projectPath = Path.Combine(root, projectName, projectName + ".csproj");
            var document = new XmlDocument { XmlResolver = null };
            document.Load(projectPath);
            var ns = new XmlNamespaceManager(document.NameTable);
            ns.AddNamespace("m", "http://schemas.microsoft.com/developer/msbuild/2003");
            var includes = document.SelectNodes("//m:Compile[@Include]", ns)
                .Cast<XmlElement>()
                .Select(element => projectName + "/" + element.GetAttribute("Include").Replace('\\', '/'))
                .ToArray();
            var tracked = RunGit(root, "ls-files --cached -z")
                .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(path => path.StartsWith(projectName + "/", StringComparison.Ordinal) &&
                    path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            Assert.That(includes.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(includes.Length),
                "Compile includes must not be duplicated.");
            Assert.That(includes, Is.EquivalentTo(tracked),
                "All tracked C# sources must be included exactly once.");
            foreach (var path in includes)
                Assert.That(File.Exists(Path.Combine(root, path)), Is.True, path);
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
                File.Copy(Path.Combine(repositoryRoot, "LICENSE"),
                    Path.Combine(packageRoot, "LICENSE"));
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

        [TestCase("../outside.txt", "Unsafe package entry path")]
        [TestCase("C:/outside.txt", "Unsafe package entry path")]
        [TestCase("assets/PCLS-LOGO-WIDE.PNG", "Duplicate package entry")]
        [TestCase("Assets/CON.txt", "Unsafe package entry path")]
        public void ReleasePackageContract_RejectsUnsafeArchiveNames(string extraEntry, string expected)
        {
            WithPackageFixture((repositoryRoot, packagePath) =>
            {
                using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update))
                {
                    using (var stream = archive.CreateEntry(extraEntry).Open())
                        stream.WriteByte(0);
                }
                var result = RunPowerShell(repositoryRoot,
                    ".\\tools\\test-release-baseline.ps1 -PackagePath '" + packagePath.Replace("'", "''") + "'");
                Assert.That(result.ExitCode, Is.Not.EqualTo(0), result.Output);
                Assert.That(result.Output, Does.Contain(expected));
            });
        }

        [Test]
        public void ReleasePackageContract_AcceptsCleanArchiveAndRejectsOversizedMetadata()
        {
            WithPackageFixture((repositoryRoot, packagePath) =>
            {
                var command = ".\\tools\\test-release-baseline.ps1 -PackagePath '" + packagePath.Replace("'", "''") + "'";
                var valid = RunPowerShell(repositoryRoot, command);
                Assert.That(valid.ExitCode, Is.EqualTo(0), valid.Output);
                using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update))
                {
                    archive.GetEntry("LICENSE").Delete();
                    using (var stream = archive.CreateEntry("LICENSE").Open())
                    {
                        var bytes = new byte[65537];
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
                var invalid = RunPowerShell(repositoryRoot, command);
                Assert.That(invalid.ExitCode, Is.Not.EqualTo(0), invalid.Output);
                Assert.That(invalid.Output, Does.Contain("metadata exceeds inspection budget"));
            });
        }

        private static void WithPackageFixture(Action<string, string> action)
        {
            var repositoryRoot = FindRepositoryRoot();
            var version = File.ReadLines(Path.Combine(repositoryRoot, "PersonalCloudLibrarySource", "extension.yaml"))
                .Single(line => line.StartsWith("Version:", StringComparison.Ordinal))
                .Substring("Version:".Length).Trim();
            var temporaryRoot = Path.Combine(Path.GetTempPath(), "pcls-package-review-" + Guid.NewGuid().ToString("N"));
            var packagePath = Path.Combine(temporaryRoot, "PersonalCloudLibrarySource-" + version + ".pext");
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    foreach (var name in new[] { "PersonalCloudLibrarySource.dll", "icon.png",
                        "Localization/en_US.xaml", "Assets/pcls-logo-wide.png" })
                    {
                        using (var stream = archive.CreateEntry(name).Open()) stream.WriteByte(0);
                    }
                    using (var writer = new StreamWriter(archive.CreateEntry("extension.yaml").Open()))
                        writer.WriteLine("Version: " + version);
                    using (var writer = new StreamWriter(archive.CreateEntry("LICENSE").Open()))
                        writer.Write(File.ReadAllText(Path.Combine(repositoryRoot, "LICENSE")));
                }
                action(repositoryRoot, packagePath);
            }
            finally
            {
                if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
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
