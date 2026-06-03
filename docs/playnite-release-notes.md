# Playnite Release Notes

Manifest generation update completed 6/2/2026

## 0.1.1

Personal Cloud Library Source imports a user-supplied JSON manifest into Playnite as a normal library source.

Highlights:

- LocalFile, LocalFolder, and RcloneRemote provider modes.
- Universal manifest generation with `tools/generate-manifest.ps1`.
- Generic rclone support for cloud providers.
- Local folder, external drive, mounted drive, and NAS support.
- Cloud-only entries appear as uninstalled.
- Cached entries launch through Playnite.
- Manual `Download to local cache` action for supported missing entries.
- Optional import diagnostics.

Limitations:

- No automatic download before launch.
- No native cloud provider APIs.
- No bundled content.
