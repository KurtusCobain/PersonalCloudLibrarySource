using NUnit.Framework;
using Playnite.SDK;
using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class ImportNotificationServiceTests
    {
        [Test]
        public void ShowFailure_UsesStableIdLocalizedTextAndDeduplicates()
        {
            var sink = new FakeSink();
            var dispatcher = new RecordingDispatcher();
            var service = new ImportNotificationService(
                sink,
                dispatcher,
                key => key == "LOCPLSImportFailureNotification"
                    ? "Localized import failure: {0}"
                    : key == "LOCPLSImportFailureSourceUnavailable"
                        ? "Localized source unavailable"
                        : null,
                () => { });
            var outcome = ImportOutcome.Failure(ImportFailureKind.SourceUnavailable, "source", "offline");

            service.ShowFailure(outcome);
            service.ShowFailure(outcome);

            Assert.That(sink.Messages.Count, Is.EqualTo(1));
            Assert.That(sink.Messages.ContainsKey(ImportNotificationService.NotificationId), Is.True);
            Assert.That(sink.Messages[ImportNotificationService.NotificationId].Text, Is.EqualTo("Localized import failure: Localized source unavailable"));
            Assert.That(sink.Messages[ImportNotificationService.NotificationId].Type, Is.EqualTo(NotificationType.Error));
            Assert.That(dispatcher.InvocationCount, Is.EqualTo(2));
        }

        [Test]
        public void FailureNotification_ActionUsesDispatcherAndOpensRecoverySurface()
        {
            var sink = new FakeSink();
            var dispatcher = new RecordingDispatcher();
            var recoveryCount = 0;
            var service = new ImportNotificationService(
                sink,
                dispatcher,
                key => "Import failed: {0}",
                () => recoveryCount++);

            service.ShowFailure(ImportOutcome.Failure(ImportFailureKind.InvalidManifest, "source", "bad json"));
            sink.Messages[ImportNotificationService.NotificationId].ActivationAction();

            Assert.That(recoveryCount, Is.EqualTo(1));
            Assert.That(dispatcher.InvocationCount, Is.EqualTo(2));
        }

        [Test]
        public void ClearAfterSuccessfulRecovery_RemovesPersistentNotificationOnDispatcher()
        {
            var sink = new FakeSink();
            var dispatcher = new RecordingDispatcher();
            var service = new ImportNotificationService(sink, dispatcher, key => "Import failed: {0}", () => { });
            service.ShowFailure(ImportOutcome.Failure(ImportFailureKind.SourceUnavailable, "source", "offline"));

            service.Clear();

            Assert.That(sink.Messages, Is.Empty);
            Assert.That(dispatcher.InvocationCount, Is.EqualTo(2));
        }

        private sealed class FakeSink : IImportNotificationSink
        {
            public Dictionary<string, NotificationMessage> Messages { get; } = new Dictionary<string, NotificationMessage>();
            public void Add(NotificationMessage message) { Messages[message.Id] = message; }
            public void Remove(string id) { Messages.Remove(id); }
        }

        private sealed class RecordingDispatcher : IImportUiDispatcher
        {
            public int InvocationCount { get; private set; }
            public void Invoke(Action action)
            {
                InvocationCount++;
                action();
            }
        }
    }
}
