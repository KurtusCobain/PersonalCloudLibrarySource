using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class ManifestGenerationCancellationTests
    {
        [Test]
        public void Generate_CancellationDuringDirectoryEnumeration_StopsBeforeFileEnumerationOrWrite()
        {
            var cancellation = new CancellationTokenSource();
            var fileSystem = new CancellingFileSystem(cancellation);
            var service = new ManifestGenerationService(fileSystem);

            Assert.Throws<OperationCanceledException>(() => service.Generate(
                new ManifestGenerationOptions
                {
                    SourceRoot = @"C:\library",
                    OutputPath = @"C:\output\library.json",
                    NoReport = true
                },
                cancellation.Token));

            Assert.That(fileSystem.FileEnumerationStarted, Is.False);
        }

        private sealed class CancellingFileSystem : IManifestGenerationFileSystem
        {
            private readonly CancellationTokenSource cancellation;

            public CancellingFileSystem(CancellationTokenSource cancellation)
            {
                this.cancellation = cancellation;
            }

            public bool FileEnumerationStarted { get; private set; }
            public bool DirectoryExists(string path) => true;

            public IEnumerable<string> EnumerateDirectories(string path)
            {
                yield return path + @"\one";
                cancellation.Cancel();
                yield return path + @"\two";
            }

            public IEnumerable<string> EnumerateFiles(string path)
            {
                FileEnumerationStarted = true;
                yield break;
            }
        }
    }
}
