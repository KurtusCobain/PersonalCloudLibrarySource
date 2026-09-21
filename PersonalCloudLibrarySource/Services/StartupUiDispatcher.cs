using Playnite.SDK;
using System;
using System.Threading;
using System.Diagnostics;

namespace PersonalCloudLibrarySource
{
    public interface IStartupUiDispatcher
    {
        void Post(Action action, CancellationToken cancellationToken);
    }

    public interface IAcknowledgingStartupUiDispatcher : IStartupUiDispatcher
    {
        bool TryPost(Action action, CancellationToken cancellationToken);
    }

    public interface IStartupUiPostTarget
    {
        void BeginInvoke(Action action);
    }

    public sealed class StartupUiDispatcher : IAcknowledgingStartupUiDispatcher
    {
        private readonly IStartupUiPostTarget target;
        private readonly Action<Exception> observeException;

        public StartupUiDispatcher(
            IStartupUiPostTarget target,
            Action<Exception> observeException)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.observeException = observeException ?? throw new ArgumentNullException(nameof(observeException));
        }

        public void Post(Action action, CancellationToken cancellationToken)
        {
            TryPost(action, cancellationToken);
        }

        public bool TryPost(Action action, CancellationToken cancellationToken)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            try
            {
                target.BeginInvoke(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Observe(ex);
                    }
                });
                return true;
            }
            catch (Exception ex)
            {
                Observe(ex);
                return false;
            }
        }

        private void Observe(Exception exception)
        {
            try
            {
                observeException(exception);
            }
            catch (Exception observerException)
            {
                Trace.TraceError(
                    "Startup UI exception observer failed. Original: {0}; observer: {1}",
                    exception,
                    observerException);
            }
        }
    }

    public sealed class PlayniteStartupUiPostTarget : IStartupUiPostTarget
    {
        private readonly IPlayniteAPI api;

        public PlayniteStartupUiPostTarget(IPlayniteAPI api)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public void BeginInvoke(Action action)
        {
            if (api.MainView?.UIDispatcher == null)
            {
                throw new InvalidOperationException("Playnite's UI dispatcher is unavailable.");
            }

            api.MainView.UIDispatcher.BeginInvoke(action);
        }
    }
}
