using System.Collections.Generic;
using System.Linq;

namespace PersonalCloudLibrarySource
{
    public sealed class GameCommandContext
    {
        public bool BelongsToPlugin { get; set; }
        public bool HasManifestItem { get; set; }
        public bool IsInstalled { get; set; }
        public bool CanInstall { get; set; }
        public bool HasCachedPath { get; set; }
        public bool CanRemoveCachedCopy { get; set; }
        public bool HasSourcePath { get; set; }
        public bool CanOpenSourceLocation { get; set; }
    }

    public sealed class GameCommandAvailability
    {
        public bool ShowPluginMenu { get; set; }
        public bool IsSingleSelection { get; set; }
        public bool CanViewDetails { get; set; }
        public bool CanInstallSelected { get; set; }
        public bool CanOpenCachedFolder { get; set; }
        public bool CanOpenSourceLocation { get; set; }
        public bool CanVerifySelected { get; set; }
        public bool CanCopySourcePaths { get; set; }
        public bool CanCopyCachePath { get; set; }
        public bool CanRemoveSelectedCachedCopies { get; set; }
    }

    public sealed class GameCommandPolicyService
    {
        public GameCommandAvailability Evaluate(IEnumerable<GameCommandContext> sourceContexts)
        {
            var contexts = sourceContexts?.ToList() ?? new List<GameCommandContext>();
            if (contexts.Count == 0 || contexts.Any(context => context == null || !context.BelongsToPlugin))
            {
                return new GameCommandAvailability();
            }

            var single = contexts.Count == 1;
            var selected = single ? contexts[0] : null;

            return new GameCommandAvailability
            {
                ShowPluginMenu = true,
                IsSingleSelection = single,
                CanViewDetails = single && selected.HasManifestItem,
                CanInstallSelected = contexts.Any(context => context.CanInstall),
                CanOpenCachedFolder = single && selected.HasCachedPath,
                CanOpenSourceLocation = single && selected.CanOpenSourceLocation,
                CanVerifySelected = contexts.All(context => context.HasManifestItem),
                CanCopySourcePaths = contexts.All(context => context.HasSourcePath),
                CanCopyCachePath = single && selected.HasCachedPath,
                CanRemoveSelectedCachedCopies = contexts.All(context => context.HasCachedPath && context.CanRemoveCachedCopy)
            };
        }
    }
}
