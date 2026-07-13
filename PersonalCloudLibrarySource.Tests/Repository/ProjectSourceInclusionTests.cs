using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class ProjectSourceInclusionTests
    {
        private static readonly ProjectContract[] Projects =
        {
            new ProjectContract("PersonalCloudLibrarySource", "PersonalCloudLibrarySource.csproj"),
            new ProjectContract("PersonalCloudLibrarySource.Tests", "PersonalCloudLibrarySource.Tests.csproj")
        };

        [Test]
        public void TrackedSourceFiles_AreIncludedInTheirProject()
        {
            var repositoryRoot = FindRepositoryRoot();
            var trackedSources = GetTrackedSourceFiles(repositoryRoot);
            var missingEntries = new List<string>();

            foreach (var project in Projects)
            {
                var projectDirectory = Path.Combine(repositoryRoot, project.Directory);
                var compiledSources = GetCompileEntries(Path.Combine(projectDirectory, project.File));
                var intendedSources = trackedSources
                    .Where(path => path.StartsWith(project.Directory + "/", StringComparison.OrdinalIgnoreCase))
                    .Select(path => path.Substring(project.Directory.Length + 1))
                    .Where(IsIntendedSource);

                missingEntries.AddRange(intendedSources
                    .Where(path => !compiledSources.Contains(path))
                    .Select(path => project.File + " omits " + path));
            }

            Assert.That(missingEntries, Is.Empty,
                "Every tracked source file must have a Compile entry:" + Environment.NewLine +
                string.Join(Environment.NewLine, missingEntries));
        }

        [Test]
        public void ProjectCompileEntries_PointToExistingFiles()
        {
            var repositoryRoot = FindRepositoryRoot();
            var missingFiles = new List<string>();

            foreach (var project in Projects)
            {
                var projectDirectory = Path.Combine(repositoryRoot, project.Directory);
                var projectFile = Path.Combine(projectDirectory, project.File);
                missingFiles.AddRange(GetCompileEntries(projectFile)
                    .Where(path => !File.Exists(Path.Combine(projectDirectory, path.Replace('/', Path.DirectorySeparatorChar))))
                    .Select(path => project.File + " references missing " + path));
            }

            Assert.That(missingFiles, Is.Empty,
                "Every Compile entry must point to an existing file:" + Environment.NewLine +
                string.Join(Environment.NewLine, missingFiles));
        }

        [Test]
        public void GetCompileEntries_ParsesNamespacedProjectXmlAttributes()
        {
            var temporaryDirectory = Path.Combine(Path.GetTempPath(), "pcls-project-contract-" + Guid.NewGuid());
            Directory.CreateDirectory(temporaryDirectory);
            var projectFile = Path.Combine(temporaryDirectory, "Contract.csproj");

            try
            {
                File.WriteAllText(projectFile,
                    "<Project xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">" +
                    "<ItemGroup>" +
                    "<Compile Condition=\"'$(Configuration)' == 'Debug'\" Include=\"Reordered\\First.cs\" />" +
                    "<Compile Include='Single\\Second.cs' Condition='true' />" +
                    "<Compile Condition='true' Include='Escaped&#92;Third.cs' />" +
                    "</ItemGroup>" +
                    "</Project>");

                CollectionAssert.AreEquivalent(new[]
                {
                    "Reordered/First.cs",
                    "Single/Second.cs",
                    "Escaped/Third.cs"
                }, GetCompileEntries(projectFile));
            }
            finally
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        private static HashSet<string> GetCompileEntries(string projectFile)
        {
            var document = new XmlDocument();
            document.Load(projectFile);
            var compileElements = document.SelectNodes("//*[local-name()='Compile']");

            return new HashSet<string>(
                compileElements.Cast<XmlElement>()
                    .Select(element => element.GetAttribute("Include"))
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Select(NormalizeProjectRelativePath),
                StringComparer.OrdinalIgnoreCase);
        }

        private static string[] GetTrackedSourceFiles(string repositoryRoot)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-files --cached -z -- \"*.cs\"",
                WorkingDirectory = repositoryRoot,
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
                Assert.That(process.ExitCode, Is.EqualTo(0), "git ls-files failed: " + error);
                return output.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(NormalizeProjectRelativePath)
                    .ToArray();
            }
        }

        private static bool IsIntendedSource(string path)
        {
            var segments = NormalizeProjectRelativePath(path).Split('/');
            var rootDirectory = segments[0];
            if (rootDirectory.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                rootDirectory.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                rootDirectory.Equals("packages", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fileName = segments[segments.Length - 1];
            return !fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) &&
                !fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase) &&
                !fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase) &&
                !fileName.StartsWith("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeProjectRelativePath(string path)
        {
            var normalized = path.Replace('\\', '/');
            while (normalized.StartsWith("./", StringComparison.Ordinal))
            {
                normalized = normalized.Substring(2);
            }

            return normalized;
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

        private sealed class ProjectContract
        {
            public ProjectContract(string directory, string file)
            {
                Directory = directory;
                File = file;
            }

            public string Directory { get; private set; }
            public string File { get; private set; }
        }
    }
}
