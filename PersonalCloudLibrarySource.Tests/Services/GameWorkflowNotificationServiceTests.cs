using NUnit.Framework;
using Playnite.SDK;
using System.Collections.Generic;
using System;

namespace PersonalCloudLibrarySource.Tests.Services
{
    [TestFixture]
    public class GameWorkflowNotificationServiceTests
    {
        [Test]
        public void Failure_UsesPersistentPlayniteNotificationWithoutWindowCallback()
        {
            var sink = new RecordingSink();
            var service = new GameWorkflowNotificationService(sink);

            service.Failure("install", "game-id", "Transfer failed: source unavailable.");

            Assert.That(sink.Messages, Has.Count.EqualTo(1));
            Assert.That(sink.Messages[0].Type, Is.EqualTo(NotificationType.Error));
            Assert.That(sink.Messages[0].Text, Does.Contain("source unavailable"));
        }

        [Test]
        public void NewResult_ReplacesPriorOperationNotification()
        {
            var sink = new RecordingSink();
            var service = new GameWorkflowNotificationService(sink);

            service.Failure("install", "game-id", "failed");
            service.Success("install", "game-id", "completed");

            Assert.That(sink.Removed, Has.Count.EqualTo(2));
            Assert.That(sink.Removed[0], Is.EqualTo(sink.Removed[1]));
            Assert.That(sink.Messages[1].Type, Is.EqualTo(NotificationType.Info));
        }

        [Test]
        public void Publish_RoutesSinkMutationThroughUiDispatcher()
        {
            var sink = new RecordingSink();
            var dispatcher = new RecordingDispatcher();
            var service = new GameWorkflowNotificationService(sink, dispatcher);

            service.Failure("install", "game-id", "failed");

            Assert.That(sink.Messages, Is.Empty);
            dispatcher.Action();
            Assert.That(sink.Messages, Has.Count.EqualTo(1));
        }

        [Test]
        public void Publish_DispatchSubmissionAndSinkFailuresDoNotPropagate()
        {
            var observed = new List<Exception>();
            var submissionService = new GameWorkflowNotificationService(
                new RecordingSink(),
                new ThrowingDispatcher(),
                observed.Add);

            Assert.DoesNotThrow(() => submissionService.Failure("install", "one", "failed"));

            var dispatcher = new RecordingDispatcher();
            var sinkService = new GameWorkflowNotificationService(
                new ThrowingSink(),
                dispatcher,
                observed.Add);
            sinkService.Success("install", "two", "done");
            Assert.DoesNotThrow(() => dispatcher.Action());
            Assert.That(observed, Has.Count.EqualTo(3));
        }

        private sealed class RecordingSink : IGameWorkflowNotificationSink
        {
            public List<NotificationMessage> Messages { get; } = new List<NotificationMessage>();
            public List<string> Removed { get; } = new List<string>();
            public void Add(NotificationMessage message) => Messages.Add(message);
            public void Remove(string id) => Removed.Add(id);
        }

        private sealed class ThrowingSink : IGameWorkflowNotificationSink
        {
            public void Add(NotificationMessage message) => throw new InvalidOperationException("add failed");
            public void Remove(string id) => throw new InvalidOperationException("remove failed");
        }

        private sealed class RecordingDispatcher : IImportUiDispatcher
        {
            public Action Action { get; private set; }
            public void Invoke(Action action) => Action = action;
        }

        private sealed class ThrowingDispatcher : IImportUiDispatcher
        {
            public void Invoke(Action action) => throw new InvalidOperationException("dispatch failed");
        }
    }
}
