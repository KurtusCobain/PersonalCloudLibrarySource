using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PersonalCloudLibrarySource
{
    public sealed class StartupActionContext
    {
        public bool PluginEnabled { get; set; }
        public bool SetupValid { get; set; }
        public SetupLaunchAction SetupAction { get; set; }
        public bool GenerateManifest { get; set; }
        public bool ManifestGenerationEligible { get; set; }
        public bool RefreshStatus { get; set; }
        public bool OpenDashboard { get; set; }
    }

    public interface IStartupActionSink
    {
        void OpenSetupWizard(CancellationToken cancellationToken);
        void ShowSetupReminder(CancellationToken cancellationToken);
        void GenerateManifest(CancellationToken cancellationToken);
        void RefreshStatus(CancellationToken cancellationToken);
        void OpenDashboard(CancellationToken cancellationToken);
        void ReportFailure(Exception exception, CancellationToken cancellationToken);
    }

    public sealed class StartupActionService : IDisposable
    {
        private readonly IStartupActionSink sink;
        private readonly object sync = new object();
        private readonly Action<Exception> observeFault;
        private CancellationTokenSource cancellation;
        private Task startupTask;
        private bool disposed;

        public StartupActionService(
            IStartupActionSink sink,
            Action<Exception> observeFault = null)
        {
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            this.observeFault = observeFault ?? (exception => Trace.TraceError(exception.ToString()));
        }

        public Task Start(StartupActionContext context)
        {
            lock (sync)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(StartupActionService));
                }

                if (startupTask != null)
                {
                    return startupTask;
                }

                var ownedCancellation = new CancellationTokenSource();
                var cancellationToken = ownedCancellation.Token;
                cancellation = ownedCancellation;
                startupTask = Task.Run(() => RunCore(context, cancellationToken));
                return startupTask;
            }
        }

        public bool Stop(TimeSpan timeout)
        {
            Task observedTask;
            lock (sync)
            {
                cancellation?.Cancel();
                observedTask = startupTask;
            }

            if (observedTask == null)
            {
                return true;
            }

            try
            {
                return observedTask.Wait(timeout);
            }
            catch (AggregateException ex)
            {
                ObserveFault(ex.Flatten());
                return true;
            }
        }

        public void Dispose()
        {
            CancellationTokenSource ownedCancellation;
            Task observedTask;
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                ownedCancellation = cancellation;
                cancellation = null;
                observedTask = startupTask;
            }

            if (ownedCancellation == null)
            {
                return;
            }

            ownedCancellation.Cancel();
            if (observedTask == null || observedTask.IsCompleted)
            {
                ownedCancellation.Dispose();
                return;
            }

            observedTask.ContinueWith(
                _ => ownedCancellation.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void RunCore(StartupActionContext context, CancellationToken cancellationToken)
        {
            if (context == null || !context.PluginEnabled || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                if (!context.SetupValid)
                {
                    ApplySetupAction(context.SetupAction, cancellationToken);
                    return;
                }

                if (context.GenerateManifest && context.ManifestGenerationEligible)
                {
                    sink.GenerateManifest(cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (context.RefreshStatus)
                {
                    sink.RefreshStatus(cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (context.OpenDashboard)
                {
                    sink.OpenDashboard(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Application shutdown is an expected terminal state.
            }
            catch (Exception ex)
            {
                try
                {
                    sink.ReportFailure(ex, cancellationToken);
                }
                catch (Exception reportingException)
                {
                    ObserveFault(new AggregateException(
                        "Startup action and failure reporting both failed.",
                        ex,
                        reportingException));
                }
            }

        }

        private void ObserveFault(Exception exception)
        {
            try
            {
                observeFault(exception);
            }
            catch (Exception observerException)
            {
                Trace.TraceError(
                    "Startup task fault observer failed. Original: {0}; observer: {1}",
                    exception,
                    observerException);
            }
        }

        private void ApplySetupAction(SetupLaunchAction action, CancellationToken cancellationToken)
        {
            if (action == SetupLaunchAction.OpenWizard)
            {
                sink.OpenSetupWizard(cancellationToken);
            }
            else if (action == SetupLaunchAction.ShowReminder)
            {
                sink.ShowSetupReminder(cancellationToken);
            }
        }
    }

    public sealed class DelegatingStartupActionSink : IStartupActionSink
    {
        private readonly Action openSetupWizard;
        private readonly Action showSetupReminder;
        private readonly Action<CancellationToken> generateManifest;
        private readonly Action refreshStatus;
        private readonly Action openDashboard;
        private readonly Action<Exception> reportFailure;
        private readonly IStartupUiDispatcher dispatcher;

        public DelegatingStartupActionSink(
            Action openSetupWizard,
            Action showSetupReminder,
            Action<CancellationToken> generateManifest,
            Action refreshStatus,
            Action openDashboard,
            Action<Exception> reportFailure,
            IStartupUiDispatcher dispatcher)
        {
            this.openSetupWizard = openSetupWizard ?? throw new ArgumentNullException(nameof(openSetupWizard));
            this.showSetupReminder = showSetupReminder ?? throw new ArgumentNullException(nameof(showSetupReminder));
            this.generateManifest = generateManifest ?? throw new ArgumentNullException(nameof(generateManifest));
            this.refreshStatus = refreshStatus ?? throw new ArgumentNullException(nameof(refreshStatus));
            this.openDashboard = openDashboard ?? throw new ArgumentNullException(nameof(openDashboard));
            this.reportFailure = reportFailure ?? throw new ArgumentNullException(nameof(reportFailure));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public void OpenSetupWizard(CancellationToken cancellationToken) => dispatcher.Post(openSetupWizard, cancellationToken);
        public void ShowSetupReminder(CancellationToken cancellationToken) => dispatcher.Post(showSetupReminder, cancellationToken);
        public void GenerateManifest(CancellationToken cancellationToken) => generateManifest(cancellationToken);
        public void RefreshStatus(CancellationToken cancellationToken) => dispatcher.Post(refreshStatus, cancellationToken);
        public void OpenDashboard(CancellationToken cancellationToken) => dispatcher.Post(openDashboard, cancellationToken);
        public void ReportFailure(Exception exception, CancellationToken cancellationToken) =>
            dispatcher.Post(() => reportFailure(exception), cancellationToken);
    }
}
