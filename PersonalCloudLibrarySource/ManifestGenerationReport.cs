using System.Collections.Generic;

namespace PersonalCloudLibrarySource
{
    public class ManifestGenerationReport
    {
        public string SourceRoot { get; set; }
        public string OutputPath { get; set; }
        public string ReportPath { get; set; }
        public int ScannedEntryCount { get; set; }
        public int DirectoryCount { get; set; }
        public int FileCount { get; set; }
        public int ItemCount { get; set; }
        public int DetectedDirectoryItemCount { get; set; }
        public List<string> DetectedDirectories { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> SkippedEntries { get; set; } = new List<string>();
        public PersonalCloudLibraryManifest Manifest { get; set; }
    }
}
