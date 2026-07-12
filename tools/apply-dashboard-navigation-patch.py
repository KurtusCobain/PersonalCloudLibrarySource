from pathlib import Path

path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.cs")
text = path.read_text(encoding="utf-8-sig")

replacements = [
    (
        "public class PersonalCloudLibrarySource : LibraryPlugin",
        "public partial class PersonalCloudLibrarySource : LibraryPlugin",
    ),
    (
        "private PersonalCloudLibrarySourceSettingsViewModel settings { get; set; }",
        "private PersonalCloudLibrarySourceSettingsV3ViewModel settings { get; set; }",
    ),
    (
        "public override LibraryClient Client { get; } = new PersonalCloudLibrarySourceClient();",
        "public override LibraryClient Client { get; }",
    ),
    (
        "settings = new PersonalCloudLibrarySourceSettingsViewModel(this);\n\n            Properties = new LibraryPluginProperties",
        "settings = new PersonalCloudLibrarySourceSettingsV3ViewModel(this);\n            InitializeDashboardNavigation();\n            Client = new PersonalCloudLibrarySourceClient(navigationService.OpenDashboard);\n\n            Properties = new LibraryPluginProperties",
    ),
]

for old, new in replacements:
    if new in text:
        continue
    if old not in text:
        raise SystemExit(f"Expected source text was not found: {old}")
    text = text.replace(old, new, 1)

path.write_text(text, encoding="utf-8-sig")
