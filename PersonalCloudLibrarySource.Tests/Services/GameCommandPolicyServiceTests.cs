using NUnit.Framework;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class GameCommandPolicyServiceTests
    {
        private readonly GameCommandPolicyService service = new GameCommandPolicyService();

        [Test]
        public void Evaluate_NoSelection_HidesPluginCommands()
        {
            var result = service.Evaluate(new GameCommandContext[0]);

            Assert.That(result.ShowPluginMenu, Is.False);
        }

        [Test]
        public void Evaluate_MixedLibrarySelection_HidesPluginCommands()
        {
            var result = service.Evaluate(new[]
            {
                new GameCommandContext { BelongsToPlugin = true },
                new GameCommandContext { BelongsToPlugin = false }
            });

            Assert.That(result.ShowPluginMenu, Is.False);
            Assert.That(result.CanInstallSelected, Is.False);
            Assert.That(result.CanRemoveSelectedCachedCopies, Is.False);
        }

        [Test]
        public void Evaluate_RemoteOnlySingleGame_OffersInstallButNotCacheActions()
        {
            var result = service.Evaluate(new[]
            {
                new GameCommandContext
                {
                    BelongsToPlugin = true,
                    HasManifestItem = true,
                    IsInstalled = false,
                    CanInstall = true,
                    HasSourcePath = true,
                    CanOpenSourceLocation = false
                }
            });

            Assert.That(result.ShowPluginMenu, Is.True);
            Assert.That(result.IsSingleSelection, Is.True);
            Assert.That(result.CanViewDetails, Is.True);
            Assert.That(result.CanInstallSelected, Is.True);
            Assert.That(result.CanOpenCachedFolder, Is.False);
            Assert.That(result.CanRemoveSelectedCachedCopies, Is.False);
            Assert.That(result.CanCopySourcePaths, Is.True);
        }

        [Test]
        public void Evaluate_CachedSingleGame_OffersCacheActionsButNotInstall()
        {
            var result = service.Evaluate(new[]
            {
                new GameCommandContext
                {
                    BelongsToPlugin = true,
                    HasManifestItem = true,
                    IsInstalled = true,
                    HasCachedPath = true,
                    CanRemoveCachedCopy = true,
                    HasSourcePath = true,
                    CanOpenSourceLocation = true
                }
            });

            Assert.That(result.CanInstallSelected, Is.False);
            Assert.That(result.CanOpenCachedFolder, Is.True);
            Assert.That(result.CanRemoveSelectedCachedCopies, Is.True);
            Assert.That(result.CanOpenSourceLocation, Is.True);
            Assert.That(result.CanCopyCachePath, Is.True);
        }

        [Test]
        public void Evaluate_MultiSelection_AllowsInstallableSubsetButRequiresEveryGameSafeForBatchRemoval()
        {
            var result = service.Evaluate(new[]
            {
                new GameCommandContext
                {
                    BelongsToPlugin = true,
                    HasManifestItem = true,
                    IsInstalled = false,
                    CanInstall = true,
                    HasSourcePath = true
                },
                new GameCommandContext
                {
                    BelongsToPlugin = true,
                    HasManifestItem = true,
                    IsInstalled = true,
                    HasCachedPath = true,
                    CanRemoveCachedCopy = true,
                    HasSourcePath = true
                }
            });

            Assert.That(result.ShowPluginMenu, Is.True);
            Assert.That(result.IsSingleSelection, Is.False);
            Assert.That(result.CanInstallSelected, Is.True);
            Assert.That(result.CanRemoveSelectedCachedCopies, Is.False);
            Assert.That(result.CanVerifySelected, Is.True);
            Assert.That(result.CanCopySourcePaths, Is.True);
        }

        [Test]
        public void Evaluate_AllCachedMultiSelection_AllowsSafeBatchRemoval()
        {
            var result = service.Evaluate(new[]
            {
                CachedContext(),
                CachedContext()
            });

            Assert.That(result.CanRemoveSelectedCachedCopies, Is.True);
            Assert.That(result.CanOpenCachedFolder, Is.False);
            Assert.That(result.CanViewDetails, Is.False);
        }

        private static GameCommandContext CachedContext()
        {
            return new GameCommandContext
            {
                BelongsToPlugin = true,
                HasManifestItem = true,
                IsInstalled = true,
                HasCachedPath = true,
                CanRemoveCachedCopy = true,
                HasSourcePath = true
            };
        }
    }
}
