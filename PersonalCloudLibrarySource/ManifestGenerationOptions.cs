using System.Collections.Generic;

namespace PersonalCloudLibrarySource
{
    public class ManifestGenerationOptions
    {
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
