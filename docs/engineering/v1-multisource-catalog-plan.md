# PCLS v1.0 multi-source runtime — coding checkpoint

**Status:** Experimental, default-off draft implementation in [PR #22](https://github.com/KurtusCobain/PersonalCloudLibrarySource/pull/22). **Not approved for use, merge or public release.** Public v0.3.2 remains unchanged. This is not a native Playnite acceptance result.

## What has been coded

- **Stable source identity:** `SourceProfileIdentityPolicy` retains all legacy single-source GameIds, assigns source-namespaced IDs to added profiles, refuses duplicate profile IDs and prevents game-ID collisions.
- **Versioned source settings:** `SourceProfilesConfigurationCodec` serializes/deserializes a bounded version-1 profile document, rejects absent/unsupported schema and malformed IDs, and previews legacy migration without writing live settings.
- **Atomic file storage:** `SourceProfilesFileStore` saves that separate document into a preexisting plugin-owned directory using a unique temporary file, atomic replace/move and a unique backup of the prior valid configuration. It refuses overwriting corrupt/unsupported existing configurations, follows no known reparse-point paths and provides explicit backup **inspection**, not automatic rollback. The store is now called only from an explicit preview-screen Save action; it is never an automatic settings migration.
- **Source-local runtime snapshots:** `SourceProfileSettingsProjector` preserves legacy remote timeout and configuration and produces source-scoped settings without changing active user settings. Outside-cache uninstall permission is refused for multi-source.
- **All-or-nothing catalog planning:** `MultiSourceRuntimeCoordinator` reads independently validated manifests and checks complete mapping, disabled/offline sources and exact/ancestor-overlapping cache destinations before publishing anything. A partial success must not cause Playnite to discard offline games.
- **Source-bound action routing:** `MultiSourceControllerFactory` checks expected Playnite plugin ownership and the matching plan route, then supplies source-specific item/settings copies to install and uninstall controllers. The new-profile install passes its **namespaced** GameId as the receipt key. Legacy receipt keys remain the exact original item ID. This has isolated Windows fixtures with real disposable file transfers.
- **Guarded Playnite integration:** `PersonalCloudLibrarySource.GetGames`, `GetInstallActions` and `GetUninstallActions` now have an experimental path gated by `EnableMultiSourceProfiles`. This setting defaults **false** and is copied correctly through the settings edit/rollback path. Normal single-source imports/actions retain their existing path. The experimental path requires a valid independent profile file; it fails closed with an exception rather than returning a successful partial catalog. Failed preparations invalidate the old action plan.

- **Preview UI:** A new localized "Source Profiles (1.0 Preview)" settings tab lets users load or preview the original source, add local/folder/rclone profiles, edit source-local connection fields and explicitly save. `SourceProfilesManagementService` validates complete profile configurations, preserves the legacy source ID, blocks removal/re-identification/disable, refuses stale edits and does **not** activate the new runtime. There is currently no automatic rollback, source retirement, live source status or built-in 7z/RAR functionality in this tab. Playnite .NET 4.6.2 XAML and localization contracts are covered by CI.

## Verified boundaries

- The legacy settings schema/version remains unchanged until the final v1 migration design and review.
- This branch does not enable a GUI toggle, auto-create a profile file, silently adopt missing sources, or issue delete permissions for game directories.
- No active cloud credentials, real NAS or private game archives are used in the automated fixtures.
- A matching V2 ownership receipt is informational, not authorization for recursive deletion. Cached-file uninstall continues to rely on the original safe deletion policy.

## Outstanding v1.0 release gates

1. **Native Playnite host acceptance:** experimental `GetGames`/action routing, stable IDs during updates, keyboard/Desktop/Fullscreen behavior and source reconnect; currently automated tests cannot establish host parity.
2. **Complete profile-management UX:** basic localized add/edit/save preview is implemented; source disable/retirement, live status, conflict resolution, coherent settings Cancel semantics and explicit restore/rollback workflow still need design and acceptance. A disabled source must not silently remove catalog entries.
3. **Last-known-good snapshots:** decide whether to preserve offline games while healthy sources refresh, with provenance and schema rules; current strict strategy fails the entire import on any failed source.
4. **Settings migration and lifecycle:** complete source-profile save/restore, supported upgrade from v0.2.0/v0.3.2 and refusal of corrupt profiles. The default-off experimental bit is not a finished migration or supported user setting.
5. **Filesystem threat review:** path/link races and file-replacement atomicity need independent adversarial Windows review. No directory-delete expansion without a separate safety decision.
6. **Other 1.0 pillars:** ZIP staging to live Install UI, safe 7z/RAR implementations, executable confirmation, size-reporting compatibility, accessibility and diagnostics.
7. **Freeze and review:** consolidate all draft PRs, run full Windows CI on exact integrated SHA, obtain Claude/Grok/Nova independent reviews, fix their blockers, then one consolidated disposable Windows acceptance and explicit owner release authorization.

This branch must remain **draft/unmerged**. CI success proves the automated fixture contract, **not** full multi-source production readiness.
