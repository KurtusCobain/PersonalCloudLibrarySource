using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PersonalCloudLibrarySource.Tests.Startup
{
    [TestFixture]
    public class StartupActionServiceTests
    {
        [Test]
        public void Run_DisabledPlugin_DoesNothing()
        {
            var sink = new RecordingSink();
            var service = new StartupActionService(sink);

            service.Start(new StartupActionContext { PluginEnabled = false }).GetAwaiter().GetResult();

            Assert.That(sink.Calls, Is.Empty);
        }

        [Test]
        public void Run_RefreshOnly_RefreshesStatus()
        {
            var sink = new RecordingSink();
            var service = new StartupActionService(sink);

            service.Start(ValidContext(refresh: true)).GetAwaiter().GetResult();

            Assert.That(sink.Calls, Is.EqualTo(new[] { "refresh" }));
        }

        [Test]
        public void Run_EligibleGenerationOnly_GeneratesManifest()
        {
            var sink = new RecordingSink();
            var service = new StartupActionService(sink);
            var context = ValidContext(generate: true);
            context.ManifestGenerationEligible = true;

            service.Start(context).GetAwaiter().GetResult();

            Assert.That(sink.Calls, Is.EqualTo(new[] { "generate" }));
        }

        [Test]
        public void Run_BothActions_GeneratesBeforeRefreshing()
        {
            var sink = new RecordingSink();
            var service = new StartupActionService(sink);
            var context = ValidContext(generate: true, refresh: true);
            context.ManifestGenerationEligible = true;

            service.Start(context).GetAwaiter().GetResult();

            Assert.That(sink.Calls, Is.EqualTo(new[] { "generate", "refresh" }));
        }

        [Test]
        public void Run_IneligibleGeneration_IsSkipped()
        {
            var sink = new RecordingSink();
            var service = new StartupActionService(sink);

            service.Start(ValidContext(generate: true)).GetAwaiter().GetResult();

            Assert.That(sink.Calls, Is.Empty);
        }

        [Test]
        public void Run_InvalidSetup_PerformsOnlySetupDecision()
        {
            var sink = new RecordingSink();
            var service = new StartupActionService(sink);
            var context = new StartupActionContext
            {
                PluginEnabled = true,
                SetupValid = false,
                SetupAction = SetupLaunchAction.OpenWizard,
                GenerateManifest = true,
                ManifestGenerationEligible = true,
                RefreshStatus = true,
                OpenDashboard = true
            };

            service.Start(context).GetAwaiter().GetResult();

            Assert.That(sink.Calls, Is.EqualTo(new[] { "wizard" }));
        }

        [Test]
        public void Run_DismissedInvalidSetup_ShowsReminderOnly()
        {
            var sink = new RecordingSink();
            var service = new StartupActionService(sink);

            service.Start(new StartupActionContext
            {
                PluginEnabled = true,
                SetupValid = false,
                SetupAction = SetupLaunchAction.ShowReminder,
                RefreshStatus = true
            }).GetAwaiter().GetResult();

            Assert.That(sink.Calls, Is.EqualTo(new[] { "reminder" }));
        }

        [Test]
        public void Start_DuplicateCall_ReturnsSameTrackedTask()
        {
            var sink = new RecordingSink();
            var service = new StartupActionService(sink);
            var context = ValidContext(refresh: true);

            var first = service.Start(context);
            var second = service.Start(context);
            Task.WaitAll(first, second);

            Assert.That(second, Is.SameAs(first));
            Assert.That(sink.Calls, Is.EqualTo(new[] { "refresh" }));
        }

        [Test]
        public void Start_BlockingGenerationReturnsPromptly_StopCancelsAndPreventsLaterActions()
        {
            var sink = new RecordingSink { BlockGenerationUntilCancellation = true };
            var service = new StartupActionService(sink);
            var context = ValidContext(generate: true, refresh: true);
            context.ManifestGenerationEligible = true;
            context.OpenDashboard = true;

            var watch = Stopwatch.StartNew();
            var task = service.Start(context);
            watch.Stop();

            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
            Assert.That(sink.GenerationStarted.WaitOne(TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(service.Stop(TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(task.IsCompleted, Is.True);

            Assert.That(sink.Calls, Is.EqualTo(new[] { "generate" }));
        }

        [Test]
        public void Stop_IsBoundedWhenGenerationIgnoresCancellation_TaskRemainsObserved()
        {
            var sink = new RecordingSink { BlockGenerationIgnoringCancellation = true };
            var service = new StartupActionService(sink);
            var context = ValidContext(generate: true, refresh: true);
            context.ManifestGenerationEligible = true;
            context.OpenDashboard = true;
            var task = service.Start(context);
            Assert.That(sink.GenerationStarted.WaitOne(TimeSpan.FromSeconds(2)), Is.True);

            var watch = Stopwatch.StartNew();
            var stopped = service.Stop(TimeSpan.FromMilliseconds(100));
            watch.Stop();

            Assert.That(stopped, Is.False);
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(1)));
            Assert.That(task.IsCompleted, Is.False);
            sink.ReleaseGeneration.Set();
            Assert.That(task.Wait(TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(sink.Calls, Is.EqualTo(new[] { "generate" }));
        }

        [Test]
        public void Run_ActionFailure_IsReportedOnceAndStopsLaterActions()
        {
            var sink = new RecordingSink { ThrowOnGenerate = true };
            var service = new StartupActionService(sink);
            var context = ValidContext(generate: true, refresh: true);
            context.ManifestGenerationEligible = true;

            service.Start(context).GetAwaiter().GetResult();

            Assert.That(sink.Calls, Is.EqualTo(new[] { "generate", "failure" }));
            Assert.That(sink.Failure, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void Run_ActionAndReportingFailure_IsObservedWithoutFaultingRetainedTask()
        {
            var sink = new RecordingSink { ThrowOnGenerate = true, ThrowOnReport = true };
            var observed = new List<Exception>();
            var service = new StartupActionService(sink, observed.Add);
            var context = ValidContext(generate: true);
            context.ManifestGenerationEligible = true;

            var task = service.Start(context);

            Assert.That(task.Wait(TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(observed, Has.Count.EqualTo(1));
            Assert.That(observed[0], Is.TypeOf<AggregateException>());
        }

        [Test]
        public void Run_OpenDashboard_IsLast()
        {
            var sink = new RecordingSink();
            var service = new StartupActionService(sink);
            var context = ValidContext(refresh: true);
            context.OpenDashboard = true;

            service.Start(context).GetAwaiter().GetResult();

            Assert.That(sink.Calls, Is.EqualTo(new[] { "refresh", "dashboard" }));
        }

        private static StartupActionContext ValidContext(bool generate = false, bool refresh = false)
        {
            return new StartupActionContext
            {
                PluginEnabled = true,
                SetupValid = true,
                SetupAction = SetupLaunchAction.None,
                GenerateManifest = generate,
                RefreshStatus = refresh
            };
        }

        private sealed class RecordingSink : IStartupActionSink
        {
            public IList<string> Calls { get; } = new List<string>();
            public bool ThrowOnGenerate { get; set; }
            public bool ThrowOnReport { get; set; }
            public Action AfterGenerate { get; set; }
            public bool BlockGenerationUntilCancellation { get; set; }
            public bool BlockGenerationIgnoringCancellation { get; set; }
            public ManualResetEvent GenerationStarted { get; } = new ManualResetEvent(false);
            public ManualResetEvent ReleaseGeneration { get; } = new ManualResetEvent(false);
            public Exception Failure { get; private set; }

            public void OpenSetupWizard(CancellationToken cancellationToken) => Calls.Add("wizard");
            public void ShowSetupReminder(CancellationToken cancellationToken) => Calls.Add("reminder");

            public void GenerateManifest(CancellationToken cancellationToken)
            {
                Calls.Add("generate");
                GenerationStarted.Set();
                if (ThrowOnGenerate)
                {
                    throw new InvalidOperationException("generation failed");
                }

                if (BlockGenerationUntilCancellation)
                {
                    cancellationToken.WaitHandle.WaitOne();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (BlockGenerationIgnoringCancellation)
                {
                    ReleaseGeneration.WaitOne();
                }

                AfterGenerate?.Invoke();
            }

            public void RefreshStatus(CancellationToken cancellationToken) => Calls.Add("refresh");
            public void OpenDashboard(CancellationToken cancellationToken) => Calls.Add("dashboard");

            public void ReportFailure(Exception exception, CancellationToken cancellationToken)
            {
                Calls.Add("failure");
                if (ThrowOnReport)
                {
                    throw new InvalidOperationException("reporting failed");
                }
                Failure = exception;
            }
        }
    }
}
