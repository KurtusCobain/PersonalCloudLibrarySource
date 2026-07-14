using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace PersonalCloudLibrarySource.Tests.Startup
{
    [TestFixture]
    public class StartupUiDispatcherTests
    {
        [Test]
        public void BlockedDispatcher_ShutdownReturnsPromptly_ReleasedCallbackDoesNotExecute()
        {
            var target = new QueuedPostTarget();
            var observed = new List<Exception>();
            var dispatcher = new StartupUiDispatcher(target, observed.Add);
            var calls = new List<string>();
            var sink = CreateSink(dispatcher, calls, _ => { });
            var service = new StartupActionService(sink);

            var task = service.Start(new StartupActionContext
            {
                PluginEnabled = true,
                SetupValid = true,
                RefreshStatus = true,
                OpenDashboard = true
            });
            Assert.That(task.Wait(TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(target.PendingCount, Is.EqualTo(2));

            var watch = Stopwatch.StartNew();
            Assert.That(service.Stop(TimeSpan.FromSeconds(2)), Is.True);
            watch.Stop();

            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
            target.ExecuteAll();
            Assert.That(calls, Is.Empty);
            Assert.That(observed, Is.Empty);
            Assert.That(task.IsCompleted, Is.True);
        }

        [Test]
        public void NormalPosts_ExecuteInGenerationRefreshDashboardOrder()
        {
            var target = new QueuedPostTarget();
            var dispatcher = new StartupUiDispatcher(target, _ => Assert.Fail("Unexpected callback failure."));
            var calls = new List<string>();
            var sink = CreateSink(
                dispatcher,
                calls,
                token => dispatcher.Post(() => calls.Add("generation"), token));
            var service = new StartupActionService(sink);

            var task = service.Start(new StartupActionContext
            {
                PluginEnabled = true,
                SetupValid = true,
                GenerateManifest = true,
                ManifestGenerationEligible = true,
                RefreshStatus = true,
                OpenDashboard = true
            });
            Assert.That(task.Wait(TimeSpan.FromSeconds(2)), Is.True);

            target.ExecuteAll();

            Assert.That(calls, Is.EqualTo(new[] { "generation", "refresh", "dashboard" }));
        }

        [Test]
        public void PostedCallbackException_IsObservedWithoutEscapingDispatcher()
        {
            var target = new QueuedPostTarget();
            var observed = new List<Exception>();
            var dispatcher = new StartupUiDispatcher(target, observed.Add);

            dispatcher.Post(() => throw new InvalidOperationException("ui failed"), CancellationToken.None);
            Assert.DoesNotThrow(target.ExecuteAll);

            Assert.That(observed, Has.Count.EqualTo(1));
            Assert.That(observed[0].Message, Is.EqualTo("ui failed"));
        }

        [Test]
        public void ThrowingPostTarget_IsObservedOnce_DoesNotFaultTaskOrRecursivelyPost()
        {
            var target = new ThrowingPostTarget();
            var observed = new List<Exception>();
            var dispatcher = new StartupUiDispatcher(target, observed.Add);
            var calls = new List<string>();
            var service = new StartupActionService(CreateSink(dispatcher, calls, _ => { }));

            var task = service.Start(new StartupActionContext
            {
                PluginEnabled = true,
                SetupValid = true,
                RefreshStatus = true
            });

            Assert.That(task.Wait(TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(task.Status, Is.EqualTo(System.Threading.Tasks.TaskStatus.RanToCompletion));
            Assert.That(service.Stop(TimeSpan.FromSeconds(1)), Is.True);
            Assert.That(target.SubmissionCount, Is.EqualTo(1));
            Assert.That(observed, Has.Count.EqualTo(1));
            Assert.That(observed[0].Message, Is.EqualTo("dispatcher unavailable"));
            Assert.That(calls, Is.Empty);
        }

        private static IStartupActionSink CreateSink(
            IStartupUiDispatcher dispatcher,
            IList<string> calls,
            Action<CancellationToken> generate)
        {
            return new DelegatingStartupActionSink(
                () => calls.Add("wizard"),
                () => calls.Add("reminder"),
                generate,
                () => calls.Add("refresh"),
                () => calls.Add("dashboard"),
                _ => calls.Add("failure"),
                dispatcher);
        }

        private sealed class QueuedPostTarget : IStartupUiPostTarget
        {
            private readonly Queue<Action> callbacks = new Queue<Action>();

            public int PendingCount => callbacks.Count;
            public void BeginInvoke(Action action) => callbacks.Enqueue(action);

            public void ExecuteAll()
            {
                while (callbacks.Count > 0)
                {
                    callbacks.Dequeue()();
                }
            }
        }

        private sealed class ThrowingPostTarget : IStartupUiPostTarget
        {
            public int SubmissionCount { get; private set; }

            public void BeginInvoke(Action action)
            {
                SubmissionCount++;
                throw new InvalidOperationException("dispatcher unavailable");
            }
        }
    }
}
