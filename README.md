# Personal Cloud Library Source

Personal Cloud Library Source (PCLS) is a Playnite `GameLibrary` plugin that imports a user-supplied manifest for a personal cloud, NAS, external-drive, or local library. It catalogs content; it does not provide or stream games.

PCLS is the Playnite library for files you already own: catalog games from your local folder, external drive, NAS, or rclone remote; **Install** copies a selected game into this PC's cache, **Play** launches that local copy, and **Uninstall**, with the default cache-contained policy, removes an authorized cached copy while retaining the original source and catalog entry. The advanced outside-cache uninstall option can delete external data and should normally remain disabled. Source files are not provided, hosted, or streamed by PCLS.

## Current status

**PCLS v0.3.2 was publicly released on November 1, 2026.** Install or update it through the existing Playnite add-on entry or get the verified [v0.3.2 GitHub release](https://github.com/KurtusCobain/PersonalCloudLibrarySource/releases/tag/v0.3.2). The public installer manifest retains v0.2.0 as an earlier package. The GitHub package and both published installer/add-on manifests have passed Windows release qualification, including official Playnite Toolbox verification.

Windows acceptance covered the Desktop and Fullscreen game workflow, an isolated v0.2.0 upgrade and local-only rclone cancellation/retry. Recursive directory cache uninstall remains disabled, third-party cloud/NAS cases vary by provider, and some keyboard/high-DPI scenarios remain untested. The future **1.0 release candidate** is a separate milestone. See [Current release status](docs/release-status.md) and [Known limits](docs/known-limits.md).

## v0.3.2 UI tour

The images below are existing repository demonstration assets; the separately captured genuine November 2026 Windows QA screenshots are staged for privacy and visual review before publication in the upstream Playnite listing.

### Guided setup

Choose an existing manifest or a library root in the Desktop setup wizard.

![PCLS setup wizard source selection](docs/images/pcls-setup-source.png)

### Provider and manifest settings

Provider-specific fields keep local-file, local-folder, and rclone configuration focused.

![PCLS provider and manifest settings](docs/images/pcls-settings-provider.png)

### Guarded cache behavior

The cache page groups install, verification, cleanup, concurrency, and safe uninstall controls.

![PCLS cache safety settings](docs/images/pcls-settings-cache-safety.png)

### Dashboard and transfers

The Desktop dashboard summarizes the source, catalog, cache, warnings, queue, and recent activity.

![PCLS dashboard overview](docs/images/pcls-dashboard-overview.png)

![PCLS completed transfer activity](docs/images/pcls-dashboard-transfer-activity.png)

### Normal Playnite controls

Catalog-only and cached games appear in the normal Desktop library. Standard Playnite controls also remain available in Fullscreen after setup is completed in Desktop.

![PCLS example games in Playnite Desktop](docs/images/pcls-library-example-games.png)

![PCLS cached example game in Playnite Fullscreen](docs/images/pcls-fullscreen-example-games.png)

## Supported providers

- `LocalFile`: reads a JSON manifest from a local path. Content sources may be absolute or relative to the manifest folder.
- `LocalFolder`: reads a manifest beneath a library root and copies selected files or directories into the local cache.
- `RcloneRemote`: reads and downloads through a user-configured rclone remote.

Rclone transfers require rclone 1.63 or newer (1.75.2 verified). PCLS uses `--inplace` only for its private job-owned staging path, then verifies and publishes the completed cache entry. This prevents rclone upload sidecars from surviving cancellation.

Local folders can point at fixed disks, external drives, mapped drives, UNC/NAS paths, or synced cloud folders. Availability, credentials, and stable drive mappings remain the user's responsibility.

## Setup

Open **Add-ons -> Extension settings -> Libraries -> Personal Cloud Library Source** in Playnite Desktop, or use the Desktop setup wizard.

For local or NAS content:

1. Select `LocalFile` or `LocalFolder`.
2. Choose the manifest file or library root and relative manifest path.
3. Choose a local cache folder.
4. Run **Verify setup**, save settings, and run **Update Game Library**.

For `RcloneRemote`:

1. Install rclone separately and run `rclone config`.
2. Confirm the remote with `rclone listremotes` and test the manifest with `rclone cat remote:path/to/manifest.json`.
3. Enter the executable path, remote name, manifest path, optional content root, and timeout in PCLS.
4. Run **Test rclone connection** and **Verify setup**, save settings, and update the library.

The default configured rclone timeout is 90 seconds, with accepted values from 5 to 300 seconds. Manifest `rclone cat` reads and the settings `rclone listremotes` test use it as a total process deadline. Queued downloads instead require first output or error activity within at most 30 seconds, then apply the configured timeout between later activity; an active transfer may therefore run longer than 90 seconds.

Detailed guides:

- [Setup wizard](docs/setup-wizard.md)
- [Local folder, external drive, and NAS setup](docs/setup-local-folder.md)
- [Rclone setup](docs/setup-rclone.md)
- [Manifest format](docs/manifest-format.md)
- [Automatic manifest generation](docs/automatic-manifest-generation.md)

## Game and cache behavior

Manifest entries import as normal Playnite games with stable IDs. A valid cached launch path is shown as installed and playable; a missing cache path can remain visible as uninstalled. Eligible entries can be copied or downloaded to the cache. Transfers are queued, can be cancelled or retried, and verify destination existence/size before completion.

Cache deletion is deliberately narrow. With **default settings**, Uninstall removes only an authorized cached file or install folder. It refuses filesystem roots, the cache root itself, paths outside the configured cache, and unsafe reparse-point paths. A separate, advanced **allow outside-cache uninstall** setting widens permitted deletion destinations and should stay disabled for normal use. Even with that setting, the candidate refuses known local source-item paths and library roots; however, it cannot guarantee the safety of every unrelated external path. Rclone remote content is never deleted by this uninstall action. Do not direct external-cache uninstall at your source storage.

## Desktop and Fullscreen

Desktop owns setup, settings, manifest generation, verification reports, the dashboard, and custom details views. Fullscreen does not have a dedicated setup wizard or dashboard.

Imported game metadata and standard Playnite play/install/uninstall controllers do not depend on Desktop-only windows. Notifications provide workflow feedback outside those windows. Automated tests and disposable Windows QA observed imported-game Play actions in Fullscreen. Additional keyboard/controller-navigation and alternate-DPI acceptance remains outstanding.

## Upgrades

Settings migration is sequential and preserves configured values while applying safer defaults. In particular, the former default 30-second rclone timeout migrates to 90 seconds, while custom timeout values remain unchanged. Back up Playnite data before upgrading and verify provider/cache paths afterward. See [Upgrades](docs/upgrades.md).

## Troubleshooting, diagnostics, and reports

Start with **Verify setup** and review the latest verification report. Import diagnostics, when enabled, and generated reports are written beneath the plugin user-data directory, never the extension install directory. Reports use summaries and capped samples rather than dumping a complete private manifest.

- [Troubleshooting](docs/troubleshooting.md)
- [Reports and diagnostics](docs/reports-and-diagnostics.md)

## Legal-use boundary

PCLS does not provide games, ROMs, BIOS files, cracks, keys, copyrighted content, storefront access, scraping, or download sources. Users must only index and copy content they own or have rights to use and must configure their own storage providers. See [Legal use](docs/legal-use.md).

## Known limits

PCLS is a catalog-and-cache workflow, not streaming. It does not auto-download a whole library at startup. Dedicated Fullscreen management UI is out of scope. Removable/mapped/NAS availability and real-provider behavior depend on the environment and still require installed qualification. See the current [Known limits](docs/known-limits.md) and proposed [roadmap to 1.0](docs/roadmap-v1.0.md).

## Development and packaging

See [Development](DEVELOPMENT.md), [Contributing](CONTRIBUTING.md), and [Security](SECURITY.md). Distribution metadata lives in `PersonalCloudLibrarySource/extension.yaml` and `playnite-addon/`; the stable `AddonId` is `PersonalCloudLibrarySource_61993828-67a8-4468-93a2-293442e36328`.
