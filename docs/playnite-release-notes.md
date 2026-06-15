# Playnite Release Notes

Manifest generation update completed 6/2/2026

## 0.2.0

Personal Cloud Library Source imports a user-supplied JSON manifest into Playnite as a normal library source.

This release packages the guided setup, universal manifest generation, verification reports, and settings theme polish updates for the Playnite add-on browser.

Highlights:

- LocalFile, LocalFolder, and RcloneRemote provider modes.
- Universal manifest generation with `tools/generate-manifest.ps1`.
- Generic rclone support for cloud providers.
- Local folder, external drive, mounted drive, and NAS support.
- Cloud-only entries appear as uninstalled.
- Cached entries launch through Playnite.
- Manual `Download to local cache` action for supported missing entries.
- Optional import diagnostics.
- Verification report output for setup and troubleshooting.
- Dark-theme readability fixes for the settings status card.

Limitations:

- No automatic download before launch.
- No native cloud provider APIs.
- No bundled content.
