# Changelog

## 0.3.2

Feature-branch test pass prepared 7/12/2026

- Added the approved PCLS cloud-controller icon, wide wordmark, and full dark logo artwork.
- Generate true-alpha PNG branding during builds instead of using checkerboard-backed source images.
- Improved setup-wizard and settings readability with Playnite theme foreground resources.
- Show only the settings fields required by the selected source provider.
- Synchronize the latest verification result with dashboard status and counts.
- Include actionable manifest and rclone timeout details in verification messages.
- Increased the default rclone timeout to 90 seconds and safely migrated the previous 30-second default while preserving custom values.
- Include generated branding assets in extension packages.

## 0.2.0

Manifest generation update completed 6/2/2026

- Added `tools/generate-manifest.ps1` as the primary public generator for local folders, external drives, mapped drives, NAS paths, and rclone remotes.
- Kept `tools/generate-rclone-manifest.ps1` as a thin compatibility wrapper over the universal generator.
- Added manifest version 3 sample output with `generatedBy`, `generatedAt`, `sourceMode`, `itemCount`, `sourceType`, and optional `packageRole`.
- Added directory-package detection for disc folders, PC folders, and Wii U `code/content/meta` packages in generated manifests.
- Added local directory-copy and rclone directory-copy install behavior for manifest items with `sourceType = directory`.
- Kept backward compatibility for older manifests that omit `sourceType`.
- Added setup verification reports and safer plugin-side file writes.
- Improved the settings status card styling for Playnite dark theme readability.

## 0.1.1

- Added provider-based import modes:
  - `LocalFile`
  - `LocalFolder`
  - `RcloneRemote`
- Added manual **Download to local cache** support for missing entries with valid source paths.
- Added `sourcePath` and `cachePath` manifest fields while keeping legacy manifest compatibility.
- Added optional import diagnostics.
- Added configurable library display name.
- Added packaging script and release documentation.
- Updated documentation for public installation through Playnite's official add-on browser.

## 0.1.0

- Initial MVP release.
- Added local JSON manifest import.
- Added settings UI.
- Added fake sample manifest for testing.
- Added fake local cache launchers for testing.
