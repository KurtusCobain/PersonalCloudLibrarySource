# Changelog

## Unreleased

- Added `tools/generate-manifest.ps1` as the primary public generator for local folders, external drives, mapped drives, NAS paths, and rclone remotes.
- Kept `tools/generate-rclone-manifest.ps1` as a thin compatibility wrapper over the universal generator.
- Added manifest version 3 sample output with `generatedBy`, `generatedAt`, `sourceMode`, `itemCount`, `sourceType`, and optional `packageRole`.
- Added directory-package detection for disc folders, PC folders, and Wii U `code/content/meta` packages in generated manifests.
- Added local directory-copy and rclone directory-copy install behavior for manifest items with `sourceType = directory`.
- Kept backward compatibility for older manifests that omit `sourceType`.
- Manifest generation update completed 6/2/2026

## 0.1.1

- Added provider-based import modes for LocalFile, LocalFolder, and RcloneRemote.
- Added manual `Download to local cache` support for missing entries with source paths.
- Added `sourcePath` and `cachePath` manifest fields while keeping legacy compatibility.
- Added optional import diagnostics.
- Added configurable library display name.
- Added public prerelease packaging script and documentation.

## 0.1.0

- Initial MVP.
- Local JSON manifest import.
- Settings UI.
- Fake sample manifest.
- Fake local cache launchers for testing.
