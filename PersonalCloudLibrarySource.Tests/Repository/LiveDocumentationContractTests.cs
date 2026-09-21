using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class LiveDocumentationContractTests
    {
        private const string AddonId = "PersonalCloudLibrarySource_61993828-67a8-4468-93a2-293442e36328";
        private const string CurrentVersion = "0.3.2";
        private const string V03TargetPattern = @"(?im)^\s*(?:-\s*)?(?:Target release:|\*\*Target release:\*\*)\s*`?0\.3\.0`?(?:\.(?=\s|$))?(?:\s|$)";

        [Test]
        public void LiveMarkdown_LocalLinksAndImagesResolve()
        {
            var broken = new List<string>();
            foreach (var file in LiveMarkdownFiles())
            {
                var text = File.ReadAllText(file);
                foreach (Match match in Regex.Matches(text, @"!?\[[^\]]*\]\(([^)]+)\)"))
                {
                    var target = match.Groups[1].Value.Trim().Trim('<', '>');
                    if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        target.StartsWith("#", StringComparison.Ordinal) ||
                        target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var pathPart = Uri.UnescapeDataString(target.Split('#')[0].Replace('/', Path.DirectorySeparatorChar));
                    var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), pathPart));
                    if (!File.Exists(resolved) && !Directory.Exists(resolved))
                    {
                        broken.Add(Relative(file) + " -> " + target);
                    }
                }
            }

            Assert.That(broken, Is.Empty, "Broken local Markdown targets:" + Environment.NewLine + string.Join(Environment.NewLine, broken));
        }

        [Test]
        public void LiveDocumentation_UsesCurrentPreparationStatusAndRequiredGuidance()
        {
            var readme = File.ReadAllText(Path.Combine(Root(), "README.md"));
            StringAssert.Contains("0.3.2", readme);
            StringAssert.DoesNotContain("current release target is `0.2.0`", readme.ToLowerInvariant());

            var requiredTerms = new[]
            {
                "Setup", "LocalFile", "LocalFolder", "RcloneRemote", "rclone config",
                "90 seconds", "Cache", "deletion", "Desktop", "Fullscreen", "Upgrade",
                "Troubleshooting", "diagnostics", "report", "rights", "Known limits"
            };
            var missing = requiredTerms.Where(term => readme.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0).ToArray();
            Assert.That(missing, Is.Empty, "README is missing required live guidance: " + string.Join(", ", missing));

            var stale = new List<string>();
            foreach (var file in LiveMarkdownFiles().Where(path => !path.EndsWith("CHANGELOG.md", StringComparison.OrdinalIgnoreCase)))
            {
                var text = File.ReadAllText(file);
                if (Regex.IsMatch(text, @"(?i)(current release target is|current release is|this release is)\s+`?0\.2\.0"))
                {
                    stale.Add(Relative(file));
                }
                if (Regex.IsMatch(text, @"(?i)\bv0\.2 (pass|setup flow|guided setup flow)"))
                {
                    stale.Add(Relative(file));
                }
                if (text.Contains("RcloneTimeoutSeconds = 30"))
                {
                    stale.Add(Relative(file));
                }
            }
            Assert.That(stale.Distinct(), Is.Empty, "Stale live release claims/instructions: " + string.Join(", ", stale.Distinct()));
        }

        [Test]
        public void RcloneDocumentation_DistinguishesProbeAndQueuedTransferDeadlines()
        {
            var text = File.ReadAllText(Path.Combine(Root(), "docs", "setup-rclone.md"));
            StringAssert.Contains("`rclone cat` manifest reads", text);
            StringAssert.Contains("`rclone listremotes` connection tests", text);
            StringAssert.Contains("total `WaitForExit` deadline", text);
            StringAssert.Contains("first output or error activity", text);
            StringAssert.Contains("at most 30 seconds", text);
            StringAssert.Contains("90-second inactivity", text);
            StringAssert.Contains("Queued transfers", text);
        }

        [Test]
        public void HistoricalV03Documents_AreVisiblyMarkedAndExcludedFromLiveTargets()
        {
            var paths = new[]
            {
                Path.Combine(Root(), "docs", "superpowers", "plans", "2026-07-11-user-friendly-dashboard-implementation.md"),
                Path.Combine(Root(), "docs", "superpowers", "specs", "2026-07-11-user-friendly-dashboard-design.md")
            };

            foreach (var path in paths)
            {
                var opening = string.Join(Environment.NewLine, File.ReadLines(path).Take(12));
                StringAssert.Contains("Historical", opening, Relative(path));
                StringAssert.Contains("0.3.0", opening, Relative(path));
            }

            var activeTargets = Directory.GetFiles(Path.Combine(Root(), "docs"), "*.md", SearchOption.AllDirectories)
                .Where(path => HasUnmarkedV03Target(File.ReadAllText(path)))
                .Select(Relative)
                .ToArray();
            Assert.That(activeTargets, Is.Empty, "Nested docs with an active 0.3.0 target must be marked Historical.");
        }

        [TestCase("- Target release: `0.3.0`.")]
        [TestCase("**Target release:** `0.3.0`")]
        public void HistoricalTargetDetector_HandlesActualMarkdownForms(string targetLine)
        {
            Assert.That(HasUnmarkedV03Target("# Record\n\n" + targetLine), Is.True,
                "Exact target form must be flagged without a Historical marker.");
            Assert.That(HasUnmarkedV03Target("# Record\n\n> **Historical status:** preserved record\n\n" + targetLine), Is.False,
                "Exact target form must be accepted when the opening marks it Historical.");
        }

        [Test]
        public void HistoricalTargetDetector_MatchesActualPlanTargetExactlyOnce()
        {
            var plan = File.ReadAllText(Path.Combine(
                Root(), "docs", "superpowers", "plans", "2026-07-11-user-friendly-dashboard-implementation.md"));
            StringAssert.Contains("- Target release: `0.3.0`.", plan);
            Assert.That(CountV03Targets(plan), Is.EqualTo(1));
        }

        [Test]
        public void DistributionYaml_KeepsIdentityAndPreReleaseVersionContract()
        {
            var extension = BoundedYamlParser.Parse(File.ReadAllText(Path.Combine(Root(), "PersonalCloudLibrarySource", "extension.yaml")));
            var addon = BoundedYamlParser.Parse(File.ReadAllText(Path.Combine(Root(), "playnite-addon", "addon-database.yaml")));
            var installer = BoundedYamlParser.Parse(File.ReadAllText(Path.Combine(Root(), "playnite-addon", "installer.yaml")));

            Assert.That(extension.Scalar("Id"), Is.EqualTo(AddonId));
            Assert.That(addon.Scalar("AddonId"), Is.EqualTo(AddonId));
            Assert.That(installer.Scalar("AddonId"), Is.EqualTo(AddonId));
            Assert.That(extension.Scalar("Version"), Is.EqualTo(CurrentVersion));
            StringAssert.Contains("## 0.3.2", File.ReadAllText(Path.Combine(Root(), "CHANGELOG.md")));

            Assert.That(extension.Mapping.ContainsKey("Links"), Is.True);
            Assert.That(addon.Mapping.ContainsKey("Screenshots"), Is.True);
            Assert.That(installer.Sequence("Packages").Count, Is.GreaterThan(0));
            Assert.That(installer.Sequence("Packages").Any(item => item.Scalar("Version") == "1.0.0"), Is.False);
        }

        [TestCase("Root:\n   Child: value", TestName = "YamlParser_RejectsOddIndentation")]
        [TestCase("Items:\n  value", TestName = "YamlParser_RejectsSequenceWithoutDash")]
        [TestCase("Description: |\nnot-indented", TestName = "YamlParser_RejectsUnindentedBlockScalar")]
        public void YamlParser_RejectsMalformedStructure(string yaml)
        {
            Assert.Throws<FormatException>(() => BoundedYamlParser.Parse(yaml));
        }

        [Test]
        public void ReferencedRepositoryImagesDecode()
        {
            var images = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in LiveMarkdownFiles())
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"!\[[^\]]*\]\(([^)]+)\)"))
                {
                    var target = match.Groups[1].Value.Trim().Trim('<', '>');
                    if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    images.Add(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file), target.Replace('/', Path.DirectorySeparatorChar))));
                }
            }

            foreach (var yaml in new[] { "playnite-addon/addon-database.yaml", "PersonalCloudLibrarySource/extension.yaml" })
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(Path.Combine(Root(), yaml.Replace('/', Path.DirectorySeparatorChar))), @"https://raw\.githubusercontent\.com/[^/]+/[^/]+/[^/]+/(?<path>[^\s]+\.png)"))
                {
                    images.Add(Path.Combine(Root(), match.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar)));
                }
            }

            Assert.That(images, Is.Not.Empty);
            foreach (var imagePath in images)
            {
                Assert.That(File.Exists(imagePath), Is.True, "Missing image: " + Relative(imagePath));
                using (var image = Image.FromFile(imagePath))
                {
                    Assert.That(image.Width, Is.GreaterThan(0), Relative(imagePath));
                    Assert.That(image.Height, Is.GreaterThan(0), Relative(imagePath));
                }
            }
        }

        [Test]
        public void ReleaseCandidateScreenshots_AreCompleteConsistentReferencedAndPrivate()
        {
            var names = new[]
            {
                "pcls-setup-source.png",
                "pcls-settings-provider.png",
                "pcls-settings-cache-safety.png",
                "pcls-dashboard-overview.png",
                "pcls-dashboard-transfer-activity.png",
                "pcls-library-example-games.png",
                "pcls-fullscreen-example-games.png"
            };
            var liveText = string.Join("\n", LiveFiles().Select(File.ReadAllText));
            var expectedSize = Size.Empty;
            foreach (var name in names)
            {
                var path = Path.Combine(Root(), "docs", "images", name);
                Assert.That(File.Exists(path), Is.True, "Missing release screenshot: " + name);
                using (var image = Image.FromFile(path))
                {
                    Assert.That(image.Width * 9, Is.EqualTo(image.Height * 16), name + " must be 16:9");
                    if (expectedSize == Size.Empty)
                    {
                        expectedSize = image.Size;
                    }
                    Assert.That(image.Size, Is.EqualTo(expectedSize), name + " must match the master dimensions");
                }
                StringAssert.Contains(name, liveText, name + " must be referenced by live documentation or metadata");
            }

            var listing = File.ReadAllText(Path.Combine(Root(), "playnite-addon", "addon-database.yaml"));
            foreach (var name in new[] { "pcls-settings-provider.png", "pcls-dashboard-overview.png", "pcls-library-example-games.png" })
            {
                StringAssert.Contains("docs/images/" + name, listing);
            }

            foreach (var forbidden in new[] { "ROMcade_Master", "katie\\", "019f5d68-bcfb" })
            {
                Assert.That(liveText.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase), Is.LessThan(0),
                    "Private screenshot/demo text leaked: " + forbidden);
            }
        }

        [Test]
        public void ReleaseCandidateCopy_KeepsPreReleaseIdentityAndCompleteOnePointZeroDraft()
        {
            var readme = File.ReadAllText(Path.Combine(Root(), "README.md"));
            var changelog = File.ReadAllText(Path.Combine(Root(), "CHANGELOG.md"));
            var notes = File.ReadAllText(Path.Combine(Root(), "docs", "playnite-release-notes.md"));
            StringAssert.Contains("1.0 release candidate", readme);
            StringAssert.Contains("## 1.0.0 - Unreleased", changelog);
            StringAssert.Contains("Release-candidate draft", notes);
            foreach (var term in new[] { "LocalFile", "LocalFolder", "RcloneRemote", "dashboard", "Fullscreen", "safe uninstall", "migration", "diagnostics" })
            {
                Assert.That((changelog + notes).IndexOf(term, StringComparison.OrdinalIgnoreCase), Is.GreaterThanOrEqualTo(0),
                    "Missing 1.0 release topic: " + term);
            }
            DistributionYaml_KeepsIdentityAndPreReleaseVersionContract();
        }

        [Test]
        public void ExternalUrls_HaveRecordedCheckResults()
        {
            var recordPath = Path.Combine(Root(), "docs", "external-link-checks.md");
            Assert.That(File.Exists(recordPath), Is.True);
            var record = File.ReadAllText(recordPath);
            var urls = LiveFiles()
                .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"https://[^\s)>]+")
                    .Cast<Match>().Select(match => match.Value.TrimEnd('.', ',', '\'', '"')))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var missing = urls.Where(url => record.IndexOf(url, StringComparison.OrdinalIgnoreCase) < 0).ToArray();
            Assert.That(missing, Is.Empty, "External URLs without a recorded result: " + string.Join(", ", missing));
            StringAssert.Contains("2026-07-14", record);
            Assert.That(record.IndexOf("unchecked", StringComparison.OrdinalIgnoreCase), Is.LessThan(0));
        }

        private static IEnumerable<string> LiveMarkdownFiles()
        {
            return LiveFiles().Where(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasUnmarkedV03Target(string markdown)
        {
            var hasTarget = CountV03Targets(markdown) > 0;
            if (!hasTarget)
            {
                return false;
            }

            var opening = string.Join(
                Environment.NewLine,
                (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Take(12));
            return opening.IndexOf("Historical", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static int CountV03Targets(string markdown)
        {
            return Regex.Matches(markdown ?? string.Empty, V03TargetPattern).Count;
        }

        private static IEnumerable<string> LiveFiles()
        {
            var root = Root();
            var rootFiles = new[] { "README.md", "CHANGELOG.md", "DEVELOPMENT.md", "CONTRIBUTING.md", "SECURITY.md" }
                .Select(name => Path.Combine(root, name));
            var docs = Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.TopDirectoryOnly);
            var yaml = new[]
            {
                Path.Combine(root, "PersonalCloudLibrarySource", "extension.yaml"),
                Path.Combine(root, "playnite-addon", "addon-database.yaml"),
                Path.Combine(root, "playnite-addon", "installer.yaml")
            };
            return rootFiles.Concat(docs).Concat(yaml);
        }

        private static string Relative(string path)
        {
            var root = Root().TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path.Substring(root.Length) : path;
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
    }

    internal sealed class BoundedYamlNode
    {
        private BoundedYamlNode(Dictionary<string, BoundedYamlNode> mapping, List<BoundedYamlNode> sequence, string value)
        {
            Mapping = mapping;
            SequenceItems = sequence;
            Value = value;
        }

        public Dictionary<string, BoundedYamlNode> Mapping { get; private set; }
        public List<BoundedYamlNode> SequenceItems { get; private set; }
        public string Value { get; private set; }

        public static BoundedYamlNode Map() => new BoundedYamlNode(new Dictionary<string, BoundedYamlNode>(StringComparer.Ordinal), null, null);
        public static BoundedYamlNode List() => new BoundedYamlNode(null, new List<BoundedYamlNode>(), null);
        public static BoundedYamlNode ScalarValue(string value) => new BoundedYamlNode(null, null, value);

        public string Scalar(string key)
        {
            BoundedYamlNode node;
            if (Mapping == null || !Mapping.TryGetValue(key, out node) || node.Value == null)
            {
                throw new InvalidOperationException("Missing YAML scalar: " + key);
            }
            return node.Value;
        }

        public List<BoundedYamlNode> Sequence(string key)
        {
            BoundedYamlNode node;
            if (Mapping == null || !Mapping.TryGetValue(key, out node) || node.SequenceItems == null)
            {
                throw new InvalidOperationException("Missing YAML sequence: " + key);
            }
            return node.SequenceItems;
        }
    }

    internal static class BoundedYamlParser
    {
        public static BoundedYamlNode Parse(string yaml)
        {
            if (yaml == null)
            {
                throw new ArgumentNullException(nameof(yaml));
            }

            var lines = Tokenize(yaml);
            if (lines.Count == 0)
            {
                throw new FormatException("YAML document is empty.");
            }
            if (lines[0].Indent != 0)
            {
                throw Error(lines[0], "Document must start at indentation zero.");
            }

            var index = 0;
            var root = ParseNode(lines, ref index, 0);
            if (index != lines.Count)
            {
                throw Error(lines[index], "Unexpected trailing structure.");
            }
            if (root.Mapping == null)
            {
                throw new FormatException("The distribution document root must be a mapping.");
            }
            return root;
        }

        private static BoundedYamlNode ParseNode(List<YamlLine> lines, ref int index, int indent)
        {
            if (index >= lines.Count || lines[index].Indent != indent)
            {
                throw index < lines.Count
                    ? Error(lines[index], "Unexpected indentation.")
                    : new FormatException("Expected nested YAML content.");
            }

            return IsSequenceLine(lines[index].Text)
                ? ParseSequence(lines, ref index, indent)
                : ParseMapping(lines, ref index, indent);
        }

        private static BoundedYamlNode ParseMapping(List<YamlLine> lines, ref int index, int indent)
        {
            var map = BoundedYamlNode.Map();
            while (index < lines.Count && lines[index].Indent == indent)
            {
                var line = lines[index];
                if (IsSequenceLine(line.Text))
                {
                    throw Error(line, "A sequence item is not valid in this mapping position.");
                }
                ParseMappingEntry(lines, ref index, indent, map, line.Text);
            }
            return map;
        }

        private static void ParseMappingEntry(
            List<YamlLine> lines,
            ref int index,
            int indent,
            BoundedYamlNode map,
            string text)
        {
            var line = lines[index];
            var colon = FindMappingColon(text);
            if (colon <= 0)
            {
                throw Error(line, "Expected a mapping key followed by a colon.");
            }

            var key = text.Substring(0, colon).Trim();
            if (key.Length == 0 || map.Mapping.ContainsKey(key))
            {
                throw Error(line, key.Length == 0 ? "Mapping key is empty." : "Duplicate mapping key: " + key);
            }

            var remainder = StripComment(text.Substring(colon + 1)).Trim();
            index++;
            if (remainder == "|")
            {
                var start = index;
                while (index < lines.Count && lines[index].Indent > indent)
                {
                    index++;
                }
                if (index == start || lines[start].Indent != indent + 2)
                {
                    throw Error(line, "Block scalar content must be indented by two spaces.");
                }
                map.Mapping.Add(key, BoundedYamlNode.ScalarValue("<block>"));
                return;
            }

            if (remainder.Length > 0)
            {
                map.Mapping.Add(key, BoundedYamlNode.ScalarValue(Unquote(remainder, line)));
                if (index < lines.Count && lines[index].Indent > indent)
                {
                    throw Error(lines[index], "A scalar cannot own nested content.");
                }
                return;
            }

            if (index >= lines.Count || lines[index].Indent <= indent)
            {
                throw Error(line, "Mapping value is missing.");
            }
            if (lines[index].Indent != indent + 2)
            {
                throw Error(lines[index], "Nested content must be indented by two spaces.");
            }
            map.Mapping.Add(key, ParseNode(lines, ref index, indent + 2));
        }

        private static BoundedYamlNode ParseSequence(List<YamlLine> lines, ref int index, int indent)
        {
            var sequence = BoundedYamlNode.List();
            while (index < lines.Count && lines[index].Indent == indent)
            {
                var line = lines[index];
                if (!IsSequenceLine(line.Text))
                {
                    throw Error(line, "Sequence items must start with a dash.");
                }

                var remainder = line.Text.Length == 1 ? string.Empty : line.Text.Substring(1).TrimStart();
                index++;
                if (remainder.Length == 0)
                {
                    if (index >= lines.Count || lines[index].Indent != indent + 2)
                    {
                        throw Error(line, "Sequence item content is missing or incorrectly indented.");
                    }
                    sequence.SequenceItems.Add(ParseNode(lines, ref index, indent + 2));
                    continue;
                }

                if (FindMappingColon(remainder) > 0)
                {
                    var item = BoundedYamlNode.Map();
                    ParseInlineSequenceMapping(lines, ref index, indent, item, remainder, line);
                    sequence.SequenceItems.Add(item);
                    continue;
                }

                sequence.SequenceItems.Add(BoundedYamlNode.ScalarValue(Unquote(StripComment(remainder).Trim(), line)));
                if (index < lines.Count && lines[index].Indent > indent)
                {
                    throw Error(lines[index], "A scalar sequence item cannot own nested content.");
                }
            }
            return sequence;
        }

        private static void ParseInlineSequenceMapping(
            List<YamlLine> lines,
            ref int index,
            int sequenceIndent,
            BoundedYamlNode item,
            string firstEntry,
            YamlLine sequenceLine)
        {
            AddInlineEntry(item, firstEntry, sequenceLine);
            while (index < lines.Count && lines[index].Indent == sequenceIndent + 2)
            {
                var entryLine = lines[index];
                ParseMappingEntry(lines, ref index, sequenceIndent + 2, item, entryLine.Text);
            }
            if (index < lines.Count && lines[index].Indent > sequenceIndent)
            {
                throw Error(lines[index], "Unexpected indentation inside sequence mapping.");
            }
        }

        private static void AddInlineEntry(BoundedYamlNode item, string text, YamlLine line)
        {
            var colon = FindMappingColon(text);
            var key = text.Substring(0, colon).Trim();
            var value = StripComment(text.Substring(colon + 1)).Trim();
            if (key.Length == 0 || value.Length == 0)
            {
                throw Error(line, "Inline sequence mapping requires a key and scalar value.");
            }
            item.Mapping.Add(key, BoundedYamlNode.ScalarValue(Unquote(value, line)));
        }

        private static List<YamlLine> Tokenize(string yaml)
        {
            var result = new List<YamlLine>();
            var source = yaml.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < source.Length; i++)
            {
                var raw = source[i];
                if (raw.IndexOf('\t') >= 0)
                {
                    throw new FormatException("YAML line " + (i + 1) + " contains a tab.");
                }
                var indent = raw.TakeWhile(character => character == ' ').Count();
                var text = raw.Substring(indent);
                if (text.Length == 0 || text.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }
                if (indent % 2 != 0)
                {
                    throw new FormatException("YAML line " + (i + 1) + " uses odd indentation.");
                }
                result.Add(new YamlLine(indent, text, i + 1));
            }
            return result;
        }

        private static bool IsSequenceLine(string text)
        {
            return text == "-" || text.StartsWith("- ", StringComparison.Ordinal);
        }

        private static int FindMappingColon(string text)
        {
            var quote = '\0';
            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];
                if ((current == '\'' || current == '"') && (quote == '\0' || quote == current))
                {
                    quote = quote == '\0' ? current : '\0';
                }
                else if (current == ':' && quote == '\0')
                {
                    return i;
                }
            }
            return -1;
        }

        private static string StripComment(string text)
        {
            var quote = '\0';
            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];
                if ((current == '\'' || current == '"') && (quote == '\0' || quote == current))
                {
                    quote = quote == '\0' ? current : '\0';
                }
                else if (current == '#' && quote == '\0' && (i == 0 || char.IsWhiteSpace(text[i - 1])))
                {
                    return text.Substring(0, i);
                }
            }
            return text;
        }

        private static string Unquote(string value, YamlLine line)
        {
            if (value.Length == 0)
            {
                throw Error(line, "Scalar value is empty.");
            }
            if (value[0] == '\'' || value[0] == '"')
            {
                if (value.Length < 2 || value[value.Length - 1] != value[0])
                {
                    throw Error(line, "Quoted scalar is not terminated.");
                }
                return value.Substring(1, value.Length - 2);
            }
            return value;
        }

        private static FormatException Error(YamlLine line, string message)
        {
            return new FormatException("YAML line " + line.Number + ": " + message);
        }

        private sealed class YamlLine
        {
            public YamlLine(int indent, string text, int number)
            {
                Indent = indent;
                Text = text;
                Number = number;
            }

            public int Indent { get; private set; }
            public string Text { get; private set; }
            public int Number { get; private set; }
        }
    }
}
