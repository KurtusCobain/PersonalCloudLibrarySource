using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource.Tests.Dashboard
{
    [TestFixture]
    public class DashboardRefreshPostGateTests
    {
        [Test]
        public void ThrowingPost_IsNonPropagatingAndReleasesGateForNextRefresh()
        {
            var gate = new DashboardRefreshPostGate();
            var callbacks = new Queue<Action>();
            var submissions = 0;
            Action<Action> post = callback =>
            {
                submissions++;
                if (submissions == 1)
                {
                    throw new InvalidOperationException("dispatcher unavailable");
                }

                callbacks.Enqueue(callback);
            };
            var refreshes = 0;

            Assert.DoesNotThrow(() => gate.Request(post, () => refreshes++));
            Assert.DoesNotThrow(() => gate.Request(post, () => refreshes++));
            callbacks.Dequeue()();

            Assert.That(submissions, Is.EqualTo(2));
            Assert.That(refreshes, Is.EqualTo(1));
        }

        [Test]
        public void PendingPost_CoalescesUntilCallbackCompletes()
        {
            var gate = new DashboardRefreshPostGate();
            var callbacks = new Queue<Action>();
            var refreshes = 0;

            gate.Request(callbacks.Enqueue, () => refreshes++);
            gate.Request(callbacks.Enqueue, () => refreshes++);

            Assert.That(callbacks, Has.Count.EqualTo(1));
            callbacks.Dequeue()();
            gate.Request(callbacks.Enqueue, () => refreshes++);
            callbacks.Dequeue()();
            Assert.That(refreshes, Is.EqualTo(2));
        }
    }
}
