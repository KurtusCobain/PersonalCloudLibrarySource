using NUnit.Framework;
using System;
using System.IO;

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
        public void BrandAssets_ArePresentAndBuildOutputUsesAlphaPngs()
        {
            var iconArtwork = FindRepositoryFile("PersonalCloudLibrarySource", "Assets", "pcls-icon.svg");
            var wideArtwork = FindRepositoryFile("PersonalCloudLibrarySource", "Assets", "pcls-logo-wide.svg");
            var fullArtwork = FindRepositoryFile("docs", "assets", "pcls-logo-full.svg");
            var iconPath = FindRepositoryFile("PersonalCloudLibrarySource", "bin", "Debug", "icon.png");
            var wideLogoPath = FindRepositoryFile("PersonalCloudLibrarySource", "bin", "Debug", "Assets", "pcls-logo-wide.png");

            StringAssert.Contains("<svg", File.ReadAllText(iconArtwork));
            StringAssert.Contains("<svg", File.ReadAllText(wideArtwork));
            StringAssert.Contains("<svg", File.ReadAllText(fullArtwork));
            Assert.That(ReadPngColorType(iconPath), Is.EqualTo(6), "Generated icon.png must be a truecolor PNG with alpha.");
            Assert.That(ReadPngColorType(wideLogoPath), Is.EqualTo(6), "Generated wide logo must be a truecolor PNG with alpha.");
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

        private static byte ReadPngColorType(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                var header = new byte[26];
                if (stream.Read(header, 0, header.Length) != header.Length)
                {
                    Assert.Fail("PNG file is too short: " + path);
                }

                var pngSignature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
                for (var index = 0; index < pngSignature.Length; index++)
                {
                    Assert.That(header[index], Is.EqualTo(pngSignature[index]), "Invalid PNG signature: " + path);
                }

                return header[25];
            }
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
