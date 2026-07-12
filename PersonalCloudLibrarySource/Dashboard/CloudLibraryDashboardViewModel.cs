using Playnite.SDK;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudLibraryDashboardViewModel : ObservableObject
    {
        private readonly DashboardStateStore stateStore;
        private readonly PluginNavigationService navigationService;

        public CloudLibraryDashboardViewModel(
            DashboardStateStore stateStore,
            PluginNavigationService navigationService)
        {
            this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            this.stateStore.PropertyChanged += StateStore_PropertyChanged;

            OpenSettingsCommand = new DelegateCommand(navigationService.OpenSettings);
            VerifyLibraryCommand = new DelegateCommand(navigationService.VerifyLibrary);
            OpenCacheFolderCommand = new DelegateCommand(navigationService.OpenCacheFolder);
            OpenLatestReportCommand = new DelegateCommand(navigationService.OpenLatestReport);
            GenerateManifestCommand = new DelegateCommand(navigationService.GenerateManifest);
            ShowUpdateLibraryInstructionsCommand = new DelegateCommand(navigationService.ShowUpdateLibraryInstructions);
            OpenSourceLocationCommand = new DelegateCommand(navigationService.OpenSourceLocation);
        }

        public CloudLibraryDashboardState State => stateStore.Current;
        public string StatusText => State?.StatusText ?? "Needs setup";
        public string SourceTypeDisplayName => State?.SourceTypeDisplayName ?? FriendlySourceNameProvider.GetDisplayName(null);
        public string SourceDescription => EmptyFallback(State?.SourceDescription);
        public string ManifestDescription => EmptyFallback(State?.ManifestDescription);
        public string CachePath => EmptyFallback(State?.CachePath);
        public int ManifestItemCount => State?.ManifestItemCount ?? 0;
        public int ImportedGameCount => State?.ImportedGameCount ?? 0;
        public int CachedGameCount => State?.CachedGameCount ?? 0;
        public int WarningCount => State?.WarningCount ?? 0;
        public int ActiveTransferCount => State?.ActiveTransferCount ?? 0;
        public int FailedTransferCount => State?.FailedTransferCount ?? 0;

        public ICommand OpenSettingsCommand { get; }
        public ICommand VerifyLibraryCommand { get; }
        public ICommand OpenCacheFolderCommand { get; }
        public ICommand OpenLatestReportCommand { get; }
        public ICommand GenerateManifestCommand { get; }
        public ICommand ShowUpdateLibraryInstructionsCommand { get; }
        public ICommand OpenSourceLocationCommand { get; }

        private static string EmptyFallback(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Not configured" : value;
        }

        private void StateStore_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DashboardStateStore.Current))
            {
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(SourceTypeDisplayName));
                OnPropertyChanged(nameof(SourceDescription));
                OnPropertyChanged(nameof(ManifestDescription));
                OnPropertyChanged(nameof(CachePath));
                OnPropertyChanged(nameof(ManifestItemCount));
                OnPropertyChanged(nameof(ImportedGameCount));
                OnPropertyChanged(nameof(CachedGameCount));
                OnPropertyChanged(nameof(WarningCount));
                OnPropertyChanged(nameof(ActiveTransferCount));
                OnPropertyChanged(nameof(FailedTransferCount));
            }
        }
    }

    internal sealed class DelegateCommand : ICommand
    {
        private readonly Action execute;
        private readonly Func<bool> canExecute;

        public DelegateCommand(Action execute, Func<bool> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute();
        }

        public void Execute(object parameter)
        {
            execute();
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
