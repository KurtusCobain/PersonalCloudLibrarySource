# Changelog

## 0.3.2 - 2026-11-01 (Public Release)

**Published release — includes the hardening and Windows QA fixes merged from PR #10:**

- Restrict new install destinations to the configured cache folder, reject unsafe fallback game IDs, and retain catalog visibility for existing external paths.
- Correct rclone transfers for empty files/directories, Windows argument quoting, and bounded cloud manifest output.
- Apply explicit local/remote manifest limits and prevent stale verification snapshots from replacing live dashboard cached counts.
- Reject local folder transfers that could follow junctions/symlinks outside the chosen source, and prevent folder copies into their own source tree; ensure rejected installs terminate the Playnite install lifecycle.
- Remove seven nonfunctional settings controls while keeping upgrade-compatible stored fields; explain mandatory transfer-safety behavior.
- Add a bounded, cancellable read-only game preview to first-run setup, with the first ten detected titles, skipped counts, and clear remote prerequisites.
- Move local/NAS manifest generation during wizard Finish to a cancellable background task, retaining Playnite settings updates on the UI thread and preventing persistence after cancellation.
- Observe queued install completion asynchronously and route Playnite completion/cancellation events through the host UI dispatcher; confirmed in isolated Windows Playnite acceptance.
- Restore compatibility with screenshot URLs in the live Playnite listing and preserve genuine Windows QA capture evidence.
- **Compatibility note:** manifests that previously attempted an install outside the cache folder now have to use an inside-cache destination. Existing files must never be relocated or deleted automatically.

- Added guided Desktop setup for `LocalFile`, `LocalFolder`, and `RcloneRemote` providers, with provider-specific validation and verification reports.
- Centralized manifest loading, parsing, path resolution, import outcomes, and sequential settings migration.
- Added queue-owned local and rclone transfers with cancellation, retry, progress, partial-file cleanup, and destination verification.
- Added the Desktop dashboard with source health, library/cache counts, transfer queue status, bounded recent activity, diagnostics, and report shortcuts.
- Kept imported games compatible with standard Playnite metadata plus play/install/uninstall controls in Desktop and Fullscreen.
- Added guarded cache removal and safe uninstall rules that reject roots, the cache root, outside-cache targets by default, and unsafe reparse-point paths.
- Added clearer notifications, public-safe diagnostics, upgrade guidance, and a seven-image release-candidate documentation set.
- Windows QA completed core installed Desktop/Fullscreen workflows, upgrade preservation, local-only rclone cancellation/retry and package validation. Broader cloud/NAS scenarios and some keyboard/DPI checks remain documented limitations, and this is not a 1.0 release.

### Earlier 0.3.2 development milestones

Feature-branch test pass prepared 7/12/2026. These improvements are included in the published 0.3.2 update.

- Added the approved PCLS cloud-controller icon, wide wordmark, and full dark logo artwork.
- Generate true-alpha PNG branding during builds instead of using checkerboard-backed source images.
- Improved setup-wizard and settings readability with Playnite theme foreground resources.
- Show only the settings fields required by the selected source provider.
- Synchronize the latest verification result with dashboard status and counts.
- Include actionable manifest and rclone timeout details in verification messages.
- Increased the default rclone timeout to 90 seconds and safely migrated the previous 30-second default while preserving custom values.
- Include generated branding assets in extension packages.

### Final Windows QA and release qualification

- Fixed deep Windows ownership-receipt staging paths and required LICENSE validation.
- Corrected the game Details dark-theme text resource binding.
- Prevented rclone cancellation sidecar leftovers using private staged transfers and a local-only real-rclone regression.
- Verified v0.2.0 → v0.3.2 migration preserved tested game IDs, settings and file hashes.
- Published the v0.3.2 package to GitHub and retained the prior 0.2.0 version in the Playnite installer manifest.
- Recursive directory cache uninstall remains disabled; shared-cache install-size display can include unrelated files.

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
