using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests.Transfers
{
    [TestFixture]
    public class RcloneCommandBuilderTests
    {
        [Test]
        public void BuildArguments_FileTransfer_UsesCopyToAndQuotedPartialDestination()
        {
            var request = new RcloneTransferRequest
            {
                RemoteName = "games",
                RemoteSourcePath = "library/PC/game.zip",
                DestinationPath = @"C:\Cache\game.zip.pcls-partial",
                IsDirectory = false
            };

            var arguments = RcloneCommandBuilder.BuildArguments(request);

            Assert.That(arguments, Does.StartWith("copyto "));
            Assert.That(arguments, Does.Contain("\"games:library/PC/game.zip\""));
            Assert.That(arguments, Does.Contain("\"C:\\Cache\\game.zip.pcls-partial\""));
            Assert.That(arguments, Does.Contain("--stats=1s"));
            Assert.That(arguments, Does.Contain("--stats-one-line"));
            Assert.That(arguments, Does.Contain("--progress"));
        }

        [Test]
        public void BuildArguments_DirectoryTransfer_UsesCopyAndCreateEmptyDirectories()
        {
            var request = new RcloneTransferRequest
            {
                RemoteName = "games:",
                RemoteSourcePath = "/library/Disc Game",
                DestinationPath = @"D:\Cache\Disc Game.pcls-partial",
                IsDirectory = true
            };

            var arguments = RcloneCommandBuilder.BuildArguments(request);

            Assert.That(arguments, Does.StartWith("copy "));
            Assert.That(arguments, Does.Contain("\"games:library/Disc Game\""));
            Assert.That(arguments, Does.Contain("--create-empty-src-dirs"));
        }
    }
}
