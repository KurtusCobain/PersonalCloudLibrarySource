using System;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public sealed class DashboardRefreshPostGate
    {
        private int pending;

        public void Request(Action<Action> post, Action refresh)
        {
            if (post == null)
            {
                throw new ArgumentNullException(nameof(post));
            }

            if (refresh == null)
            {
                throw new ArgumentNullException(nameof(refresh));
            }

            if (Interlocked.Exchange(ref pending, 1) != 0)
            {
                return;
            }

            try
            {
                post(() =>
                {
                    try
                    {
                        refresh();
                    }
                    finally
                    {
                        Interlocked.Exchange(ref pending, 0);
                    }
                });
            }
            catch
            {
                Interlocked.Exchange(ref pending, 0);
            }
        }
    }
}
