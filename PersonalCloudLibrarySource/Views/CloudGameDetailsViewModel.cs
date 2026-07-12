using System;
using System.Windows.Input;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudGameDetailsViewModel
    {
        public CloudGameDetailsViewModel(
            GameCommandTarget target,
            Action install,
            Action removeCachedCopy,
            Action openCachedFolder,
            Action openSourceLocation,
            Action verify,
            Action copySourcePath,
            Action copyCachePath)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var item = target.Item;
            Title = item?.Title ?? target.Game?.Name ?? "Unknown game";
            Platform = EmptyFallback(item?.Platform);
            InstallationState = target.PolicyContext?.HasCachedPath == true || target.Game?.IsInstalled == true
                ? "Cached locally"
                : "Available from source";
            SourceType = EmptyFallback(item?.SourceType);
            SourcePath = EmptyFallback(target.SourceDisplayPath);
            CachePath = EmptyFallback(target.CacheDisplayPath);
            InstallDirectory = EmptyFallback(target.InstallDirectory);
            PackageRole = EmptyFallback(item?.PackageRole);
            Notes = EmptyFallback(item?.Notes);
            ManifestId = EmptyFallback(item?.Id);

            InstallCommand = new DelegateCommand(install ?? (() => { }), () => target.PolicyContext?.CanInstall == true);
            RemoveCachedCopyCommand = new DelegateCommand(removeCachedCopy ?? (() => { }), () => target.PolicyContext?.CanRemoveCachedCopy == true);
            OpenCachedFolderCommand = new DelegateCommand(openCachedFolder ?? (() => { }), () => target.PolicyContext?.HasCachedPath == true);
            OpenSourceLocationCommand = new DelegateCommand(openSourceLocation ?? (() => { }), () => target.PolicyContext?.CanOpenSourceLocation == true);
            VerifyCommand = new DelegateCommand(verify ?? (() => { }), () => item != null);
            CopySourcePathCommand = new DelegateCommand(copySourcePath ?? (() => { }), () => !string.IsNullOrWhiteSpace(target.SourceDisplayPath));
            CopyCachePathCommand = new DelegateCommand(copyCachePath ?? (() => { }), () => !string.IsNullOrWhiteSpace(target.CacheDisplayPath));
        }

        public string Title { get; }
        public string Platform { get; }
        public string InstallationState { get; }
        public string SourceType { get; }
        public string SourcePath { get; }
        public string CachePath { get; }
        public string InstallDirectory { get; }
        public string PackageRole { get; }
        public string Notes { get; }
        public string ManifestId { get; }

        public ICommand InstallCommand { get; }
        public ICommand RemoveCachedCopyCommand { get; }
        public ICommand OpenCachedFolderCommand { get; }
        public ICommand OpenSourceLocationCommand { get; }
        public ICommand VerifyCommand { get; }
        public ICommand CopySourcePathCommand { get; }
        public ICommand CopyCachePathCommand { get; }

        private static string EmptyFallback(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Not available" : value;
        }
    }
}
