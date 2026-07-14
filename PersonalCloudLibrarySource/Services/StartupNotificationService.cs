using Playnite.SDK;
using System;
using System.Globalization;

namespace PersonalCloudLibrarySource
{
    public sealed class StartupNotificationService
    {
        public const string SetupReminderId = "PersonalCloudLibrarySource.SetupReminder";
        public const string FailureId = "PersonalCloudLibrarySource.StartupFailure";

        private readonly IImportNotificationSink sink;
        private readonly IImportUiDispatcher dispatcher;
        private readonly Func<string, string> getResource;
        private readonly Action openRecovery;
        private readonly Func<bool> isDesktopMode;

        public StartupNotificationService(
            IImportNotificationSink sink,
            IImportUiDispatcher dispatcher,
            Func<string, string> getResource,
            Action openRecovery,
            Func<bool> isDesktopMode)
        {
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.getResource = getResource ?? throw new ArgumentNullException(nameof(getResource));
            this.openRecovery = openRecovery ?? throw new ArgumentNullException(nameof(openRecovery));
            this.isDesktopMode = isDesktopMode ?? throw new ArgumentNullException(nameof(isDesktopMode));
        }

        public void ShowSetupReminder()
        {
            var desktop = isDesktopMode();
            var text = getResource(desktop
                ? "LOCPLSSetupReminderNotification"
                : "LOCPLSSetupReminderFullscreenNotification") ?? string.Empty;
            Replace(desktop
                ? new NotificationMessage(
                    SetupReminderId,
                    text,
                    NotificationType.Info,
                    () => dispatcher.Invoke(openRecovery))
                : new NotificationMessage(SetupReminderId, text, NotificationType.Info));
        }

        public void ShowFailure(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            var desktop = isDesktopMode();
            var template = getResource(desktop
                ? "LOCPLSStartupFailureNotification"
                : "LOCPLSStartupFailureFullscreenNotification") ?? string.Empty;
            var text = string.Format(CultureInfo.CurrentCulture, template, exception.Message);
            Replace(desktop
                ? new NotificationMessage(
                    FailureId,
                    text,
                    NotificationType.Error,
                    () => dispatcher.Invoke(openRecovery))
                : new NotificationMessage(FailureId, text, NotificationType.Error));
        }

        private void Replace(NotificationMessage message)
        {
            dispatcher.Invoke(() =>
            {
                sink.Remove(message.Id);
                sink.Add(message);
            });
        }
    }
}
