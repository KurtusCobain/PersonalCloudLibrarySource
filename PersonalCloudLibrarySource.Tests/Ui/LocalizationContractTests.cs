using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace PersonalCloudLibrarySource.Tests.Ui
{
    [TestFixture]
    public class LocalizationContractTests
    {
        private static readonly string[] UserFacingXamlAttributes =
            { "Content", "Header", "Text", "Title", "ToolTip" };

        [Test]
        public void LocalizationKeys_AreUnique()
        {
            var keys = ReadResources().Select(resource => resource.Key).ToList();
            Assert.That(keys.Count, Is.EqualTo(keys.Distinct(StringComparer.Ordinal).Count()),
                "Localization keys must be unique.");
        }

        [Test]
        public void EveryLocalizationReference_ResolvesToOneResource()
        {
            var resources = ReadResources().GroupBy(resource => resource.Key)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var missing = new List<string>();
            foreach (var file in SourceFiles("*.cs").Concat(SourceFiles("*.xaml")))
            {
                if (file.EndsWith(Path.Combine("Localization", "en_US.xaml"), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (Match match in Regex.Matches(File.ReadAllText(file), @"LOCPLS[A-Za-z0-9_]+"))
                {
                    int count;
                    if (!resources.TryGetValue(match.Value, out count) || count != 1)
                    {
                        missing.Add(Path.GetFileName(file) + ": " + match.Value);
                    }
                }
            }

            Assert.That(missing, Is.Empty, string.Join(Environment.NewLine, missing));
        }

        [Test]
        public void FormattedLocalizationUsages_MatchResourcePlaceholders()
        {
            var resources = ReadResources().ToDictionary(resource => resource.Key, resource => resource.Value);
            var failures = new List<string>();
            var pattern = new Regex("PclsResources\\.Format\\(\\s*\\\"(?<key>LOCPLS[A-Za-z0-9_]+)\\\"\\s*,\\s*\\\"(?<fallback>(?:\\\\.|[^\\\"])*)\\\"");
            foreach (var file in SourceFiles("*.cs"))
            {
                foreach (Match match in pattern.Matches(File.ReadAllText(file)))
                {
                    var key = match.Groups["key"].Value;
                    var fallback = Regex.Unescape(match.Groups["fallback"].Value);
                    if (!resources.ContainsKey(key) || !Placeholders(resources[key]).SetEquals(Placeholders(fallback)))
                    {
                        failures.Add(Path.GetFileName(file) + ": " + key);
                    }
                }
            }

            Assert.That(failures, Is.Empty, "Format placeholders differ: " + string.Join(", ", failures));
        }

        [Test]
        public void FormattedLocalizationUsages_SupplyEveryReferencedArgument()
        {
            var failures = new List<string>();
            foreach (var file in SourceFiles("*.cs"))
            {
                var source = File.ReadAllText(file);
                foreach (var invocation in FindFormatInvocations(source))
                {
                    var fallbackPlaceholders = Placeholders(invocation.Arguments[1]);
                    var suppliedArgumentCount = invocation.Arguments.Count - 2;
                    if (fallbackPlaceholders.Any(index => index >= suppliedArgumentCount))
                    {
                        failures.Add(Path.GetFileName(file) + ": " + invocation.Arguments[0]);
                    }
                }
            }

            Assert.That(failures, Is.Empty,
                "Format calls do not supply every referenced argument: " + string.Join(", ", failures));
        }

        [Test]
        public void XamlUserFacingAttributes_AreResourceDriven()
        {
            var failures = new List<string>();
            foreach (var file in SourceFiles("*.xaml"))
            {
                if (file.EndsWith(Path.Combine("Localization", "en_US.xaml"), StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith("App.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var document = new XmlDocument();
                document.Load(file);
                foreach (var attributeName in UserFacingXamlAttributes)
                {
                    foreach (XmlAttribute attribute in document.SelectNodes("//@" + attributeName))
                    {
                        var value = attribute.Value ?? string.Empty;
                        if (value.Length > 0 && !value.StartsWith("{", StringComparison.Ordinal))
                        {
                            failures.Add(Path.GetFileName(file) + ": " + attributeName + "=\"" + value + "\"");
                        }
                    }
                }
            }

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        }

        [Test]
        public void CSharpUserFeedbackSinks_AreResourceDriven()
        {
            var failures = new List<string>();
            var userFacingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "PersonalCloudLibrarySource.GameCommands.cs",
                "PersonalCloudLibrarySource.Navigation.cs",
                "PersonalCloudLibrarySourceSettings.cs",
                "PersonalCloudLibrarySourceSettingsV3ViewModel.cs",
                "PersonalCloudLibraryUninstallController.cs",
                "RcloneInstallController.cs",
                "SetupWizardWindowService.cs",
                "TransferActivityTracker.cs"
            };
            var directLiteralPatterns = new[]
            {
                "MessageBox\\.Show\\(\\s*\\\"",
                "\\.ShowMessage\\(\\s*\\\"",
                "\\.ShowErrorMessage\\(\\s*\\\"",
                "\\bName\\s*=\\s*\\\"(?:Download|Remove)",
                "SetupStatusHeadline\\s*=\\s*\\\"",
                "\\bMessage\\s*=\\s*\\\""
            };

            foreach (var file in SourceFiles("*.cs"))
            {
                if (!userFacingFiles.Contains(Path.GetFileName(file)))
                {
                    continue;
                }
                var source = File.ReadAllText(file);
                foreach (var pattern in directLiteralPatterns)
                {
                    if (Regex.IsMatch(source, pattern, RegexOptions.Singleline))
                    {
                        failures.Add(Path.GetFileName(file) + ": " + pattern);
                    }
                }
            }

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        }

        [Test]
        public void ExplicitReleaseFacingCSharpSinks_AreResourceDriven()
        {
            var failures = new List<string>();
            AssertNoMatch(
                "PersonalCloudLibrarySourceSettings.cs",
                new[]
                {
                    "BrowseForFolder\\(\\s*\\\"",
                    "OpenFileInExplorer\\([^,]+,\\s*\\\"",
                    "details\\.AppendLine\\(\\s*\\\"",
                    "SetupStatusDetails\\s*=\\s*\\\"",
                    "SetupStatusDetails\\s*=\\s*(?:\\r?\\n\\s*)?\\\"",
                    "\\\"Manifest load: \\\"\\s*\\+",
                    "\\\"Items found: \\\"\\s*\\+",
                    "\\\"Items detected: \\\"\\s*\\+"
                },
                failures);
            AssertLocalizedInvocationArguments(
                "PersonalCloudLibrarySource.GameCommands.cs",
                "CopyText(",
                1,
                failures);
            AssertNoMatch(
                Path.Combine("Dashboard", "CloudLibraryDashboardViewModel.cs"),
                new[]
                {
                    "StatusText\\s*=>[^;]*\\?\\?\\s*\\\"",
                    "EmptyFallback[\\s\\S]*?\\?\\s*\\\""
                },
                failures);
            AssertNoMatch(
                Path.Combine("Dashboard", "CloudTransferQueueItemViewModel.cs"),
                new[] { "state\\.ToString\\(\\)" },
                failures);

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        }

        [Test]
        public void OpenFileDialogFilters_LocalizeLabelsAndKeepPatternsInCode()
        {
            var failures = new List<string>();
            foreach (var relativePath in new[]
            {
                "PersonalCloudLibrarySourceSettings.cs",
                Path.Combine("Setup", "SetupWizardView.xaml.cs")
            })
            {
                var source = File.ReadAllText(FindRepositoryFile("PersonalCloudLibrarySource", relativePath));
                if (Regex.IsMatch(source, "Filter\\s*=\\s*\\\""))
                {
                    failures.Add(relativePath + ": literal Filter assignment");
                }
                if (!source.Contains("PclsResources.Format") ||
                    !source.Contains("\"*.json\"") ||
                    !source.Contains("\"rclone.exe\"") ||
                    !source.Contains("\"*.exe\"") ||
                    !source.Contains("\"*.*\""))
                {
                    failures.Add(relativePath + ": localized filter with fixed patterns is missing");
                }
            }

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        }

        [Test]
        public void TranslatorOwnedResources_DoNotContainFixedIdentifiers()
        {
            var identifiers = new[]
            {
                "LocalFile", "LocalFolder", "RcloneRemote", "launchFile", "cachePath",
                "installDirectory", "LocalCacheFolder", "ManifestRelativePath", "LocalLibraryRoot"
            };
            var failures = ReadResources()
                .Where(resource => identifiers.Any(identifier => resource.Value.Contains(identifier)))
                .Select(resource => resource.Key)
                .ToList();

            Assert.That(failures, Is.Empty,
                "Fixed identifiers must be supplied by code: " + string.Join(", ", failures));
        }

        private static void AssertNoMatch(string relativePath, IEnumerable<string> patterns, ICollection<string> failures)
        {
            var source = File.ReadAllText(FindRepositoryFile("PersonalCloudLibrarySource", relativePath));
            foreach (var pattern in patterns)
            {
                if (Regex.IsMatch(source, pattern, RegexOptions.Singleline))
                {
                    failures.Add(relativePath + ": " + pattern);
                }
            }
        }

        private static void AssertLocalizedInvocationArguments(
            string relativePath,
            string marker,
            int userTextArgumentIndex,
            ICollection<string> failures)
        {
            var source = File.ReadAllText(FindRepositoryFile("PersonalCloudLibrarySource", relativePath));
            foreach (var invocation in FindInvocations(source, marker))
            {
                if (invocation.Arguments.Count > 0 &&
                    invocation.Arguments[0].TrimStart().StartsWith("string ", StringComparison.Ordinal))
                {
                    continue;
                }
                if (invocation.Arguments.Count <= userTextArgumentIndex ||
                    !invocation.Arguments[userTextArgumentIndex].TrimStart().StartsWith("PclsResources.", StringComparison.Ordinal))
                {
                    failures.Add(relativePath + ": " + marker + " argument " + userTextArgumentIndex);
                }
            }
        }

        private static IEnumerable<FormatInvocation> FindFormatInvocations(string source)
        {
            return FindInvocations(source, "PclsResources.Format(");
        }

        private static IEnumerable<FormatInvocation> FindInvocations(string source, string marker)
        {
            var searchIndex = 0;
            while ((searchIndex = source.IndexOf(marker, searchIndex, StringComparison.Ordinal)) >= 0)
            {
                var arguments = new List<string>();
                var current = new System.Text.StringBuilder();
                var depth = 1;
                var inString = false;
                var escaped = false;
                var index = searchIndex + marker.Length;
                for (; index < source.Length && depth > 0; index++)
                {
                    var character = source[index];
                    if (inString)
                    {
                        if (escaped)
                        {
                            current.Append(character);
                            escaped = false;
                        }
                        else if (character == '\\')
                        {
                            escaped = true;
                        }
                        else if (character == '"')
                        {
                            inString = false;
                        }
                        else
                        {
                            current.Append(character);
                        }
                        continue;
                    }

                    if (character == '"')
                    {
                        inString = true;
                    }
                    else if (character == '(')
                    {
                        depth++;
                        current.Append(character);
                    }
                    else if (character == ')')
                    {
                        depth--;
                        if (depth > 0) current.Append(character);
                    }
                    else if (character == ',' && depth == 1)
                    {
                        arguments.Add(current.ToString().Trim());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(character);
                    }
                }

                arguments.Add(current.ToString().Trim());
                if (arguments.Count >= 2)
                {
                    yield return new FormatInvocation(arguments);
                }
                searchIndex = index;
            }
        }

        private sealed class FormatInvocation
        {
            public FormatInvocation(IReadOnlyList<string> arguments) { Arguments = arguments; }
            public IReadOnlyList<string> Arguments { get; }
        }

        private static HashSet<int> Placeholders(string value)
        {
            return new HashSet<int>(Regex.Matches(value ?? string.Empty, @"\{(?<index>\d+)(?:[^}]*)\}")
                .Cast<Match>().Select(match => int.Parse(match.Groups["index"].Value)));
        }

        private static List<KeyValuePair<string, string>> ReadResources()
        {
            var document = new XmlDocument();
            document.Load(FindRepositoryFile("PersonalCloudLibrarySource", "Localization", "en_US.xaml"));
            var result = new List<KeyValuePair<string, string>>();
            foreach (XmlElement element in document.DocumentElement.ChildNodes.OfType<XmlElement>())
            {
                var key = element.GetAttribute("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
                if (!string.IsNullOrEmpty(key))
                {
                    result.Add(new KeyValuePair<string, string>(key, element.InnerText));
                }
            }
            return result;
        }

        private static IEnumerable<string> SourceFiles(string pattern)
        {
            return Directory.GetFiles(FindRepositoryFile("PersonalCloudLibrarySource", "PersonalCloudLibrarySource.csproj").Replace("PersonalCloudLibrarySource.csproj", string.Empty), pattern, SearchOption.AllDirectories)
                .Where(path => path.IndexOf(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0)
                .Where(path => path.IndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) < 0);
        }

        private static string FindRepositoryFile(params string[] segments)
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                var path = segments.Aggregate(directory.FullName, Path.Combine);
                if (File.Exists(path)) return path;
                directory = directory.Parent;
            }
            Assert.Fail("Repository file was not found: " + string.Join("/", segments));
            return string.Empty;
        }
    }
}
