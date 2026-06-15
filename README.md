# Personal Cloud Library Source

Personal Cloud Library Source is a Playnite `GameLibrary` plugin for importing a user-supplied personal cloud, NAS, external-drive, or local library catalog into Playnite.

It supports:

- `LocalFile`
- `LocalFolder`
- `RcloneRemote`

Manifest generation update completed 6/2/2026

## What It Does

- Imports manifest entries into Playnite as a normal library source.
- Preserves stable game IDs from the manifest.
- Represents cached local files as installed and playable.
- Represents missing-local or cloud-only entries safely as uninstalled.
- Supports manual `Download to local cache` actions for eligible items.
- Supports safe `Remove cached copy` uninstall actions for cache-owned paths.
- Generates a v3 manifest from a local folder, external drive, mapped drive, or NAS path from inside Playnite settings.

## What It Does Not Do

This does not provide games, ROMs, BIOS files, cracks, keys, copyrighted content, scraping, storefront access, or download sources.

It does not auto-download an entire cloud library on startup.

It does not delete source files or cloud files.

## Current Release Status

The public Playnite add-on browser package is still `0.1.1`.

The repository now contains the v0.2 guided-setup and manifest-generation pass intended for the next packaged release. See:

- [CHANGELOG.md](CHANGELOG.md)
- [docs/playnite-release-notes.md](docs/playnite-release-notes.md)

## Normal User Setup

For a local folder, external drive, mapped drive, or NAS path:

1. Open Playnite.
2. Open **Add-ons -> Extension settings -> Libraries -> Personal Cloud Library Source**.
3. Choose `LocalFolder`.
4. Browse to your library root.
5. Click **Generate manifest from folder**.
6. Review the summary and save settings.
7. Run **Update Game Library** in Playnite.

For a cloud provider through rclone:

1. Configure your remote with `rclone config`.
2. Choose `RcloneRemote`.
3. Fill in the rclone executable path, remote name, manifest path, and optional content root.
4. Use **Verify setup** or **Test rclone connection**.
5. Run **Update Game Library** in Playnite.

## Guided Manifest Generation

The v0.2 setup flow adds local manifest generation directly inside Playnite settings.

The generated manifest is written to the plugin user data path, not the extension install folder.

Use this for:

- local folders
- external drives
- mapped drives
- NAS paths
- synced local cloud folders

See:

- [docs/setup-wizard.md](docs/setup-wizard.md)
- [docs/automatic-manifest-generation.md](docs/automatic-manifest-generation.md)
- [docs/setup-local-folder.md](docs/setup-local-folder.md)

## Advanced or Manual Manifest Generation

Advanced users can still generate manifests outside Playnite:

- `tools/generate-manifest.ps1`
- `tools/generate-rclone-manifest.ps1`

These tools are useful for scripted workflows or advanced rclone scanning.

See:

- [docs/manifest-format.md](docs/manifest-format.md)
- [docs/setup-rclone.md](docs/setup-rclone.md)

## Cache and Download Behavior

- `sourceType = file` items download with `rclone copyto` in `RcloneRemote` mode.
- `sourceType = directory` items download with `rclone copy` in `RcloneRemote` mode.
- Local-folder copies use local filesystem copy helpers.
- The plugin only reports a `Play` action when the expected local cached launch path exists.
- Cloud-only or missing-local items remain visible as uninstalled when `TreatMissingFilesAsUninstalled` is enabled.

Planned v0.3 notes:

- [docs/auto-cache-before-launch.md](docs/auto-cache-before-launch.md)

## Legal and Use Boundaries

Users are responsible for only indexing files they own or have rights to use.

Cloud providers and rclone remotes are configured by the user.

See:

- [docs/legal-use.md](docs/legal-use.md)

## Troubleshooting

See:

- [docs/troubleshooting.md](docs/troubleshooting.md)

## Documentation Index

- [docs/setup-wizard.md](docs/setup-wizard.md)
- [docs/automatic-manifest-generation.md](docs/automatic-manifest-generation.md)
- [docs/setup-local-folder.md](docs/setup-local-folder.md)
- [docs/setup-rclone.md](docs/setup-rclone.md)
- [docs/manifest-format.md](docs/manifest-format.md)
- [docs/legal-use.md](docs/legal-use.md)
- [docs/troubleshooting.md](docs/troubleshooting.md)

## Development and Packaging

See:

- [DEVELOPMENT.md](DEVELOPMENT.md)
- [CONTRIBUTING.md](CONTRIBUTING.md)
- [SECURITY.md](SECURITY.md)

The repo includes:

- `PersonalCloudLibrarySource/extension.yaml`
- `playnite-addon/addon-database.yaml`
- `playnite-addon/installer.yaml`
- `tools/package-extension.ps1`

`AddonId` remains:

`PersonalCloudLibrarySource_61993828-67a8-4468-93a2-293442e36328`
