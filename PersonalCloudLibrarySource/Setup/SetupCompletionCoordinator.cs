using System;

namespace PersonalCloudLibrarySource
{
    public sealed class SetupCompletionCoordinator
    {
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
