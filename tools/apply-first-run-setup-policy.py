from pathlib import Path


def replace_once(text, old, new, label):
    if new in text:
        return text
    if old not in text:
        raise SystemExit(f"Patch target missing for {label}: {old}")
    return text.replace(old, new, 1)

# Register test and production service.
tests_path = Path("PersonalCloudLibrarySource.Tests/PersonalCloudLibrarySource.Tests.csproj")
tests = tests_path.read_text(encoding="utf-8-sig")
tests = replace_once(
    tests,
    '    <Compile Include="Setup\\SetupWizardViewModelTests.cs" />',
    '    <Compile Include="Setup\\SetupLaunchPolicyServiceTests.cs" />\n    <Compile Include="Setup\\SetupWizardViewModelTests.cs" />',
    "setup policy test registration",
)
tests_path.write_text(tests, encoding="utf-8-sig")

project_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.csproj")
project = project_path.read_text(encoding="utf-8-sig")
project = replace_once(
    project,
    '    <Compile Include="Services\\PluginNavigationService.cs" />',
    '    <Compile Include="Services\\PluginNavigationService.cs" />\n    <Compile Include="Services\\SetupLaunchPolicyService.cs" />',
    "setup policy service registration",
)
project_path.write_text(project, encoding="utf-8-sig")

# Add persistent setup state fields and properties.
settings_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySourceSettingsV3.cs")
settings = settings_path.read_text(encoding="utf-8-sig")
settings = replace_once(
    settings,
    "        private bool showSetupReminders = true;",
    "        private bool showSetupReminders = true;\n        private bool setupWizardCompleted;\n        private bool setupWizardDismissed;",
    "setup state fields",
)
settings = replace_once(
    settings,
    """        public bool ShowSetupReminders
        {
            get => showSetupReminders;
            set => SetValue(ref showSetupReminders, value);
        }
""",
    """        public bool ShowSetupReminders
        {
            get => showSetupReminders;
            set => SetValue(ref showSetupReminders, value);
        }

        public bool SetupWizardCompleted
        {
            get => setupWizardCompleted;
            set => SetValue(ref setupWizardCompleted, value);
        }

        public bool SetupWizardDismissed
        {
            get => setupWizardDismissed;
            set => SetValue(ref setupWizardDismissed, value);
        }
""",
    "setup state properties",
)
settings_path.write_text(settings, encoding="utf-8-sig")

# Mark successful wizard completion.
wizard_vm_path = Path("PersonalCloudLibrarySource/Setup/SetupWizardViewModel.cs")
wizard_vm = wizard_vm_path.read_text(encoding="utf-8-sig")
wizard_vm = replace_once(
    wizard_vm,
    "            Draft.ApplyTo(settings);",
    "            Draft.ApplyTo(settings);\n            settings.SetupWizardCompleted = true;\n            settings.SetupWizardDismissed = false;",
    "wizard completion state",
)
wizard_vm_path.write_text(wizard_vm, encoding="utf-8-sig")

# Remember dismissal when the window closes without successful completion.
window_path = Path("PersonalCloudLibrarySource/Setup/SetupWizardWindowService.cs")
window = window_path.read_text(encoding="utf-8-sig")
window = replace_once(
    window,
    "            window.ShowDialog();",
    """            window.ShowDialog();
            if (!settings.Settings.SetupWizardCompleted)
            {
                settings.Settings.SetupWizardDismissed = true;
                plugin.SavePluginSettings(settings.Settings);
            }""",
    "wizard dismissal state",
)
window_path.write_text(window, encoding="utf-8-sig")

# Apply startup policy in the Playnite navigation lifecycle.
navigation_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.Navigation.cs")
navigation = navigation_path.read_text(encoding="utf-8-sig")
navigation = replace_once(
    navigation,
    """        private CloudLibrarySidebarItem dashboardSidebarItem;
""",
    """        private CloudLibrarySidebarItem dashboardSidebarItem;
        private readonly SetupLaunchPolicyService setupLaunchPolicyService = new SetupLaunchPolicyService();
        private bool setupLaunchHandled;
""",
    "startup policy fields",
)
navigation = replace_once(
    navigation,
    "            InitializeDashboardNavigation();",
    "            InitializeDashboardNavigation();\n            HandleSetupLaunchPolicy();",
    "startup policy invocation",
)
navigation = replace_once(
    navigation,
    """        private void InitializeDashboardNavigation()
""",
    """        private void HandleSetupLaunchPolicy()
        {
            if (setupLaunchHandled || settings?.Settings == null)
            {
                return;
            }

            setupLaunchHandled = true;
            System.Collections.Generic.List<string> errors;
            var setupValid = settings.VerifySettings(out errors);
            if (setupValid)
            {
                if (!settings.Settings.SetupWizardCompleted || settings.Settings.SetupWizardDismissed)
                {
                    settings.Settings.SetupWizardCompleted = true;
                    settings.Settings.SetupWizardDismissed = false;
                    SavePluginSettings(settings.Settings);
                }

                return;
            }

            var action = setupLaunchPolicyService.Evaluate(new SetupLaunchContext
            {
                PluginEnabled = settings.Settings.Enabled,
                SetupValid = false,
                SetupCompleted = settings.Settings.SetupWizardCompleted,
                SetupDismissed = settings.Settings.SetupWizardDismissed,
                ShowReminders = settings.Settings.ShowSetupReminders
            });

            if (action == SetupLaunchAction.OpenWizard)
            {
                navigationService.RunSetupWizard();
                return;
            }

            if (action == SetupLaunchAction.ShowReminder)
            {
                playniteApi.Notifications.Add(new Playnite.SDK.NotificationMessage(
                    "PersonalCloudLibrarySource_SetupReminder",
                    "Personal Cloud Library setup needs attention. Click to continue setup.",
                    Playnite.SDK.NotificationType.Info,
                    navigationService.RunSetupWizard));
            }
        }

        private void InitializeDashboardNavigation()
""",
    "startup policy implementation",
)
navigation_path.write_text(navigation, encoding="utf-8-sig")
