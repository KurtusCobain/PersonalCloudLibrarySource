using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Input;

namespace PersonalCloudLibrarySource
{
    public sealed class CloudLibraryDashboardViewModel : ObservableObject, IDisposable
    {
        private readonly DashboardStateStore stateStore;
        private readonly PluginNavigationService navigationService;
        private readonly CloudTransferManager transferManager;
        private readonly DashboardActivityService activityService;
        private readonly IDashboardTransferActions transferActions;
        private readonly IStartupUiDispatcher dispatcher;
        private readonly CancellationTokenSource lifetimeCancellation = new CancellationTokenSource();
        private readonly CancellationToken lifetimeToken;
        private readonly object refreshSync = new object();
        private IReadOnlyList<CloudTransferQueueItemViewModel> transferQueueItems =
            new List<CloudTransferQueueItemViewModel>().AsReadOnly();
        private IReadOnlyList<DashboardActivityRecord> recentActivity =
            new List<DashboardActivityRecord>().AsReadOnly();
        private bool transferRefreshPending;
        private bool disposed;

        public CloudLibraryDashboardViewModel(
            DashboardStateStore stateStore,
            PluginNavigationService navigationService)
        {
            this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            this.navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            lifetimeToken = lifetimeCancellation.Token;
            this.stateStore.PropertyChanged += StateStore_PropertyChanged;

            OpenSettingsCommand = new DelegateCommand(navigationService.OpenSettings);
            VerifyLibraryCommand = new DelegateCommand(navigationService.VerifyLibrary);
            OpenCacheFolderCommand = new DelegateCommand(navigationService.OpenCacheFolder);
            OpenLatestReportCommand = new DelegateCommand(navigationService.OpenLatestReport);
            GenerateManifestCommand = new DelegateCommand(navigationService.GenerateManifest);
            ShowUpdateLibraryInstructionsCommand = new DelegateCommand(navigationService.ShowUpdateLibraryInstructions);
            OpenSourceLocationCommand = new DelegateCommand(navigationService.OpenSourceLocation);
            RunSetupWizardCommand = new DelegateCommand(navigationService.RunSetupWizard);
        }

        public CloudLibraryDashboardViewModel(
            DashboardStateStore stateStore,
            PluginNavigationService navigationService,
            CloudTransferManager transferManager,
            DashboardActivityService activityService,
            IDashboardTransferActions transferActions,
            IStartupUiDispatcher dispatcher)
            : this(stateStore, navigationService)
        {
            this.transferManager = transferManager ?? throw new ArgumentNullException(nameof(transferManager));
            this.activityService = activityService ?? throw new ArgumentNullException(nameof(activityService));
            this.transferActions = transferActions ?? throw new ArgumentNullException(nameof(transferActions));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            this.transferManager.Changed += TransferManager_Changed;
            this.activityService.Changed += ActivityService_Changed;
            PostImmediate(RefreshTransferPresentation);
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
        public IReadOnlyList<CloudTransferQueueItemViewModel> TransferQueueItems
        {
            get => transferQueueItems;
            private set => SetValue(ref transferQueueItems, value);
        }

        public IReadOnlyList<DashboardActivityRecord> RecentActivity
        {
            get => recentActivity;
            private set => SetValue(ref recentActivity, value);
        }

        public ICommand OpenSettingsCommand { get; }
        public ICommand VerifyLibraryCommand { get; }
        public ICommand OpenCacheFolderCommand { get; }
        public ICommand OpenLatestReportCommand { get; }
        public ICommand GenerateManifestCommand { get; }
        public ICommand ShowUpdateLibraryInstructionsCommand { get; }
        public ICommand OpenSourceLocationCommand { get; }
        public ICommand RunSetupWizardCommand { get; }

        public void Dispose()
        {
            lock (refreshSync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                transferRefreshPending = false;
            }

            stateStore.PropertyChanged -= StateStore_PropertyChanged;
            if (transferManager != null)
            {
                transferManager.Changed -= TransferManager_Changed;
            }

            if (activityService != null)
            {
                activityService.Changed -= ActivityService_Changed;
            }

            lifetimeCancellation.Cancel();
            lifetimeCancellation.Dispose();
        }

        private static string EmptyFallback(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Not configured" : value;
        }

        private void StateStore_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DashboardStateStore.Current))
            {
                if (dispatcher == null)
                {
                    RaiseStatePropertiesChanged();
                }
                else
                {
                    PostImmediate(RaiseStatePropertiesChanged);
                }
            }
        }

        private void TransferManager_Changed(object sender, EventArgs e)
        {
            lock (refreshSync)
            {
                if (disposed || transferRefreshPending)
                {
                    return;
                }

                transferRefreshPending = true;
            }

            var accepted = TryPost(() =>
            {
                lock (refreshSync)
                {
                    transferRefreshPending = false;
                    if (disposed)
                    {
                        return;
                    }
                }

                RefreshTransferPresentation();
            });
            if (!accepted)
            {
                lock (refreshSync)
                {
                    transferRefreshPending = false;
                }
            }
        }

        private void ActivityService_Changed(object sender, EventArgs e)
        {
            PostImmediate(RefreshTransferPresentation);
        }

        private void PostImmediate(Action action)
        {
            lock (refreshSync)
            {
                if (disposed)
                {
                    return;
                }
            }

            TryPost(() =>
            {
                lock (refreshSync)
                {
                    if (disposed)
                    {
                        return;
                    }
                }

                action();
            });
        }

        private bool TryPost(Action action)
        {
            var acknowledgingDispatcher = dispatcher as IAcknowledgingStartupUiDispatcher;
            if (acknowledgingDispatcher != null)
            {
                return acknowledgingDispatcher.TryPost(action, lifetimeToken);
            }

            try
            {
                dispatcher.Post(action, lifetimeToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RefreshTransferPresentation()
        {
            var jobs = transferManager.Jobs
                .Where(job => job != null && job.State != CloudTransferState.Completed)
                .OrderByDescending(job => job.CreatedAt)
                .Select(job => new CloudTransferQueueItemViewModel(
                    job,
                    () => transferActions.Cancel(job.Id),
                    () => transferActions.Retry(job.Id)))
                .ToList()
                .AsReadOnly();

            TransferQueueItems = jobs;
            RecentActivity = activityService.Records.ToList().AsReadOnly();
        }

        private void RaiseStatePropertiesChanged()
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
