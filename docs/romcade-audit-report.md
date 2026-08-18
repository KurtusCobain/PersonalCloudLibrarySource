# ROMcade Setup and Plugin Audit

## Scope

This was a read-only audit of the local ROMcade / Playnite setup under `D:\ROMcade_Master` and a comparison against the current `PersonalCloudLibrarySource` repo baseline at commit `8d9274a6c13d2f5c1b90a457ef30f070095b97c7` (`Add guided setup and local manifest generation`).

The audit focused on public-safe structure, Playnite extension manifests, ROMcade architecture notes, targeted plugin source slices, and recent Playnite extension log behavior. No ROMcade files were modified. No `PersonalCloudLibrarySource` code was changed in this pass.

## Safety Notes

- No ROMs, BIOS files, game files, keys, cracks, saves, or personal library content were copied into this repo.
- No private manifest payloads or wholesale private file listings were pasted into this report.
- Private paths and user-specific content were summarized or redacted where possible.
- No `ExtensionsData` content was altered.
- No generated manifests, reports, caches, build outputs, or package artifacts were staged or committed.

## Environment Summary

| Item | Result |
| --- | --- |
| Playnite executable | Found |
| Toolbox | Found |
| Portable `Extensions` folder | Found |
| Portable `ExtensionsData` folder | Found |
| `playnite.log` | Found |
| `extensions.log` | Found |
| PCLS baseline commit | `8d9274a6c13d2f5c1b90a457ef30f070095b97c7` present locally |
| Current PCLS branch | `main` |
| Current repo status before report write | clean working tree on `main`, ahead of `origin/main` by 1 commit |

Safe top-level ROMcade orientation:

- Portable Playnite lives under `D:\ROMcade_Master\Playnite\Playnite`.
- Obvious tool/dev folders exist for `Tools`, `ROMcade_Data`, and `ROMcade_Dev`.
- Personal content directories also exist and should remain out of the repo and out of future public docs.

## Installed Extension Inventory

| Name | ID | Version | Type/Role | Relevance to PCLS |
| --- | --- | --- | --- | --- |
| AmazonGames library integration | `AmazonLibrary_Builtin` | `2.11` | Built-in game library | Low |
| Battle.net library integration | `BattlenetLibrary_Builtin` | `2.24` | Built-in game library | Low |
| Epic Store library integration | `EpicGamesLibrary_Builtin` | `2.27` | Built-in game library | Low |
| ThemeExtras | `felixkmh_Extras_Plugin` | `1.4.4` | Generic UI/theme utility | Low |
| GOG library integration | `GogLibrary_Builtin` | `2.21` | Built-in game library | Low |
| IGDB metadata provider | `IGDBMetadata_Builtin` | `2.13` | Metadata provider | Medium, supports metadata-before-download workflows |
| itch.io library integration | `ItchioLibrary_Builtin` | `2.7` | Built-in game library | Low |
| Nexus Mods checker | `Nexus_Mods_Checker_ece2874c-be52-4a64-b178-ed379a042f85` | `2.13` | Script/utility | Low |
| Meta Quest Library Importer | `OculusLibraryPlugin_Playnite_Plugin` | `2.6.5` | Game library | Low |
| EA app library | `OriginLibrary_Builtin` | `3.2.1` | Built-in game library | Low |
| Personal Cloud Library Source | `PersonalCloudLibrarySource_61993828-67a8-4468-93a2-293442e36328` | `0.1.1` | Game library | Direct baseline |
| Playnite Achievements | `PlayniteAchievements` | `2.1.3` | Generic plugin | Low |
| HowLongToBeat | `playnite-howlongtobeat-plugin` | `3.10.6` | Generic metadata/enrichment plugin | Medium, example of pre-install enrichment value |
| SuccessStory | `playnite-successstory-plugin` | `3.7.1` | Generic plugin | Low |
| PS2 Memory Lane | `PS2MemoryLane` | `1.0.0` | Generic plugin | Low |
| Rockstar Games library integration | `Rockstar_Games_Library` | `2.10` | Built-in game library | Low |
| ROMCade Cloud Library | `ROMCade.CloudLibrary_8421974b-93db-47d7-becd-3d592da236e6` | `0.1` | Custom game library plugin | High |
| Steam Family Group | `SteamFamilyGroup` | `0.1.0` | Game library | Low |
| Steam library integration | `SteamLibrary_Builtin` | `2.40` | Built-in game library | Low |
| Universal Steam Metadata | `Universal_Steam_Metadata` | `2.21` | Metadata provider | Medium, metadata-before-download precedent |
| Ubisoft Connect library integration | `UplayLibrary_Builtin` | `2.9` | Built-in game library | Low |
| Xbox library integration | `XboxLibrary_Builtin` | `2.15` | Built-in game library | Low |

## ROMcade-Specific Findings

### 1. Separate cloud catalog UI plus manual import workflow

ROMcade is not just a manifest-backed importer. Its architecture notes and plugin source show a separate cloud-catalog window that reads a private `CloudLibrary.json`, keeps a sidecar user-state file, and lets the user manually import selected cloud items into the normal Playnite library.

Practical implication for PCLS:

- ROMcade treats "browse cloud catalog" and "import into Playnite library" as separate concerns.
- PCLS currently acts more like a direct Playnite library source that imports from a manifest immediately.

### 2. Safe, explicit import menu surface

`ROMCade.CloudLibrary` exposes a large `GetMainMenuItems()` surface with explicit dry-run, execute, remove-imported-entries, and reports actions. It also keeps risky bulk behavior disabled (`Import All Safe Platforms` is intentionally not implemented in the safety pass).

Practical implication for PCLS:

- ROMcade gives the user more visible operational tools and report entry points.
- PCLS already has guided settings and generated manifest/report buttons, but less operational menu surface once the plugin is installed.

### 3. Reporting and verification are treated as first-class features

ROMcade writes text reports, CSV dry-run reports, source integration reports, and backups around import operations. It also tracks warnings such as duplicate titles, title collisions, missing artwork, missing metadata, possible wrong platform, archive candidates, and source-ID fixes.

Practical implication for PCLS:

- PCLS already writes a manifest generation report and import diagnostics, but ROMcade has a more operator-friendly reporting model for ongoing maintenance.

### 4. Metadata-before-download is intentionally supported

ROMcade architecture notes explicitly allow placeholder imports with `IsInstalled = false` and tags such as `Needs Artwork`, `Needs Metadata`, and `Needs Download`. PCLS docs already state that cloud-only entries can exist before download and can be enriched by Playnite metadata tools first.

Practical implication for PCLS:

- The product direction is aligned.
- ROMcade is stronger in how it surfaces missing-artwork / missing-metadata states.

### 5. Safe backup and state-write patterns

ROMcade's cloud user-state save flow backs up the prior JSON, writes to a temp file, then replaces/moves atomically. Import flows also write backups under `ROMcade_Data/Backups` and clearly state that live Playnite DB files are not copied while Playnite is running.

Practical implication for PCLS:

- The same pattern is valuable anywhere PCLS writes mutable user-owned files such as generated manifests, reports, future cache queues, or future sidecar state.

### 6. Strong boundaries that should not be merged as-is

ROMcade includes private-root assumptions, private catalog/tooling, and family-specific state flags. Those are useful local patterns, but they are not suitable for direct public-plugin merge.

## PersonalCloudLibrarySource Comparison

| Feature / behavior | ROMcade approach | Current PCLS approach | Gap | Merge value | Risk |
| --- | --- | --- | --- | --- | --- |
| Primary model | Separate cloud browser plus optional import into Playnite | Direct manifest-backed Playnite library source | Product model differs | Medium | Avoid feature drift |
| Local/NAS manifest generation | External private tooling and private catalog pipeline | Built-in guided local/NAS manifest generation from Playnite settings | PCLS already stronger for public local workflows | Low | None |
| Rclone manifest use | ROMcade relies on private rooted remote and external tools | PCLS supports `RcloneRemote` plus local-folder mode | Similar capability, different packaging | Low | Keep PCLS rclone-optional |
| Main menu tooling | Rich menu with dry-run, execute, reports, remove-imported entries | Settings-screen-driven workflow, install/uninstall actions, generated manifest/report buttons | PCLS has less operational affordance in menus | High | Keep scope tight and opt-in |
| Import reporting | Text reports, CSV dry-run reports, source-integration report, backup notes | Manifest generation report plus diagnostics file | PCLS reporting is narrower | High | Low |
| Source integration verification | Explicit `Game.SourceId` verification and duplicate `GameId` reporting | PCLS imports through standard library flow and logs diagnostics | PCLS lacks explicit source-status audit output | High | Low |
| Placeholder metadata workflow | Explicit tags like `Needs Artwork` and `Needs Metadata` | Docs say metadata can be added before download, but no explicit status tagging | PCLS UX is less visible | Medium | Tag design could clutter libraries |
| Safety confirmations | Confirm dialogs and friendly error messages for execute/remove flows | Safer than before, but fewer visible operation summaries | PCLS can be more explicit | Medium | Low |
| Mutable JSON writes | Temp + replace with timestamped backup | Generated manifest/report writes are straightforward file writes | PCLS could harden user-data writes | Medium | Low |
| Family-specific user state | Favorites, kids-approved, museum-important, etc. | None | Not aligned with plugin intent | None | High, should not merge |
| Hardcoded private root handling | Private fallback root assumptions in ROMcade paths | PCLS uses user-selected paths and generic docs | No gap | None | High, should not merge |
| Bulk safe-platform import surface | Per-platform import commands | PCLS imports directly from manifest results | Could inspire opt-in tools later | Low | High scope expansion |

## Best Merge Candidates

### P0 - Safe / high-value

1. Better operator-facing reports and report entry points
   PCLS already writes generation reports and diagnostics. ROMcade shows that making these easier to discover and easier to interpret is high value with low risk.

2. Source/library integration verification report
   A small PCLS report that verifies imported item count, duplicate IDs, plugin ownership, and install/download eligibility would materially improve supportability.

3. More explicit operation summaries and confirmations
   ROMcade does a good job of turning potentially destructive or confusing operations into explicit confirmations and friendly result dialogs. PCLS could reuse that approach for cache/download/uninstall operations and validation results.

### P1 - Useful but needs design

4. Metadata-gap surfacing for cloud-only items
   ROMcade's `Needs Artwork` / `Needs Metadata` model is useful, but PCLS should avoid polluting normal libraries with too many automatic tags. A lighter diagnostic-only or optional-tag approach would fit better.

5. Hardened file-write and backup patterns for mutable plugin outputs
   PCLS should consider temp-and-replace writes plus lightweight timestamped backups for generated manifests, reports, or future sidecar state files.

### P2 - Later / optional

6. Optional main-menu tools for validation, report opening, and diagnostics
   Useful, but only if kept smaller than ROMcade's custom menu tree.

7. Read-only detector for existing ROMcade-style config or import state
   Potentially useful for migrations, but it must stay read-only and path-agnostic.

8. Better platform-detection hints and package diagnostics
   ROMcade's per-platform operational thinking could inform future PCLS diagnostics, especially for directory packages and emulator-style content.

## Features Not to Merge

- Hardcoded private root assumptions such as a fixed `D:\ROMcade_Master` fallback.
- Family-specific user-state flags and private household workflow concepts.
- Private `CloudLibrary.json` architecture or any code that depends on ROMcade-only data shape.
- Any tooling that touches BIOS, saves, system folders, provider tokens, or `rclone.conf`.
- Any behavior that copies or exposes copyrighted game content, private manifests, or personal library inventory.
- Any startup-time bulk auto-download behavior.
- Any direct live Playnite DB editing model outside Playnite's in-app API.

## Recommended Next Pass

1. Add a small PCLS source/integration verification report that summarizes imported items, duplicate IDs, download-eligible items, and plugin-ownership edge cases.
2. Add more visible report/diagnostic entry points in the settings UI or a minimal main-menu surface.
3. Design an optional metadata-gap indicator strategy that stays lighter than ROMcade's full tagging model.
4. Harden generated manifest/report writes with temp-file replacement and optional backup handling in plugin user data.
5. Evaluate a narrow v0.3 operation-summary pass for download/uninstall/validation results, while keeping cache-before-launch opt-in and out of startup.

## Open Questions

1. Should PCLS stay strictly "manifest-backed library source only," or should it eventually gain a separate catalog/browser surface?
2. If PCLS adds metadata-gap surfacing, should that be tags, diagnostics only, or a settings-screen summary?
3. Should future PCLS operational tools live in the settings view only, or also in a small `Extensions` menu entry?
4. Is read-only migration detection from an existing ROMcade-style setup worth adding, or should PCLS remain fully generic?

## Log Summary

- Recent portable Playnite logs show `Personal Cloud Library Source` loading manifests through rclone and repeatedly importing 3 manifest entries successfully.
- The same logs show PCLS correctly refusing install actions for `romcade-cloud:*` entries that belong to the ROMcade plugin rather than PCLS.
- No PCLS-specific XAML, initialization, settings, or assembly-load exceptions were observed in the inspected log slices.
- Other installed plugins produced unrelated environment noise, including missing Battle.net local data and missing Ubisoft Connect cache data. These are useful reminders that clear, plugin-specific diagnostics matter.

## Conclusion

ROMcade does not reveal a reason to redesign PCLS around a private cloud-catalog model. The best merge candidates are narrower: better diagnostics, better operator-facing reports, explicit source integration verification, and safer user-facing operation summaries. The private-root assumptions, family-specific state, and ROMcade-only catalog architecture should stay out of PCLS.
