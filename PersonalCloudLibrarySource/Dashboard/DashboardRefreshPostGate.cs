using System;
using System.Threading;

namespace PersonalCloudLibrarySource
{
    public sealed class DashboardRefreshPostGate
    {
        private int pending;

        public void Request(Action<Action> post, Action refresh)
        {
            if (post == null) throw new ArgumentNullException(nameof(post));
            RequestAcknowledged(callback => { post(callback); return true; }, refresh);
        }

        public void RequestAcknowledged(Func<Action, bool> post, Action refresh)
        {
            if (post == null) throw new ArgumentNullException(nameof(post));
            if (refresh == null) throw new ArgumentNullException(nameof(refresh));
            if (Interlocked.CompareExchange(ref pending, 1, 0) != 0) return;

            try
            {
                if (!post(() =>
                {
                    // Release before reading the latest state. An event arriving
                    // during the refresh must be able to request a follow-up.
                    Interlocked.Exchange(ref pending, 0);
                    refresh();
                }))
                    Interlocked.Exchange(ref pending, 0);
            }
            catch
            {
                Interlocked.Exchange(ref pending, 0);
            }
        }
    }
}
