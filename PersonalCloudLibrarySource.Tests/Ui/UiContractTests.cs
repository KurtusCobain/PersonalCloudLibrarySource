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
        public void BrandArtwork_IsPresent()
        {
            StringAssert.Contains("<svg", File.ReadAllText(FindRepositoryFile("PersonalCloudLibrarySource", "Assets", "pcls-icon.svg")));
            StringAssert.Contains("<svg", File.ReadAllText(FindRepositoryFile("PersonalCloudLibrarySource", "Assets", "pcls-logo-wide.svg")));
            StringAssert.Contains("<svg", File.ReadAllText(FindRepositoryFile("docs", "assets", "pcls-logo-full.svg")));
        }

        [Test]
        public void EncodedIcon_UsesAlpha()
        {
            var bytes = Convert.FromBase64String(
                File.ReadAllText(FindRepositoryFile("tools", "pcls-icon-flat.b64")).Trim());
            Assert.That(ReadPngColorType(bytes), Is.EqualTo(6));
        }

        [Test]
        public void EncodedWideLogo_Decodes()
        {
            var bytes = ReadWideLogoBytes();
            Assert.That(bytes.Length, Is.GreaterThan(26));
            AssertPngSignature(bytes);
        }

        [Test]
        public void EncodedWideLogo_UsesTransparency()
        {
            var bytes = ReadWideLogoBytes();
            var colorType = ReadPngColorType(bytes);
            var transparent = colorType == 6 ||
                (colorType == 3 && ContainsChunk(bytes, new byte[] { 116, 82, 78, 83 }));
            Assert.That(transparent, Is.True);
        }

        private static byte[] ReadWideLogoBytes()
        {
            var directory = Path.GetDirectoryName(
                FindRepositoryFile("tools", "assets", "pcls-logo-wide.part01"));
            var base64 = string.Concat(
                Directory.GetFiles(directory, "pcls-logo-wide.part*")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => File.ReadAllText(path).Trim()));
            return Convert.FromBase64String(base64);
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
            AssertPngSignature(bytes);
            return bytes[25];
        }

        private static void AssertPngSignature(byte[] bytes)
        {
            var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (var index = 0; index < signature.Length; index++)
            {
                Assert.That(bytes[index], Is.EqualTo(signature[index]));
            }
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
