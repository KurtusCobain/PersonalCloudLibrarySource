using System.Collections.Generic;
using System;
using System.IO;

namespace PersonalCloudLibrarySource
{
    public class ManifestGenerationOptions
    {
        // A rejected edit or stale background scan must not overwrite the
        // active catalog before a new settings pointer commits.
        public static ManifestGenerationOptions CreateIsolated(string sourceRoot,
            string outputDirectory, string backupDirectory)
        {
            var root = Path.GetFullPath(outputDirectory);
            var name = "personal-cloud-library.generated-" + Guid.NewGuid().ToString("N");
            return new ManifestGenerationOptions
            {
                SourceRoot = sourceRoot,
                OutputPath = Path.Combine(root, name + ".json"),
                ReportPath = Path.Combine(root, name + ".report.txt"),
                BackupDirectory = backupDirectory
            };
        }

        public string SourceRoot { get; set; }
        public string OutputPath { get; set; }
        public string ReportPath { get; set; }
        public string BackupDirectory { get; set; }
        public List<string> IncludeExtensions { get; set; } = new List<string>();
        public List<string> ExcludeFolders { get; set; } = new List<string>();
        public bool IncludeNonLaunchablePackages { get; set; }
        public bool NoReport { get; set; }
    }
}
