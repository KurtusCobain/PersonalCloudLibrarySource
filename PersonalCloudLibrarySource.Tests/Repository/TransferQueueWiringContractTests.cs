using NUnit.Framework;
using System;
using System.IO;

namespace PersonalCloudLibrarySource.Tests.Repository
{
    [TestFixture]
    public class TransferQueueWiringContractTests
    {
        [Test]
        public void ProductionTransferCallersUseQueueAndContainNoDetachedTaskRun()
        {
            var root = FindRepositoryRoot();
            var commands = File.ReadAllText(Path.Combine(root, "PersonalCloudLibrarySource", "PersonalCloudLibrarySource.GameCommands.cs"));
            var plugin = File.ReadAllText(Path.Combine(root, "PersonalCloudLibrarySource", "PersonalCloudLibrarySource.cs"));
            var controller = File.ReadAllText(Path.Combine(root, "PersonalCloudLibrarySource", "RcloneInstallController.cs"));

            Assert.That(commands, Does.Not.Contain("Task.Run"));
            Assert.That(commands, Does.Contain("GetTransferQueue().Retry"));
            Assert.That(plugin, Does.Contain("GetTransferQueue()"));
            Assert.That(controller, Does.Contain("transferQueue.Enqueue"));
            Assert.That(controller, Does.Contain("transferQueue.GetCompletion"));
        }

        private static string FindRepositoryRoot()
        {
            var current = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                    File.Exists(Path.Combine(current.FullName, ".git")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException("Repository root was not found.");
        }
    }
}
