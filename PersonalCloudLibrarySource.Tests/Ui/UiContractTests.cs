using NUnit.Framework;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Ui
{
    [TestFixture]
    public class UiContractTests
    {
        private const string WideLogoPackUri = "/PersonalCloudLibrarySource;component/Assets/pcls-logo-wide.png";

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
        public void BrandArtwork_UsesDirectReferencePngs()
        {
            Assert.That(File.Exists(FindRepositoryFile("PersonalCloudLibrarySource", "icon.png")), Is.True);
            Assert.That(File.Exists(FindRepositoryFile("PersonalCloudLibrarySource", "Assets", "pcls-logo-wide.png")), Is.True);
            Assert.That(File.Exists(FindRepositoryFile("docs", "assets", "pcls-logo-full.png")), Is.True);

            var root = FindRepositoryRoot();
            Assert.That(File.Exists(Path.Combine(root, "PersonalCloudLibrarySource", "Assets", "pcls-icon.svg")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "PersonalCloudLibrarySource", "Assets", "pcls-logo-wide.svg")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "docs", "assets", "pcls-logo-full.svg")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "tools", "decode-brand-assets.ps1")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "tools", "pcls-logo-wide.b64")), Is.False);
            Assert.That(File.Exists(Path.Combine(root, "tools", "apply-0.3.2-assets-note.txt")), Is.False);
            Assert.That(Directory.GetFiles(Path.Combine(root, "tools", "assets"), "pcls-*.part*"), Is.Empty);
        }

        [Test]
        public void BrandArtwork_IsUsedByPrimaryViews()
        {
            var dashboard = File.ReadAllText(
                FindRepositoryFile("PersonalCloudLibrarySource", "Dashboard", "CloudLibraryDashboardView.xaml"));
            var setupWizard = File.ReadAllText(
                FindRepositoryFile("PersonalCloudLibrarySource", "Setup", "SetupWizardView.xaml"));

            StringAssert.Contains(WideLogoPackUri, dashboard);
            StringAssert.Contains(WideLogoPackUri, setupWizard);
        }

        [Test]
        public void RuntimeBrandArtwork_HasExpectedDimensionsAndTransparency()
        {
            AssertTransparentPng(FindRepositoryFile("PersonalCloudLibrarySource", "icon.png"), 512, 512);
            AssertTransparentPng(
                FindRepositoryFile("PersonalCloudLibrarySource", "Assets", "pcls-logo-wide.png"),
                1400,
                420);
        }

        [Test]
        public void DocumentationBrandArtwork_DecodesWithTransparency()
        {
            AssertTransparentPng(FindRepositoryFile("docs", "assets", "pcls-logo-full.png"), 1254, 1254);
        }

        private static void AssertTransparentPng(string path, int expectedWidth, int expectedHeight)
        {
            using (var bitmap = new Bitmap(path))
            {
                Assert.That(bitmap.Width, Is.EqualTo(expectedWidth));
                Assert.That(bitmap.Height, Is.EqualTo(expectedHeight));
                Assert.That(Image.IsAlphaPixelFormat(bitmap.PixelFormat), Is.True);
                Assert.That(bitmap.GetPixel(0, 0).A, Is.EqualTo(0));
                Assert.That(bitmap.GetPixel(bitmap.Width - 1, 0).A, Is.EqualTo(0));
                Assert.That(bitmap.GetPixel(0, bitmap.Height - 1).A, Is.EqualTo(0));
                Assert.That(bitmap.GetPixel(bitmap.Width - 1, bitmap.Height - 1).A, Is.EqualTo(0));

                var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                var data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    Assert.That(data.Scan0, Is.Not.EqualTo(IntPtr.Zero));
                    Assert.That(data.Stride, Is.Not.EqualTo(0));
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
            }
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

        private static string FindRepositoryRoot()
        {
            var solution = FindRepositoryFile("PersonalCloudLibrarySource", "PersonalCloudLibrarySource.sln");
            return Directory.GetParent(Path.GetDirectoryName(solution)).FullName;
        }
    }
}
