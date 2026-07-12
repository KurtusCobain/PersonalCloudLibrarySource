using Playnite.SDK;

namespace PersonalCloudLibrarySource
{
    public sealed class DashboardStateStore : ObservableObject
    {
        private CloudLibraryDashboardState current;

        public CloudLibraryDashboardState Current
        {
            get => current;
            set => SetValue(ref current, value);
        }
    }
}
