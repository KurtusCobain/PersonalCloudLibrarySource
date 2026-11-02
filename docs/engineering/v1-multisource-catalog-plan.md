# PCLS v1 multi-source catalog safety contract

**Status: draft pure planning code, not yet active multi-source loading or settings.** See [PR #22](https://github.com/KurtusCobain/PersonalCloudLibrarySource/pull/22) and [issue #19](https://github.com/KurtusCobain/PersonalCloudLibrarySource/issues/19). Public v0.3.2 is unchanged.

## Implemented foundations

- `SourceProfileIdentityPolicy`: profile IDs are persistent, nonempty GUIDs; source names and locations do not define a game's identity. Legacy single-source GameIds remain byte-for-byte identical; games from new profiles are namespaced deterministically to prevent collisions. Duplicate IDs and multiple legacy profiles fail validation.
- `MultiSourceCatalogPlanner.Build`: takes already validated independent single-source `ImportOutcome` batches, verifies their source IDs and manifest provenance, copies PCLS-managed Playnite metadata without changing input games, assigns stable namespaced IDs and produces GameId → ProfileId + OriginalItemId routing records.
- `SourceProfileSettingsProjector` converts a disposable copy of a v0.3.2 single-source configuration into one *legacy* profile using a caller-provided persistent GUID. It preserves custom rclone timeout, paths, manifest settings and legacy GameIds. Separate profiles project into separate read-only runtime settings snapshots with a shared cache policy; any pre-existing outside-cache uninstall option causes a **refusal**, not a silent safety-policy rewrite. No settings persistence or Playnite action is performed here.
- If **any** enabled source is inaccessible, invalid, or missing, the plan refuses to publish a partial successful catalog. This is a deliberate guard against Playnite removing games that disappear from the returned `GetGames` result when a NAS/cloud scan fails. Likewise, disabling a source without an explicit keep/retire policy cannot silently remove its titles.
- Tests cover same original game ID in legacy and new sources, name/order/provider changes, source failure, disabled source, duplicate IDs, mismatched source item metadata, and nonmutation of input game objects.

## Not yet implemented / release blockers

1. No multi-profile settings persistence, migration from v0.3.2 single-source settings, or Source Profiles UI.
2. `PersonalCloudLibrarySource.GetGames` still loads **one source**; this planner is not wired to runtime import.
3. Install/Uninstall controllers still load a single manifest from active settings. Routing GameIds to the correct source-specific settings and source item is essential before enabling multi-source import.
4. Cross-profile cache collisions, disconnected source last-known-good snapshots, source removal/retirement decisions and duplicate-content hints need a coherent lifecycle design.
5. This baseline chooses **fail the entire import on any source error** instead of publishing partial data. A future persisted last-known-good per-source snapshot may allow healthy sources to update while retaining offline entries, but only with provenance/recency and user-controlled retirement.
6. Provider credentials, sync status, settings UI, migration and release qualification require separate coding, review and disposable Windows provider tests.

## Safety and rollout policy

Do not combine and publish namespaced IDs until install/uninstall routing is fully compatible. A wrong profile mapping could attach a game action to the wrong source or cache file. Persist source settings in a versioned new schema and write legacy migration tests before enabling new-source profiles. Never delete/rename Playnite games merely because a provider is temporarily offline.

Windows CI stays on every coding PR, then exact integrated SHA → independent Claude/Grok/Nova reviews → one consolidated native Playnite acceptance. This document is not approval to merge, release, or modify public installer metadata.
