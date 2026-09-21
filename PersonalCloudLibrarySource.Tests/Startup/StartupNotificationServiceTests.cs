using NUnit.Framework;
using Playnite.SDK;
using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource.Tests.Startup
{
    [TestFixture]
    public class StartupNotificationServiceTests
    {
        [Test]
        public void ShowSetupReminder_UsesStableDeduplicatedWarningWithRecoveryAction()
        {
            var sink = new RecordingSink();
            var dispatcher = new RecordingDispatcher();
            var opened = 0;
            var service = new StartupNotificationService(sink, dispatcher, Key, () => opened++, () => true);

            service.ShowSetupReminder();

            Assert.That(sink.Removed, Is.EqualTo(new[] { StartupNotificationService.SetupReminderId }));
            Assert.That(sink.Added, Has.Count.EqualTo(1));
            Assert.That(sink.Added[0].Id, Is.EqualTo(StartupNotificationService.SetupReminderId));
            Assert.That(sink.Added[0].Type, Is.EqualTo(NotificationType.Info));
            sink.Added[0].ActivationAction.Invoke();
            Assert.That(opened, Is.EqualTo(1));
            Assert.That(dispatcher.InvokeCount, Is.EqualTo(2));
        }

        [Test]
        public void ShowFailure_UsesStableDeduplicatedError()
        {
            var sink = new RecordingSink();
            var service = new StartupNotificationService(sink, new RecordingDispatcher(), Key, () => { }, () => true);

            service.ShowFailure(new InvalidOperationException("bad path"));

            Assert.That(sink.Removed, Is.EqualTo(new[] { StartupNotificationService.FailureId }));
            Assert.That(sink.Added[0].Id, Is.EqualTo(StartupNotificationService.FailureId));
            Assert.That(sink.Added[0].Type, Is.EqualTo(NotificationType.Error));
            Assert.That(sink.Added[0].Text, Does.Contain("bad path"));
        }

        [Test]
        public void FullscreenReminder_UsesDesktopRecoveryCopyAndHasNoActivationAction()
        {
            var sink = new RecordingSink();
            var service = new StartupNotificationService(
                sink,
                new RecordingDispatcher(),
                Key,
                () => Assert.Fail("Fullscreen notification must not open Desktop UI."),
                () => false);

            service.ShowSetupReminder();

            Assert.That(sink.Added[0].Text, Does.Contain("Desktop mode"));
            Assert.That(sink.Added[0].ActivationAction, Is.Null);
        }

        [Test]
        public void FullscreenFailure_UsesDesktopRecoveryCopyAndHasNoActivationAction()
        {
            var sink = new RecordingSink();
            var service = new StartupNotificationService(
                sink,
                new RecordingDispatcher(),
                Key,
                () => Assert.Fail("Fullscreen notification must not open Desktop UI."),
                () => false);

            service.ShowFailure(new InvalidOperationException("bad path"));

            Assert.That(sink.Added[0].Text, Does.Contain("Desktop mode"));
            Assert.That(sink.Added[0].ActivationAction, Is.Null);
        }

        private static string Key(string key)
        {
            if (key == "LOCPLSSetupReminderNotification") return "Setup needs attention.";
            if (key == "LOCPLSStartupFailureNotification") return "Startup action failed: {0}";
            if (key == "LOCPLSSetupReminderFullscreenNotification") return "Setup needs attention. Open Desktop mode to recover.";
            if (key == "LOCPLSStartupFailureFullscreenNotification") return "Startup action failed: {0} Open Desktop mode to recover.";
            return key;
        }

        private sealed class RecordingSink : IImportNotificationSink
        {
            public IList<NotificationMessage> Added { get; } = new List<NotificationMessage>();
            public IList<string> Removed { get; } = new List<string>();
            public void Add(NotificationMessage message) => Added.Add(message);
            public void Remove(string id) => Removed.Add(id);
        }

        private sealed class RecordingDispatcher : IImportUiDispatcher
        {
            public int InvokeCount { get; private set; }
            public void Invoke(Action action)
            {
                InvokeCount++;
                action();
            }
        }
    }
}
