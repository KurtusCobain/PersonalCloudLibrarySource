from pathlib import Path


def replace_once(text, old, new, label):
    if new in text:
        return text
    if old not in text:
        raise SystemExit(f"Patch target missing for {label}: {old}")
    return text.replace(old, new, 1)


# Register tests.
tests_path = Path("PersonalCloudLibrarySource.Tests/PersonalCloudLibrarySource.Tests.csproj")
tests = tests_path.read_text(encoding="utf-8-sig")
tests = replace_once(
    tests,
    '    <Compile Include="Transfers\\CloudTransferQueueItemViewModelTests.cs" />',
    '    <Compile Include="Transfers\\CloudTransferQueueItemViewModelTests.cs" />\n    <Compile Include="Transfers\\TransferActivityTrackerTests.cs" />',
    "activity tracker test registration",
)
tests_path.write_text(tests, encoding="utf-8-sig")

# Register production files.
project_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.csproj")
project = project_path.read_text(encoding="utf-8-sig")
project = replace_once(
    project,
    '    <Compile Include="Dashboard\\DashboardStateStore.cs" />',
    '    <Compile Include="Dashboard\\DashboardStateStore.cs" />\n    <Compile Include="Dashboard\\DashboardActivityService.cs" />',
    "dashboard activity service registration",
)
project = replace_once(
    project,
    '    <Compile Include="Transfers\\RcloneTransferModels.cs" />',
    '    <Compile Include="Transfers\\RcloneTransferModels.cs" />\n    <Compile Include="Transfers\\TransferActivityTracker.cs" />',
    "transfer activity tracker registration",
)
project_path.write_text(project, encoding="utf-8-sig")

# Integrate transfer activity and notifications.
transfers_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.Transfers.cs")
transfers = transfers_path.read_text(encoding="utf-8-sig")
transfers = replace_once(
    transfers,
    "using System;",
    "using Playnite.SDK;\nusing System;",
    "Playnite notification namespace",
)
transfers = replace_once(
    transfers,
    """        private CloudTransferManager transferManager;
        private CloudTransferExecutor transferExecutor;""",
    """        private CloudTransferManager transferManager;
        private CloudTransferExecutor transferExecutor;
        private DashboardActivityService dashboardActivityService;
        private TransferActivityTracker transferActivityTracker;""",
    "transfer activity fields",
)
transfers = replace_once(
    transfers,
    """        private int GetActiveTransferCount()
""",
    """        internal DashboardActivityService GetDashboardActivityService()
        {
            if (dashboardActivityService == null)
            {
                dashboardActivityService = new DashboardActivityService();
            }

            return dashboardActivityService;
        }

        private TransferActivityTracker GetTransferActivityTracker()
        {
            if (transferActivityTracker == null)
            {
                transferActivityTracker = new TransferActivityTracker();
            }

            return transferActivityTracker;
        }

        private int GetActiveTransferCount()
""",
    "activity service accessors",
)
transfers = replace_once(
    transfers,
    """            playniteApi.MainView.UIDispatcher.BeginInvoke(new Action(() =>
            {
                RefreshDashboardState();
                UpdateNavigationItemState();
            }));""",
    """            playniteApi.MainView.UIDispatcher.BeginInvoke(new Action(() =>
            {
                ProcessNewTransferActivities();
                RefreshDashboardState();
                UpdateNavigationItemState();
            }));""",
    "transfer activity processing call",
)
transfers = replace_once(
    transfers,
    """        private void DisposeTransferManager()
""",
    """        private void ProcessNewTransferActivities()
        {
            foreach (var record in GetTransferActivityTracker().CollectNew(GetTransferManager().Jobs))
            {
                GetDashboardActivityService().Add(
                    record.Kind,
                    record.Message,
                    record.TimestampUtc,
                    record.GameId);

                if (record.Kind == DashboardActivityKind.TransferCompleted &&
                    settings?.Settings?.NotifyTransferCompleted == true)
                {
                    AddTransferNotification(record, NotificationType.Info);
                }
                else if (record.Kind == DashboardActivityKind.TransferFailed &&
                         settings?.Settings?.NotifyTransferFailed == true)
                {
                    AddTransferNotification(record, NotificationType.Error);
                }
            }
        }

        private void AddTransferNotification(
            DashboardActivityRecord record,
            NotificationType notificationType)
        {
            Action callback = null;
            if (record.GameId.HasValue)
            {
                var gameId = record.GameId.Value;
                callback = () => playniteApi.MainView.SelectGame(gameId);
            }

            playniteApi.Notifications.Add(new NotificationMessage(
                Guid.NewGuid().ToString(),
                record.Message,
                notificationType,
                callback));
        }

        private void DisposeTransferManager()
""",
    "transfer activity methods",
)
transfers_path.write_text(transfers, encoding="utf-8-sig")

# Connect activity service to dashboard VM.
view_model_path = Path("PersonalCloudLibrarySource/Dashboard/CloudLibraryDashboardViewModel.cs")
view_model = view_model_path.read_text(encoding="utf-8-sig")
view_model = replace_once(
    view_model,
    """        private readonly PersonalCloudLibrarySourceSettings transferSettings;
""",
    """        private readonly PersonalCloudLibrarySourceSettings transferSettings;
        private readonly DashboardActivityService activityService;
""",
    "dashboard activity field",
)
view_model = replace_once(
    view_model,
    """            CloudTransferExecutor transferExecutor = null,
            PersonalCloudLibrarySourceSettings transferSettings = null)""",
    """            CloudTransferExecutor transferExecutor = null,
            PersonalCloudLibrarySourceSettings transferSettings = null,
            DashboardActivityService activityService = null)""",
    "dashboard constructor activity argument",
)
view_model = replace_once(
    view_model,
    """            this.transferSettings = transferSettings;
            this.stateStore.PropertyChanged += StateStore_PropertyChanged;""",
    """            this.transferSettings = transferSettings;
            this.activityService = activityService;
            this.stateStore.PropertyChanged += StateStore_PropertyChanged;
            if (this.activityService != null)
            {
                this.activityService.Changed += ActivityService_Changed;
            }""",
    "dashboard activity subscription",
)
view_model = replace_once(
    view_model,
    """        public IReadOnlyList<CloudTransferQueueItemViewModel> TransferJobs
""",
    """        public IReadOnlyList<DashboardActivityRecord> RecentActivities =>
            activityService == null
                ? new List<DashboardActivityRecord>().AsReadOnly()
                : activityService.Records.Take(5).ToList().AsReadOnly();

        public IReadOnlyList<CloudTransferQueueItemViewModel> TransferJobs
""",
    "recent activities property",
)
view_model = replace_once(
    view_model,
    """        private void StateStore_PropertyChanged(object sender, PropertyChangedEventArgs e)
""",
    """        private void ActivityService_Changed(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(RecentActivities));
        }

        private void StateStore_PropertyChanged(object sender, PropertyChangedEventArgs e)
""",
    "activity changed handler",
)
view_model_path.write_text(view_model, encoding="utf-8-sig")

# Pass activity service from the plugin.
navigation_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.Navigation.cs")
navigation = navigation_path.read_text(encoding="utf-8-sig")
navigation = replace_once(
    navigation,
    """                    GetTransferExecutor(),
                    settings.Settings)""",
    """                    GetTransferExecutor(),
                    settings.Settings,
                    GetDashboardActivityService())""",
    "dashboard activity dependency",
)
navigation_path.write_text(navigation, encoding="utf-8-sig")

# Add recent activity group to dashboard XAML.
xaml_path = Path("PersonalCloudLibrarySource/Dashboard/CloudLibraryDashboardView.xaml")
xaml = xaml_path.read_text(encoding="utf-8-sig")
xaml = replace_once(
    xaml,
    """                <RowDefinition Height=\"Auto\" />
                <RowDefinition Height=\"Auto\" />
                <RowDefinition Height=\"Auto\" />
            </Grid.RowDefinitions>""",
    """                <RowDefinition Height=\"Auto\" />
                <RowDefinition Height=\"Auto\" />
                <RowDefinition Height=\"Auto\" />
                <RowDefinition Height=\"Auto\" />
            </Grid.RowDefinitions>""",
    "dashboard extra row",
)
xaml = replace_once(
    xaml,
    """            <GroupBox Grid.Row=\"4\"
                      Header=\"{DynamicResource LOCPLSCacheHeading}\"""",
    """            <GroupBox Grid.Row=\"4\"
                      Header=\"{DynamicResource LOCPLSRecentActivity}\"
                      Margin=\"0,0,0,12\">
                <ItemsControl ItemsSource=\"{Binding RecentActivities}\" Margin=\"12\">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Grid Margin=\"0,0,0,8\">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width=\"Auto\" />
                                    <ColumnDefinition Width=\"12\" />
                                    <ColumnDefinition Width=\"*\" />
                                </Grid.ColumnDefinitions>
                                <TextBlock Grid.Column=\"0\" Text=\"{Binding DisplayTime}\" />
                                <TextBlock Grid.Column=\"2\" Text=\"{Binding Message}\" TextWrapping=\"Wrap\" />
                            </Grid>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </GroupBox>

            <GroupBox Grid.Row=\"5\"
                      Header=\"{DynamicResource LOCPLSCacheHeading}\"""",
    "recent activity group",
)
xaml = replace_once(
    xaml,
    '<GroupBox Grid.Row="5" Header="{DynamicResource LOCPLSQuickActions}">',
    '<GroupBox Grid.Row="6" Header="{DynamicResource LOCPLSQuickActions}">',
    "quick actions row shift",
)
xaml_path.write_text(xaml, encoding="utf-8-sig")

# Add localization key.
localization_path = Path("PersonalCloudLibrarySource/Localization/en_US.xaml")
localization = localization_path.read_text(encoding="utf-8-sig")
localization = replace_once(
    localization,
    '    <sys:String x:Key="LOCPLSCacheHeading">Cache</sys:String>',
    '    <sys:String x:Key="LOCPLSRecentActivity">Recent activity</sys:String>\n    <sys:String x:Key="LOCPLSCacheHeading">Cache</sys:String>',
    "recent activity localization",
)
localization_path.write_text(localization, encoding="utf-8-sig")
