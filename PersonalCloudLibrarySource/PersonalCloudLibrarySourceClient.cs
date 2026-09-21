using System;

namespace PersonalCloudLibrarySource
{
    public class PersonalCloudLibrarySourceClient : Playnite.SDK.LibraryClient
    {
        private readonly Action openDashboard;

        public PersonalCloudLibrarySourceClient(Action openDashboard = null)
        {
            this.openDashboard = openDashboard;
        }

        public override bool IsInstalled => true;

        public override void Open()
        {
            openDashboard?.Invoke();
        }
    }
}
