# PCLS v1 multi-source catalog safety contract

**Status: draft pure planning code, not yet active multi-source loading or settings.** See [PR #22](https://github.com/KurtusCobain/PersonalCloudLibrarySource/pull/22) and [issue #19](https://github.com/KurtusCobain/PersonalCloudLibrarySource/issues/19). Public v0.3.2 is unchanged.

## Implemented foundations

- `SourceProfileIdentityPolicy`: profile IDs are persistent, nonempty GUIDs; source names and locations do not define a game's identity. Legacy single-source GameIds remain byte-for-byte identical; games from new profiles are namespaced deterministically to prevent collisions. Duplicate IDs and multiple legacy profiles fail validation.
- `MultiSourceCatalogPlanner.Build`: takes already validated independent single-source `ImportOutcome` batches, verifies their source IDs and manifest provenance, copies PCLS-managed Playnite metadata without changing input games, assigns stable namespaced IDs and produces GameId → ProfileId + OriginalItemId routing records.
- `SourceProfileSettingsProjector` converts a disposable copy of a v0.3.2 single-source configuration into one *legacy* profile using a caller-provided persistent GUID. It preserves custom rclone timeout, paths, manifest settings and legacy GameIds. Separate profiles project into separate read-only runtime settings snapshots with a shared cache policy; any pre-existing outside-cache uninstall option causes a **refusal**, not a silent safety-policy rewrite. No settings persistence or Playnite action is performed here.
- If **any** enabled source is inaccessible, invalid, or missing, the plan refuses to publish a partial successful catalog. This is a deliberate guard against Playnite removing games that disappear from the returned `GetGames` result when a NAS/cloud scan fails. Likewise, disabling a source without an explicit keep/retire policy cannot silently remove its titles.
- Tests cover same original game ID in legacy and new sources, name/order/provider changes, source failure, disabled source, duplicate IDs, mismatched source item metadata, and nonmutation of input game objects.

## Multi-source coordinator and source routing (next coding increment)

`MultiSourceRuntimeCoordinator.Prepare(profiles, sharedSettings, rcloneReader)` now loads **real independently validated manifests**, projects each source's settings, refuses unavailable/disabled sources, checks duplicate and ancestor-overlapping cache destinations across **all** sources, and only then assembles a combined atomic catalog. It never publishes partial results to Playnite.

For each combined game ID, `MultiSourceRuntimePlan.TryResolveAction` returns a `SourceScopedGameAction` whose original manifest item and source-specific runtime settings were captured at import time. Returned item/settings snapshots are copies, so an accidental settings-panel edit or caller mutation cannot silently switch the source for a previously staged action. This prepares *safe* install/uninstall routing; the live controller dispatch must still be connected in a separately reviewed change.

`SourceProfilesConfigurationCodec` now provides a bounded **versioned (schema v1) JSON preview** for source profiles, including one legacy-source migration using an explicitly stable GUID. It refuses missing/unknown schema versions, invalid duplicate IDs and oversized documents. The codec is pure: it **does not save a file** or rewrite v0.3.2 settings. Secure and atomic persistence, source retirement UX and rollback must be designed before enabling the new settings.

New deterministic tests cover two local manifests with identical original game IDs, source-specific action routing, changes to input profile settings after publication, offline source reconnect, identical/overlapping cache destinations across profiles, legacy settings preservation and JSON schema failures. This is **not native Playnite acceptance** or a test of actual connected NAS/cloud environments.

## Not yet implemented / release blockers

1. No multi-profile settings persistence, migration from v0.3.2 single-source settings, or Source Profiles UI.
2. `PersonalCloudLibrarySource.GetGames` still loads **one source**; this planner is not wired to runtime import.
3. Install/Uninstall controllers still load a single manifest from active settings. Routing GameIds to the correct source-specific settings and source item is essential before enabling multi-source import.
4. Cross-profile cache collisions are now refused in the read-only coordinator, including exact and ancestor-overlapping destinations. Disconnected source last-known-good snapshots, source removal/retirement decisions and duplicate-content hints still need a coherent lifecycle design.
5. **Critical receipt-key migration:** `RcloneInstallController` currently writes V1/V2 ownership receipts under `item.Id`, not `Game.GameId`. Two new profiles may have the same original item ID while their Playnite GameIds differ. Before activation, install and uninstall ownership checks must use a stable, source-namespaced receipt identity for *new* profiles, while retaining the exact existing receipt key for the legacy source. Never simply rewrite `item.Id` to the namespaced ID, because manifest paths and transfer fallback semantics depend on the original item ID. Add explicit regression tests for two profiles with identical original IDs and separate cache roots/receipts.
6. Source-specific install/uninstall controller dispatch must route from the original combined GameId to the correct source-specific item and settings snapshot. The currently active single-source manifest must not be consulted for a new-profile game.
5. This baseline chooses **fail the entire import on any source error** instead of publishing partial data. A future persisted last-known-good per-source snapshot may allow healthy sources to update while retaining offline entries, but only with provenance/recency and user-controlled retirement.
6. Provider credentials, sync status, settings UI, migration and release qualification require separate coding, review and disposable Windows provider tests.

## Safety and rollout policy

Do not combine and publish namespaced IDs until install/uninstall routing is fully compatible. A wrong profile mapping could attach a game action to the wrong source or cache file. Persist source settings in a versioned new schema and write legacy migration tests before enabling new-source profiles. Never delete/rename Playnite games merely because a provider is temporarily offline.

Windows CI stays on every coding PR, then exact integrated SHA → independent Claude/Grok/Nova reviews → one consolidated native Playnite acceptance. This document is not approval to merge, release, or modify public installer metadata.
