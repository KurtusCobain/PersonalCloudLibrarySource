from pathlib import Path

path = Path("PersonalCloudLibrarySource/PersonalCloudLibrarySource.Navigation.cs")
text = path.read_text(encoding="utf-8-sig")

replacements = [
    (
        "ActiveTransferCount = 0,\n                    FailedTransferCount = 0,",
        "ActiveTransferCount = GetActiveTransferCount(),\n                    FailedTransferCount = GetFailedTransferCount(),",
    ),
    (
        "settings.Settings.PropertyChanged -= DashboardSettings_PropertyChanged;\n            }\n        }",
        "settings.Settings.PropertyChanged -= DashboardSettings_PropertyChanged;\n            }\n\n            DisposeTransferManager();\n        }",
    ),
    (
        "private void DashboardSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)\n        {\n            RefreshDashboardState();",
        "private void DashboardSettings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)\n        {\n            SynchronizeTransferManagerSettings();\n            RefreshDashboardState();",
    ),
    (
        "if (dashboardSidebarItem != null)\n            {\n                dashboardSidebarItem.Visible = settings.Settings.ShowSidebarDashboard;\n            }\n        }",
        "if (dashboardSidebarItem != null)\n            {\n                dashboardSidebarItem.Visible = settings.Settings.ShowSidebarDashboard;\n            }\n\n            UpdateSidebarTransferProgress();\n        }",
    ),
]

for old, new in replacements:
    if new in text:
        continue
    if old not in text:
        raise SystemExit(f"Transfer dashboard patch target missing: {old}")
    text = text.replace(old, new, 1)

path.write_text(text, encoding="utf-8-sig")
