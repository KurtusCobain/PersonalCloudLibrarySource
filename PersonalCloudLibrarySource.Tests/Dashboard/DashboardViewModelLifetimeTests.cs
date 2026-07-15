using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace PersonalCloudLibrarySource.Tests.Dashboard
{
    [TestFixture]
    public class DashboardViewModelLifetimeTests
    {
        [Test]
        public void DeactivateDisposesOnce_ActivateAfterUnloadCreatesFreshInstance()
        {
            var created = new List<DisposableViewModel>();
            var lifetime = new DashboardViewModelLifetime(() =>
            {
                var value = new DisposableViewModel();
                created.Add(value);
                return value;
            });

            var first = lifetime.Activate();
            lifetime.Deactivate();
            lifetime.Deactivate();
            var second = lifetime.Activate();

            Assert.That(created, Has.Count.EqualTo(2));
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(created[0].DisposeCount, Is.EqualTo(1));
            Assert.That(created[1].DisposeCount, Is.EqualTo(0));

            lifetime.Dispose();
            Assert.That(created[1].DisposeCount, Is.EqualTo(1));
        }

        private sealed class DisposableViewModel : IDisposable
        {
            public int DisposeCount { get; private set; }
            public void Dispose() => DisposeCount++;
        }
    }
}
