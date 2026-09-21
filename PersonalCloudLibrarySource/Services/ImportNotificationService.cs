using Playnite.SDK;
using System;
using System.Globalization;

namespace PersonalCloudLibrarySource
{
    public interface IImportNotificationSink
    {
        void Add(NotificationMessage message);
        void Remove(string id);
    }

    public interface IImportUiDispatcher
    {
        void Invoke(Action action);
    }

    public sealed class ImportNotificationService
    {
        public const string NotificationId = "PersonalCloudLibrarySource.ImportFailure";

        private readonly IImportNotificationSink sink;
        private readonly IImportUiDispatcher dispatcher;
        private readonly Func<string, string> getResource;
        private readonly Action openRecovery;

        public ImportNotificationService(
            IImportNotificationSink sink,
            IImportUiDispatcher dispatcher,
            Func<string, string> getResource,
            Action openRecovery)
        {
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.getResource = getResource ?? throw new ArgumentNullException(nameof(getResource));
            this.openRecovery = openRecovery ?? throw new ArgumentNullException(nameof(openRecovery));
        }

        public void ShowFailure(ImportOutcome outcome)
        {
            if (outcome == null || outcome.Succeeded)
            {
                throw new ArgumentException("A failed import outcome is required.", nameof(outcome));
            }

            var template = getResource("LOCPLSImportFailureNotification") ?? string.Empty;
            var reason = getResource(GetFailureResourceKey(outcome.FailureKind)) ?? string.Empty;
            var text = string.Format(CultureInfo.CurrentCulture, template, reason);
            dispatcher.Invoke(() =>
            {
                sink.Remove(NotificationId);
                sink.Add(new NotificationMessage(
                    NotificationId,
                    text,
                    NotificationType.Error,
                    () => dispatcher.Invoke(openRecovery)));
            });
        }

        public void Clear()
        {
            dispatcher.Invoke(() => sink.Remove(NotificationId));
        }

        private static string GetFailureResourceKey(ImportFailureKind kind)
        {
            switch (kind)
            {
                case ImportFailureKind.UnsupportedSchema:
                    return "LOCPLSImportFailureUnsupportedSchema";
                case ImportFailureKind.InvalidManifest:
                    return "LOCPLSImportFailureInvalidManifest";
                default:
                    return "LOCPLSImportFailureSourceUnavailable";
            }
        }
    }

    public sealed class PlayniteImportNotificationSink : IImportNotificationSink
    {
        private readonly INotificationsAPI notifications;

        public PlayniteImportNotificationSink(INotificationsAPI notifications)
        {
            this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        }

        public void Add(NotificationMessage message) => notifications.Add(message);
        public void Remove(string id) => notifications.Remove(id);
    }

    public sealed class PlayniteImportUiDispatcher : IImportUiDispatcher
    {
        private readonly IPlayniteAPI api;

        public PlayniteImportUiDispatcher(IPlayniteAPI api)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public void Invoke(Action action)
        {
            if (api.MainView?.UIDispatcher == null)
            {
                action();
                return;
            }

            api.MainView.UIDispatcher.Invoke(action);
        }
    }
}
