using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests.Dashboard
{
    [TestFixture]
    public class FriendlySourceNameProviderTests
    {
        [TestCase(null, "Existing manifest file")]
        [TestCase("", "Existing manifest file")]
        [TestCase(PersonalCloudLibrarySourceSettings.LocalFileProviderType, "Existing manifest file")]
        [TestCase(PersonalCloudLibrarySourceSettings.LocalFolderProviderType, "Local, external, or network folder")]
        [TestCase(PersonalCloudLibrarySourceSettings.RcloneRemoteProviderType, "Cloud storage through rclone")]
        [TestCase("FutureProvider", "Unknown source")]
        public void GetDisplayName_ReturnsFriendlyLabel(string providerType, string expected)
        {
            Assert.That(FriendlySourceNameProvider.GetDisplayName(providerType), Is.EqualTo(expected));
        }
    }
}
