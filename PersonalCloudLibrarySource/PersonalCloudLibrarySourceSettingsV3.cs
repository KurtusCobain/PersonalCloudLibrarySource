using Playnite.SDK;

namespace PersonalCloudLibrarySource
{
    public class PersonalCloudLibrarySourceSettingsV3 : PersonalCloudLibrarySourceSettings
    {
        public const int CurrentSettingsVersion = 4;

        private int settingsVersion;
        private bool showTopPanelButton = true;
        private bool showSidebarDashboard = true;
        private bool showSetupReminders = true;
        private bool openDashboardAtStartup;
        private int transferConcurrency = 1;
        private bool verifyAfterTransfer = true;
        private bool removeIncompleteTransferFiles = true;
        private bool notifyLibraryUpdates = true;
        private bool notifyTransferCompleted = true;
        private bool notifyTransferFailed = true;
        private bool notifySourceUnavailable = true;
        private bool notifyVerificationWarnings = true;

        public int SettingsVersion
        {
            get => settingsVersion;
            set => SetValue(ref settingsVersion, value);
        }

        public bool ShowTopPanelButton
        {
            get => showTopPanelButton;
            set => SetValue(ref showTopPanelButton, value);
        }

        public bool ShowSidebarDashboard
        {
            get => showSidebarDashboard;
            set => SetValue(ref showSidebarDashboard, value);
        }

        public bool ShowSetupReminders
        {
            get => showSetupReminders;
            set => SetValue(ref showSetupReminders, value);
        }

        public bool OpenDashboardAtStartup
        {
            get => openDashboardAtStartup;
            set => SetValue(ref openDashboardAtStartup, value);
        }

        public int TransferConcurrency
        {
            get => transferConcurrency;
            set => SetValue(ref transferConcurrency, value);
        }

        public bool VerifyAfterTransfer
        {
            get => verifyAfterTransfer;
            set => SetValue(ref verifyAfterTransfer, value);
        }

        public bool RemoveIncompleteTransferFiles
        {
            get => removeIncompleteTransferFiles;
            set => SetValue(ref removeIncompleteTransferFiles, value);
        }

        public bool NotifyLibraryUpdates
        {
            get => notifyLibraryUpdates;
            set => SetValue(ref notifyLibraryUpdates, value);
        }

        public bool NotifyTransferCompleted
        {
            get => notifyTransferCompleted;
            set => SetValue(ref notifyTransferCompleted, value);
        }

        public bool NotifyTransferFailed
        {
            get => notifyTransferFailed;
            set => SetValue(ref notifyTransferFailed, value);
        }

        public bool NotifySourceUnavailable
        {
            get => notifySourceUnavailable;
            set => SetValue(ref notifySourceUnavailable, value);
        }

        public bool NotifyVerificationWarnings
        {
            get => notifyVerificationWarnings;
            set => SetValue(ref notifyVerificationWarnings, value);
        }
    }
}
