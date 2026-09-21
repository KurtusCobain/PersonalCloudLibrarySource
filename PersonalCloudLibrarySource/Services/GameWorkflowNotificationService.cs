using Playnite.SDK;
using System;

namespace PersonalCloudLibrarySource
{
    public interface IGameWorkflowNotificationSink
    {
        void Add(NotificationMessage message);
        void Remove(string id);
    }

    public sealed class GameWorkflowNotificationService
    {
        private const string Prefix = "PersonalCloudLibrarySource.GameWorkflow.";
        private readonly IGameWorkflowNotificationSink sink;
        private readonly IImportUiDispatcher dispatcher;
        private readonly Action<Exception> observeException;

        public GameWorkflowNotificationService(IGameWorkflowNotificationSink sink)
            : this(sink, new ImmediateGameWorkflowUiDispatcher(), null)
        {
        }

        public GameWorkflowNotificationService(
            IGameWorkflowNotificationSink sink,
            IImportUiDispatcher dispatcher,
            Action<Exception> observeException = null)
        {
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.observeException = observeException;
        }

        public void Success(string operation, string gameId, string text) => Publish(operation, gameId, text, NotificationType.Info);
        public void Warning(string operation, string gameId, string text) => Publish(operation, gameId, text, NotificationType.Info);
        public void Failure(string operation, string gameId, string text) => Publish(operation, gameId, text, NotificationType.Error);

        private void Publish(string operation, string gameId, string text, NotificationType type)
        {
            var id = Prefix + Normalize(operation) + "." + Normalize(gameId);
            try
            {
                dispatcher.Invoke(() =>
                {
                    TrySink(() => sink.Remove(id));
                    TrySink(() => sink.Add(new NotificationMessage(id, text ?? string.Empty, type)));
                });
            }
            catch (Exception ex)
            {
                Observe(ex);
            }
        }

        private void TrySink(Action action)
        {
            try { action(); }
            catch (Exception ex) { Observe(ex); }
        }

        private void Observe(Exception exception)
        {
            try { observeException?.Invoke(exception); }
            catch { }
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().Replace(" ", "-");
        }
    }

    internal sealed class ImmediateGameWorkflowUiDispatcher : IImportUiDispatcher
    {
        public void Invoke(Action action) => action();
    }

    public sealed class PlayniteGameWorkflowNotificationSink : IGameWorkflowNotificationSink
    {
        private readonly INotificationsAPI notifications;

        public PlayniteGameWorkflowNotificationSink(INotificationsAPI notifications)
        {
            this.notifications = notifications;
        }

        public void Add(NotificationMessage message) => notifications?.Add(message);
        public void Remove(string id) => notifications?.Remove(id);
    }
}
