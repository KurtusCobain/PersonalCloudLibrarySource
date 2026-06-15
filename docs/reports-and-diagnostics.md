# Reports and Diagnostics

Personal Cloud Library Source writes troubleshooting output to the plugin user data folder, not to the extension install folder.

## Report Types

### Verification report

Use **Verify setup / generate report** in the settings screen to create:

```text
<Playnite plugin user data>\reports\latest-verification-report.txt
```

The verification report summarizes:

- selected provider mode
- manifest source description
- manifest load success or failure
- manifest version when available
- item counts and warning counts
- duplicate ID and missing-field counts
- cache/download eligibility counts
- path-resolution warnings
- cache safety summary
- limited metadata-gap summaries

The report is intentionally public-safe. It uses counts and capped warning samples instead of dumping the full manifest inventory by default.

### Import diagnostics

When **Enable import diagnostics** is turned on, the plugin writes:

```text
<Playnite plugin user data>\diagnostics\last-import-diagnostics.txt
```

This file is useful when Playnite library updates run but imported items do not behave as expected.

### Generated manifest files

When you use **Generate manifest from folder**, the plugin writes generated files under:

```text
<Playnite plugin user data>\manifests\
```

Typical files:

- `personal-cloud-library.generated.json`
- `personal-cloud-library.generated.report.txt`

## Safer Writes and Backups

Plugin-generated manifests and reports use a temp-file write followed by replace or move into place.

If an existing generated file or verification report is being replaced, the plugin can create lightweight timestamped backups under:

```text
<Playnite plugin user data>\backups\
```

These backups are for plugin-generated outputs only. The plugin does not back up or delete source-provider files, cloud files, ROMs, BIOS files, keys, cracks, or other personal content.

## Recommended Workflow

1. Configure the provider settings.
2. Run **Verify setup / generate report**.
3. Review the verification report if setup needs attention.
4. Generate a manifest from a local or NAS folder if needed.
5. Run **Update Game Library** in Playnite.
6. Check diagnostics only if library-update behavior still looks wrong.
