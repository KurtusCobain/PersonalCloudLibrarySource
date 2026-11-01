using System;
using System.Threading;
using System.Threading.Tasks;

namespace PersonalCloudLibrarySource
{
    public sealed class SetupCompletionCoordinator
    {
        public async Task<bool> CompleteAsync(
            Func<CancellationToken, Task> prepareAsync,
            Func<bool> persist,
            Action reportSuccess,
            CancellationToken token)
        {
            if (prepareAsync == null) throw new ArgumentNullException(nameof(prepareAsync));
            if (persist == null) throw new ArgumentNullException(nameof(persist));
            if (reportSuccess == null) throw new ArgumentNullException(nameof(reportSuccess));

            token.ThrowIfCancellationRequested();
            await prepareAsync(token);
            token.ThrowIfCancellationRequested();
            if (!persist()) return false;
            reportSuccess();
            return true;
        }

        public bool Complete(Action prepare, Func<bool> persist, Action reportSuccess)
        {
            if (prepare == null) throw new ArgumentNullException(nameof(prepare));
            if (persist == null) throw new ArgumentNullException(nameof(persist));
            if (reportSuccess == null) throw new ArgumentNullException(nameof(reportSuccess));

            prepare();
            if (!persist())
            {
                return false;
            }

            reportSuccess();
            return true;
        }
    }
}
