# Personal Cloud Library Source 1.0

**Release-candidate draft.** The installed extension still identifies as 0.3.2 while the final 1.0 qualification matrix is completed. These notes describe the intended 1.0 scope and are not a publication announcement.

## What is included

- Guided Desktop setup and provider-specific validation for `LocalFile`, `LocalFolder`, and `RcloneRemote`.
- Catalog-first import so remote entries can receive Playnite metadata before they are cached.
- Queue-owned local and rclone transfers with cancellation, retry, progress, partial-file cleanup, and verification.
- A Desktop dashboard for source health, catalog/cache counts, transfer state, recent activity, reports, and diagnostics.
- Standard Playnite play/install/uninstall controls for imported games in Desktop and Fullscreen.
- Safe uninstall behavior that removes only authorized cached files or install folders and never source or remote content.
- Sequential settings migration that preserves configured values while applying safer defaults.
- Public-safe verification reports and capped diagnostics stored beneath plugin user data.

## Release boundary

PCLS catalogs a user-supplied personal library and copies selected content into a guarded local cache. It does not stream gameplay, provide content or cloud accounts, scrape storefronts, or download an entire library automatically.

Before final release notes are published, the release checklist must record installed Desktop, Fullscreen, provider, upgrade, and package qualification. No row should be described as passing before that evidence exists.

Historical release details remain in [the changelog](../CHANGELOG.md).
