# Playnite Add-on Listing Reference

![Personal Cloud Library Source workflow](images/pcls-workflow.png)

## Short Description

Import a user-supplied cloud, NAS, external drive, or local manifest into Playnite's normal library view.

Manifest generation update completed 6/2/2026

## Long Description

Personal Cloud Library Source imports a user-supplied cloud, NAS, external-drive, or local manifest into Playnite's normal library view. Cloud-only entries can appear before download, be enriched with Playnite metadata, downloaded or copied to a local cache when needed, launched locally, and later uninstalled from cache while keeping the catalog entry.

This does not provide games, ROMs, BIOS files, cracks, keys, copyrighted content, scraping, storefront access, or download sources. It catalogs user-supplied entries and copies or downloads user-owned files from configured local folders or rclone remotes.

## Features

- Normal Playnite library source integration.
- LocalFile, LocalFolder, and RcloneRemote provider modes.
- Support for local folders, external drives, mapped drives, NAS shares, and synced cloud folders.
- Generic rclone support for Google Drive, OneDrive, Dropbox, and other rclone remotes.
- In-Playnite manifest generation for local folders, external drives, mapped drives, and NAS roots.
- Advanced manual manifest generation tools for scripted local and rclone workflows.
- Cloud-only entries import as uninstalled.
- Cached entries launch locally through Playnite.
- Manual download/cache action for supported missing entries.
- Manual cache cleanup/uninstall action for installed cached entries.
- Optional import diagnostics.
- Configurable library display name.

## Requirements

- Playnite.
- A manifest file, either generated or user-created.
- rclone installed and configured only when using RcloneRemote mode.
- Local access to files when using LocalFile or LocalFolder mode.

## What It Does Not Provide

This does not provide games, ROMs, BIOS files, cracks, keys, copyrighted content, scraping, storefront access, or download sources.

## Provider Examples

- `LocalFile`: read one manifest JSON file from disk.
- `LocalFolder`: read a manifest and copy files from an external drive, mapped drive, NAS share, or synced cloud folder.
- `RcloneRemote`: read a manifest with `rclone cat` and copy selected files with `rclone copyto` or `rclone copy`.

## Setup Summary

1. Install the add-on.
2. Choose a provider mode in settings.
3. Point the plugin at a manifest.
4. Set a local cache folder.
5. Run Playnite's library update.
6. Use Playnite metadata tools to enrich imported entries.
7. Download or cache selected entries when ready to play.
8. Remove cached copies later without removing manifest entries or Playnite metadata.

## Legal and Use Disclaimer

Personal Cloud Library Source is for user-supplied personal libraries only. Cloud providers and rclone remotes are configured by the user.

## Suggested Tags

- Library source
- Cloud library
- Local library
- rclone
- NAS
- External drive
- Metadata organization
