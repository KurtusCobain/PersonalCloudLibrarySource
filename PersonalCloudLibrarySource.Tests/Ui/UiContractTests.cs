using NUnit.Framework;
using System;
using System.IO;
using System.Linq;

namespace PersonalCloudLibrarySource.Tests.Ui
{
    [TestFixture]
    public class UiContractTests
    {
        [Test]
        public void SetupWizard_UsesPlayniteThemeForeground()
        {
            var xaml = File.ReadAllText(FindRepositoryFile("PersonalCloudLibrarySource", "Setup", "SetupWizardView.xaml"));

            StringAssert.Contains("Foreground=\"{DynamicResource TextBrush}\"", xaml);
        }

        [Test]
        public void SourceSettings_ShowOnlyFieldsForSelectedProvider()
        {
            var xaml = File.ReadAllText(FindRepositoryFile("PersonalCloudLibrarySource", "PersonalCloudLibrarySourceSettingsView.xaml"));

            StringAssert.Contains("Value=\"LocalFile\"", xaml);
            StringAssert.Contains("Value=\"LocalFolder\"", xaml);
            StringAssert.Contains("Value=\"RcloneRemote\"", xaml);
            Assert.That(CountOccurrences(xaml, "<DataTrigger"), Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void BrandAssets_ArePresentAndEncodedPngsUseTransparency()
        {
            var iconArtwork = FindRepositoryFile("PersonalCloudLibrarySource", "Assets", "pcls-icon.svg");
            var wideArtwork = FindRepositoryFile("PersonalCloudLibrarySource", "Assets", "pcls-logo-wide.svg");
            var fullArtwork = FindRepositoryFile("docs", "assets", "pcls-logo-full.svg");
            var iconBase64Path = FindRepositoryFile("tools", "pcls-icon-flat.b64");
            var widePartsDirectory = Path.GetDirectoryName(FindRepositoryFile("tools", "assets", "pcls-logo-wide.part01"));

            StringAssert.Contains("<svg", File.ReadAllText(iconArtwork));
            StringAssert.Contains("<svg", File.ReadAllText(wideArtwork));
            StringAssert.Contains("<svg", File.ReadAllText(fullArtwork));

            var iconBytes = Convert.FromBase64String(File.ReadAllText(iconBase64Path).Trim());
            var wideBase64 = string.Concat(
                Directory.GetFiles(widePartsDirectory, "pcls-logo-wide.part*")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => File.ReadAllText(path).Trim()));
            var wideBytes = Convert.FromBase64String(wideBase64);

            Assert.That(ReadPngColorType(iconBytes), Is.EqualTo(6), "Encoded icon.png must use truecolor alpha.");

            var wideColorType = ReadPngColorType(wideBytes);
            var wideHasTransparency = wideColorType == 6 ||
                (wideColorType == 3 && ContainsChunk(wideBytes, new byte[] { 116, 82, 78, 83 }));
            Assert.That(
                wideHasTransparency,
                Is.True,
                "Encoded wide logo must use truecolor alpha or an indexed palette with a tRNS transparency chunk.");
        }

        private static int CountOccurrences(string value, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private static byte ReadPngColorType(byte[] bytes)
        {
            Assert.That(bytes, Is.Not.Null);
            Assert.That(bytes.Length, Is.GreaterThanOrEqualTo(26));

            var pngSignature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (var index = 0; index < pngSignature.Length; index++)
            {
                Assert.That(bytes[index], Is.EqualTo(pngSignature[index]), "Invalid PNG signature.");
            }

            return bytes[25];
        }

        private static bool ContainsChunk(byte[] bytes, byte[] chunkName)
        {
            for (var index = 0; index <= bytes.Length - chunkName.Length; index++)
            {
                var matches = true;
                for (var offset = 0; offset < chunkName.Length; offset++)
                {
                    if (bytes[index + offset] != chunkName[offset])
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return true;
                }
            }

            return false;
        }

        private static string FindRepositoryFile(params string[] segments)
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                var path = directory.FullName;
                foreach (var segment in segments)
                {
                    path = Path.Combine(path, segment);
                }

                if (File.Exists(path))
                {
                    return path;
                }

                directory = directory.Parent;
            }

            Assert.Fail("Repository file was not found: " + string.Join("/", segments));
            return string.Empty;
        }
    }
}
