using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    public enum LauncherCandidateKind
    {
        LikelyGame,
        NeedsReview,
        InstallerOrHelper
    }

    public sealed class LauncherCandidate
    {
        public string RelativePath { get; internal set; } = string.Empty;
        public LauncherCandidateKind Kind { get; internal set; }
        public int Score { get; internal set; }
        public string Explanation { get; internal set; } = string.Empty;
    }

    public sealed class LauncherSelectionPreview
    {
        public IReadOnlyList<LauncherCandidate> Candidates { get; internal set; }
            = new LauncherCandidate[0];
        public string SuggestedRelativePath { get; internal set; } = string.Empty;
        // No candidate ranking result grants execution or installer consent.
        public bool RequiresUserConfirmation => true;
        public bool CanExecuteUnattended => false;
    }

    // Pure filename-based guidance for the future extracted-package wizard.
    // It never enumerates a real disk or executes a discovered path.
    public sealed class GameLauncherCandidatePolicy
    {
        private static readonly HashSet<string> AllowedExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".exe", ".bat", ".cmd" };

        private static readonly string[] InstallerIndicators =
        {
            "setup", "install", "unins", "uninstall", "remove",
            "vcredist", "vcredist", "redist", "dxsetup", "directx",
            "updater", "update", "crashreporter", "crashhandler",
            "anticheat", "easyanticheat", "prerequisite"
        };

        public LauncherSelectionPreview Preview(
            IEnumerable<string> relativeFilePaths, string gameTitle)
        {
            var result = new LauncherSelectionPreview();
            if (relativeFilePaths == null)
                return result;

            var candidates = new List<LauncherCandidate>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizedTitle = Clean(gameTitle);
            var count = 0;
            foreach (var input in relativeFilePaths)
            {
                if (++count > ArchiveInspectionPolicy.MaxEntries)
                    return result; // unbounded input must not produce a suggestion
                if (string.IsNullOrWhiteSpace(input))
                    continue;
                var path = input.Replace('/', '\\');
                // Reuse the strict archive path validation so even this
                // read-only preview never suggests an escaped/unusable path.
                if (!new ArchiveInspectionPolicy().Inspect(new[]
                {
                    new ArchiveEntryObservation
                    {
                        EntryPath = path, UncompressedBytes = 1, CompressedBytes = 1
                    }
                }).AllowedForStagedExtraction || !seen.Add(path))
                    continue;
                var extension = Path.GetExtension(path);
                if (!AllowedExtensions.Contains(extension))
                    continue;

                var stem = Clean(Path.GetFileNameWithoutExtension(path));
                var helper = InstallerIndicators.Any(x =>
                    stem.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0);
                var score = extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
                    ? 50 : 10;
                if (!string.IsNullOrWhiteSpace(normalizedTitle) &&
                    stem.Equals(normalizedTitle, StringComparison.OrdinalIgnoreCase))
                    score += 40;
                else if (normalizedTitle.Length >= 4 &&
                    stem.IndexOf(normalizedTitle, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 20;
                if (stem.Equals("game", StringComparison.OrdinalIgnoreCase))
                    score += 10;
                if (stem.IndexOf("launcher", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 5;
                if (helper)
                    score -= 150;

                candidates.Add(new LauncherCandidate
                {
                    RelativePath = path,
                    Kind = helper ? LauncherCandidateKind.InstallerOrHelper :
                        score >= 70 ? LauncherCandidateKind.LikelyGame :
                        LauncherCandidateKind.NeedsReview,
                    Score = score,
                    Explanation = helper
                        ? "Installer, dependency, updater or helper name: inspect manually."
                        : score >= 70
                            ? "Possible game executable; verify before using as a Play action."
                            : "Executable choice is ambiguous and needs user review."
                });
            }
            var ordered = candidates.OrderByDescending(x => x.Score)
                .ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.Candidates = ordered.AsReadOnly();
            if (ordered.Count > 0 && ordered[0].Kind == LauncherCandidateKind.LikelyGame &&
                (ordered.Count == 1 || ordered[0].Score - ordered[1].Score >= 20))
                result.SuggestedRelativePath = ordered[0].RelativePath;
            return result;
        }

        private static string Clean(string value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }
    }
}
