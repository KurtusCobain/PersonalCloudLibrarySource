using System;

namespace PersonalCloudLibrarySource
{
    public sealed class DashboardViewModelLifetime : IDisposable
    {
        private readonly Func<IDisposable> factory;
        private IDisposable current;

        public DashboardViewModelLifetime(Func<IDisposable> factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public IDisposable Activate()
        {
            if (current == null)
            {
                current = factory() ?? throw new InvalidOperationException("Dashboard view model factory returned null.");
            }

            return current;
        }

        public void Deactivate()
        {
            var value = current;
            current = null;
            value?.Dispose();
        }

        public void Dispose()
        {
            Deactivate();
        }
    }
}
