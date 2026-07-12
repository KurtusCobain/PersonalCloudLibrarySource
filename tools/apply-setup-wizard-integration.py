from pathlib import Path

nav_path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.Navigation.cs")
nav = nav_path.read_text(encoding="utf-8-sig")

replacements = [
    (
        "private CloudLibraryDashboardWindowService dashboardWindowService;\n        private PluginNavigationService navigationService;",
        "private CloudLibraryDashboardWindowService dashboardWindowService;\n        private SetupWizardWindowService setupWizardWindowService;\n        private PluginNavigationService navigationService;",
    ),
    (
        "dashboardWindowService = new CloudLibraryDashboardWindowService(playniteApi, CreateDashboardView);\n            navigationService = new PluginNavigationService(",
        "dashboardWindowService = new CloudLibraryDashboardWindowService(playniteApi, CreateDashboardView);\n            setupWizardWindowService = new SetupWizardWindowService(\n                playniteApi,\n                settings,\n                GetDefaultLocalCacheFolder,\n                SetupWizardCompleted);\n            navigationService = new PluginNavigationService(",
    ),
    (
        "settings.ShowUpdateLibraryInstructions,\n                OpenSourceLocationFromDashboard);",
        "settings.ShowUpdateLibraryInstructions,\n                OpenSourceLocationFromDashboard,\n                setupWizardWindowService.OpenWizard);",
    ),
    (
        "yield return CreateMainMenuItem(menuSection, \"LOCPLSOpenDashboard\", \"Open Dashboard\", navigationService.OpenDashboard, iconPath);\n            yield return CreateMainMenuItem(menuSection, \"LOCPLSUpdateLibraryHelp\"",
        "yield return CreateMainMenuItem(menuSection, \"LOCPLSOpenDashboard\", \"Open Dashboard\", navigationService.OpenDashboard, iconPath);\n            yield return CreateMainMenuItem(menuSection, \"LOCPLSRunSetupWizard\", \"Run Setup Wizard\", navigationService.RunSetupWizard, iconPath);\n            yield return CreateMainMenuItem(menuSection, \"LOCPLSUpdateLibraryHelp\"",
    ),
    (
        "private void OpenSourceLocationFromDashboard()",
        "private void SetupWizardCompleted()\n        {\n            try\n            {\n                var pluginSettings = settings.Settings;\n                if (string.Equals(\n                        GetProviderType(pluginSettings),\n                        PersonalCloudLibrarySourceSettings.LocalFolderProviderType,\n                        StringComparison.OrdinalIgnoreCase) &&\n                    string.IsNullOrWhiteSpace(pluginSettings.LocalManifestPath))\n                {\n                    var report = GenerateManifestFromFolder(pluginSettings.LocalLibraryRoot);\n                    pluginSettings.LocalManifestPath = report.OutputPath;\n                    pluginSettings.ManifestRelativePath = string.Empty;\n                    pluginSettings.LastGeneratedManifestPath = report.OutputPath;\n                    pluginSettings.LastGeneratedReportPath = report.ReportPath;\n                    pluginSettings.LastManifestGeneratedAt = report.Manifest.GeneratedAt;\n                    pluginSettings.LastManifestItemCount = report.ItemCount;\n                    settings.EndEdit();\n                }\n\n                RefreshDashboardState();\n                UpdateNavigationItemState();\n                dashboardWindowService.OpenDashboard();\n                playniteApi.Dialogs.ShowMessage(\n                    \"Setup was saved successfully. Run Update Game Library in Playnite to import or refresh the catalog.\",\n                    GetDashboardResource(\"LOCPLSSetupWizardTitle\", \"Personal Cloud Library Setup\"));\n            }\n            catch (Exception ex)\n            {\n                logger.Error(ex, \"Personal Cloud Library Source could not complete guided setup.\");\n                RefreshDashboardState();\n                UpdateNavigationItemState();\n                playniteApi.Dialogs.ShowErrorMessage(\n                    \"Setup was saved, but manifest generation did not finish: \" + ex.Message,\n                    GetDashboardResource(\"LOCPLSSetupWizardTitle\", \"Personal Cloud Library Setup\"));\n            }\n        }\n\n        private void OpenSourceLocationFromDashboard()",
    ),
]

for old, new in replacements:
    if new in nav:
        continue
    if old not in nav:
        raise SystemExit(f"Navigation patch target missing: {old}")
    nav = nav.replace(old, new, 1)

nav_path.write_text(nav, encoding="utf-8-sig")

dashboard_path = Path("PersonalCloudLibrarySource/Dashboard/CloudLibraryDashboardView.xaml")
dashboard = dashboard_path.read_text(encoding="utf-8-sig")
old_button = '''                    <Button Content="{DynamicResource LOCPLSUpdateLibraryHelp}"
                            Command="{Binding ShowUpdateLibraryInstructionsCommand}"'''
new_button = '''                    <Button Content="{DynamicResource LOCPLSRunSetupWizard}"
                            Command="{Binding RunSetupWizardCommand}"
                            Margin="0,0,8,8"
                            Padding="10,5" />
                    <Button Content="{DynamicResource LOCPLSUpdateLibraryHelp}"
                            Command="{Binding ShowUpdateLibraryInstructionsCommand}"'''
if new_button not in dashboard:
    if old_button not in dashboard:
        raise SystemExit("Dashboard setup button insertion target missing")
    dashboard = dashboard.replace(old_button, new_button, 1)
dashboard_path.write_text(dashboard, encoding="utf-8-sig")
