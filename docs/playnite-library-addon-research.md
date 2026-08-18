# Playnite Library Add-on Research

## Scope

This is a read-only research and design audit for `PersonalCloudLibrarySource` (`PCLS`) after the published `v0.2.0` release.

Scope boundaries followed:

- No source code was modified.
- No files were staged.
- No commits were created.
- No `v0.3` implementation was started.
- No cache-before-launch automation was added.
- No private ROMcade catalog content, ROMs, BIOS files, keys, cracks, saves, or personal manifests were copied into this report.

This report uses:

- Official Playnite SDK API docs and official Playnite template/add-on database repositories.
- Public repositories for comparable Playnite add-ons.
- Current local PCLS settings and plugin source.
- The existing local-only [docs/romcade-audit-report.md](D:/PersonalCloudLibrarySource/docs/romcade-audit-report.md) as prior-context input only.

## Executive Summary

PCLS is already aligned with several important Playnite library-plugin patterns:

- Stable plugin GUID and stable extension `Id`.
- Manifest-backed import with cloud-only entries allowed to appear as uninstalled.
- Conditional install and uninstall actions scoped to PCLS-owned games.
- Plugin-user-data outputs for generated manifests, reports, and diagnostics.
- A working settings view with first-party tooling for manifest generation and verification.

The main gaps are not architectural. They are mostly product-surface and operator-experience gaps:

- The settings screen is functional, but too linear and too dense for first-run users.
- The current status card is readable now, but still too passive and not action-oriented.
- Checks, reports, diagnostics, and helper actions are present but over-clustered.
- There is no small Playnite-native menu surface for frequently used operational actions.
- PCLS has no queue/progress model yet for future cache/download operations.

The best next step is not a redesign. It is a focused `v0.3` UX and operations pass:

1. tighten setup flow and conditional visibility,
2. surface reports and verification more clearly,
3. add a minimal menu surface,
4. harden future install/download progress patterns before any cache-before-launch work,
5. keep all automation explicitly opt-in.

## Official Playnite SDK Best Practices

### Primary sources

- [Playnite SDK `LibraryPlugin`](https://api.playnite.link/docs/api/Playnite.SDK.Plugins.LibraryPlugin.html)
- [Playnite SDK `Plugin`](https://api.playnite.link/docs/api/Playnite.SDK.Plugins.Plugin.html)
- [Playnite SDK `ISettings`](https://api.playnite.link/docs/api/Playnite.SDK.ISettings.html)
- [Playnite SDK `InstallController`](https://api.playnite.link/docs/api/Playnite.SDK.Plugins.InstallController.html)
- [Playnite SDK `PlayController`](https://api.playnite.link/docs/api/Playnite.SDK.Plugins.PlayController.html)
- [Playnite SDK `MainMenuItem`](https://api.playnite.link/docs/api/Playnite.SDK.Plugins.MainMenuItem.html)
- [Playnite Toolbox custom library template](https://github.com/JosefNemec/Playnite/blob/master/source/Tools/Playnite.Toolbox/Templates/Extensions/CustomLibraryPlugin/_name_.cs)
- [Playnite Toolbox custom library build include list](https://github.com/JosefNemec/Playnite/blob/master/source/Tools/Playnite.Toolbox/Templates/Extensions/CustomLibraryPlugin/BuildInclude.txt)
- [Playnite Toolbox custom library `extension.yaml` template](https://github.com/JosefNemec/Playnite/blob/master/source/Tools/Playnite.Toolbox/Templates/Extensions/CustomLibraryPlugin/extension.yaml)
- [Playnite Add-on Database README](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/master/README.md)

### Best-practice summary and PCLS comparison

| Topic | Best practice from official sources | Current PCLS status | Recommendation |
| --- | --- | --- | --- |
| Plugin identity | Keep a stable plugin `Guid Id` in code and matching stable extension/add-on IDs in manifests. | Good. PCLS uses stable code GUID and stable public add-on ID. | Keep unchanged. |
| Library plugin model | `LibraryPlugin` should return `GameMetadata` from `GetGames` or use `ImportGames` only when customized import is needed. | Good. PCLS uses `GetGames` for manifest-backed imports, which fits the current model. | Keep this model. Do not switch to a separate custom import flow unless product intent changes. |
| Stable game IDs | Imported items need stable `GameId` values so updates reconcile instead of duplicating. | Good in intent. Manifest IDs are the reconciliation key. | Preserve manifest `id` stability as a product rule and document it more clearly. |
| Installed state | `IsInstalled` should reflect real launchable/local availability, not remote catalog presence. | Good. PCLS treats cloud-only or missing-local items as uninstalled. | Keep. This is one of PCLS's strongest product decisions. |
| Play action presence | `PlayController` or play actions should exist only when the game is actually launchable. | Good. PCLS only exposes local launch behavior when expected local files exist. | Keep strict gating. Do not add speculative play actions. |
| Install actions | `GetInstallActions` should return install controllers only when the game is owned by the plugin and installation is valid. | Good. PCLS scopes by `PluginId`, settings, source path availability, and cached state. | Keep. Add clearer user-facing refusal reasons later. |
| Uninstall actions | `GetUninstallActions` should be similarly scoped and safety-checked. | Good. PCLS already checks cache path safety and uninstall target validity. | Keep. This is the right foundation for future UX polish. |
| Settings lifecycle | `ISettings` requires edit lifecycle plus `VerifySettings(out errors)`. | Good. PCLS implements the expected settings lifecycle. | Keep. Expand field-level feedback in UI later. |
| Settings exposure | Official template uses `Properties.HasSettings = true`, `GetSettings`, and `GetSettingsView`. | Good. PCLS follows this pattern. | Keep. |
| User data storage | `GetPluginUserDataPath()` is the correct place for plugin-owned outputs and state. | Good. PCLS stores generated manifests/reports in plugin user data. | Keep. Avoid extension-folder writes. |
| Menu surfaces | `GetMainMenuItems` and `GetGameMenuItems` should stay targeted and clearly sectioned. | Gap. PCLS currently has no menu surface. | Add a small, task-driven menu surface in `v0.3`. |
| Packaging | Toolbox template and Playnite add-on docs expect `extension.yaml`, icon, localization, compiled DLL, and valid metadata. | Good. PCLS package layout already matches expected structure. | Keep validating package contents with every release. |
| Add-on browser metadata | Add-on manifest and installer manifest must use the same add-on ID and point to a valid `.pext` release URL. | Good. `v0.2.0` metadata is aligned. | Keep release checklist strict. |

### Additional official-pattern observations

- The official custom library template uses a stable `Guid`, a stable `extension.yaml` `Id`, `HasSettings = true`, and a small `GetGames` example. PCLS already follows that shape.
- The Add-on Database README explicitly calls out Toolbox verification and strict YAML metadata structure. PCLS should continue treating add-on metadata as release-critical, not as an afterthought.
- The SDK surface makes menu items, sidebar items, play controllers, install controllers, and uninstall controllers first-class primitives. PCLS is using controllers correctly already, but under-uses the menu surface.

## Existing Add-on Comparison

### Sources reviewed

- [Local Library repo](https://github.com/azuravian/playnite-LocalLibrary)
- [RomM plugin repo](https://github.com/rommapp/playnite-plugin)
- [EmuLibrary/GameVault repo](https://github.com/f4mrfaux/Playnite-EmuLibrary)
- [XCloud Library repo](https://github.com/joyrider3774/Playnite_XCloud_Library)
- [Playnite Add-on Database](https://github.com/JosefNemec/PlayniteAddonDatabase)
- [darklinkpower PlayniteExtensionsCollection](https://github.com/darklinkpower/PlayniteExtensionsCollection)

### Comparison table

| Add-on | Model | Setup/UI Pattern | Install/Cache Pattern | Diagnostics Pattern | Good Ideas for PCLS | Avoid |
| --- | --- | --- | --- | --- | --- | --- |
| [Local Library](https://github.com/azuravian/playnite-LocalLibrary) | Library plugin for locally stored installers/media | Multi-tab settings with source selection, installer-path controls, and warnings/tooltips | Installs from local storage; can use actions or ROMs; optional path scanning and update detection | Heavy inline tooltip guidance, but limited operator reporting in README | Split complex configuration into sections or tabs; explain destructive options inline | Dense UI, too many knobs visible at once, ambiguous install failures like issue `#50` |
| [RomM](https://github.com/rommapp/playnite-plugin) | Remote catalog/API-backed library plugin with downloads | Setup includes auth, mappings, emulator paths, and stateful notifications | Download queue, concurrent downloads, cancellation, install version selection, sidebar for download monitoring | Strong operator-facing runtime surface: queue, notifications, sidebar, version prompts | Queue/progress model, cancellation, explicit install summaries, sidebar or equivalent surface | Plaintext password storage, large scope, high complexity, emulator-specific coupling |
| [EmuLibrary / GameVault](https://github.com/f4mrfaux/Playnite-EmuLibrary) | Remote/local repository treated as installable uninstalled library | Mapping-driven configuration by emulator, profile, platform, ROM type, source, destination | Distinguishes `SingleFile`, `MultiFile`, `ISOInstaller`, `PCInstaller`; treats uninstalled items as catalog entries until installed | README has strong troubleshooting guidance and path semantics | Better explicit `sourceType`-driven UX language, clear multi-file/package semantics | Hard-to-generalize emulator/platform-specific complexity and overly broad scope |
| [XCloud Library](https://github.com/joyrider3774/Playnite_XCloud_Library) | Cloud catalog importer with browser launch | Compact grouped settings, mostly simple controls | No local cache; install/uninstall used as logical state; play uses configured browser path | Uses notifications and setup prompts when browser path is missing | Compact grouped settings, first-run prompt to open settings, minimal UI surface | Browser-specific assumptions and weak launch-path guardrails |
| [Game Pass Catalog Browser](https://github.com/darklinkpower/PlayniteExtensionsCollection) | Generic catalog browser, not a library plugin | Minimal settings with help link and a few toggles | Adds catalog entries to Playnite, not cache/install oriented | Good menu and sidebar surface, clear global progress dialogs, explicit cache reset action | Small `Extensions` menu section, contextual help link, progress dialogs, no clutter | Too many bulk actions for PCLS if copied directly |
| [Game Media Tools](https://github.com/darklinkpower/PlayniteExtensionsCollection) | Generic maintenance tool | Menu-first rather than settings-first | No install/cache logic | Strong maintenance-tool pattern: focused menu items and result dialogs | Good model for lightweight maintenance/report commands | PowerShell/XAML heavy maintenance UI is broader than PCLS needs |

### Add-on-specific notes

#### Local Library

Public sources reviewed:

- [README](https://github.com/azuravian/playnite-LocalLibrary/blob/main/README.md)
- [Settings XAML](https://github.com/azuravian/playnite-LocalLibrary/blob/main/LocalLibrarySettingsView.xaml)
- [Add-on database entry](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/master/addons/library/Azuravian_LocalLibrary.yaml)
- [Issue #50](https://github.com/azuravian/playnite-LocalLibrary/issues/50)

Patterns worth copying:

- Separate configuration areas for source ownership, installer paths, and optional automation.
- Strong inline warnings around settings that can affect user data or play actions.

Patterns to avoid:

- Too much visible complexity at first run.
- Options that feel implementation-centric instead of task-centric.
- Ambiguous install failures such as "Installation Implementation is not available."

#### RomM

Public sources reviewed:

- [README](https://github.com/rommapp/playnite-plugin/blob/main/README.md)
- [Main plugin class](https://github.com/rommapp/playnite-plugin/blob/main/RomM.cs)
- [Settings model](https://github.com/rommapp/playnite-plugin/blob/main/Settings/Settings.cs)
- [Install controller](https://github.com/rommapp/playnite-plugin/blob/main/Games/RomMInstallController.cs)
- [Download queue controller](https://github.com/rommapp/playnite-plugin/blob/main/Downloads/DownloadQueueController.cs)
- [Download sidebar item](https://github.com/rommapp/playnite-plugin/blob/main/Downloads/RomMDownloadsSidebarItem%20.cs)
- [Add-on database entry](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/master/addons/library/rommapp_RomM.yaml)
- [Issue #31](https://github.com/rommapp/playnite-plugin/issues/31)
- [Issue #23](https://github.com/rommapp/playnite-plugin/issues/23)
- [Issue #91](https://github.com/rommapp/playnite-plugin/issues/91)

Patterns worth copying:

- Explicit install queue controller with cancellation support.
- Sidebar surface for active downloads instead of burying progress in message boxes.
- Prompting for revisions/versions at install time instead of polluting the library with duplicate visible entries.
- Strong scoping of install/uninstall actions to plugin-owned games.

Patterns to avoid:

- High setup complexity before first successful import.
- Auth and emulator mapping friction.
- Storage of sensitive credentials in plaintext.
- Product drift toward emulator-specific management instead of generic source semantics.

#### EmuLibrary / GameVault

Public sources reviewed:

- [README](https://github.com/f4mrfaux/Playnite-EmuLibrary/blob/master/README.md)
- [Add-on database entry](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/master/addons/library/psychonic_EmuLibrary.yaml)

Patterns worth copying:

- Clear conceptual distinction between `SingleFile`, `MultiFile`, disc-image, and installer flows.
- Strong documentation around folder/package semantics and installed/uninstalled catalog behavior.
- Good articulation of "library entry first, local copy later" value.

Patterns to avoid:

- Large mapping-heavy setup for generic users.
- Over-expansion into platform-specific heuristics that make the product harder to reason about.

#### XCloud Library

Public sources reviewed:

- [README](https://github.com/joyrider3774/Playnite_XCloud_Library/blob/main/README.md)
- [Settings XAML](https://github.com/joyrider3774/Playnite_XCloud_Library/blob/main/XCloudLibrarySettingsView.xaml)
- [Main plugin class](https://github.com/joyrider3774/Playnite_XCloud_Library/blob/main/XCloudLibrary.cs)
- [Add-on database entry](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/master/addons/library/joyrider3774_XCloudLibrary.yaml)

Patterns worth copying:

- Compact grouped settings.
- First-run behavior that nudges the user back to settings when required launch configuration is missing.
- Clean scoping of install, uninstall, and play actions to the plugin's own items.

Patterns to avoid:

- Browser-path-only assumptions.
- Minimal diagnostics when launch configuration is wrong.

#### Game Pass Catalog Browser and Game Media Tools

Public sources reviewed:

- [Game Pass Catalog Browser add-on entry](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/master/addons/generic/darklinkpower_GamePassCatalogBrowser.yaml)
- [Game Pass Catalog Browser plugin class](https://github.com/darklinkpower/PlayniteExtensionsCollection/blob/master/source/Generic/GamePassCatalogBrowser/GamePassCatalogBrowser.cs)
- [Game Pass Catalog Browser settings view](https://github.com/darklinkpower/PlayniteExtensionsCollection/blob/master/source/Generic/GamePassCatalogBrowser/GamePassCatalogBrowserSettingsView.xaml)
- [Game Media Tools add-on entry](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/master/addons/generic/darklinkpower_GameMediaTools.yaml)
- [Game Media Tools PowerShell menu script](https://github.com/darklinkpower/PlayniteExtensionsCollection/blob/master/source/Generic/GameMediaTools/GameMediaTools.psm1)

Patterns worth copying:

- Small top-level `@Section` menu grouping.
- Focused menu items that map directly to operator tasks.
- Global progress dialogs for long-running jobs.
- Help link in settings.
- Maintenance commands that produce immediate visible outcomes.

Patterns to avoid:

- Large tool suites for PCLS.
- Menu sprawl.
- A maintenance-tool identity that overwhelms the core library-source purpose.

## PCLS Current Strengths

- Stable plugin identity and released add-on metadata discipline.
- Correct library-plugin model for manifest-backed import.
- Correct installed/uninstalled semantics for cloud-only or missing-local entries.
- Good early support for `sourceType = file` and `sourceType = directory`.
- Good local-folder manifest generation and verification positioning.
- Good plugin-user-data output discipline.
- Good dark-theme readability fix for the current status card.
- Good safety stance on uninstall path restrictions and plugin-owned actions only.

## PCLS Current Gaps

- The settings screen still reads like a long form rather than a guided flow.
- LocalFile, LocalFolder, and RcloneRemote settings are all visible together, increasing cognitive load.
- The setup status card reports facts, but does not turn them into next steps strongly enough.
- Tools are over-concentrated in one "Checks and Tools" block.
- Diagnostics and reporting exist, but they are not surfaced as a clear operator workflow.
- There is no small menu surface for frequent maintenance tasks.
- There is no queue/progress model for future multi-item caching work.
- There is no explicit "why no install action" or "why no play action" user-facing explanation surface outside logs and reports.
- First-run guidance is present, but it is still modal/message-box driven rather than embedded into the page.

## UI and Settings Recommendations

### 1. Turn the page into a guided flow, not a single long form

Recommended order:

1. Setup Status
2. Source Mode
3. Provider-specific setup block
4. Manifest Generation or Manifest Selection
5. Cache / Download behavior
6. Reports / Diagnostics
7. Advanced Options
8. Legal-use note

Rationale:

- This matches the user's mental sequence.
- It hides irrelevant provider fields until needed.
- It reduces the "wall of controls" effect.

### 2. Make provider sections conditionally visible

Recommended behavior:

- `LocalFile`: show only manifest path and cache behavior.
- `LocalFolder`: show library root, manifest path/relative path guidance, generation tools, cache behavior.
- `RcloneRemote`: show rclone-specific fields, manifest path, content root, timeout, and connectivity tools.

Rationale:

- Both XCloud and Game Pass Catalog Browser show the value of smaller focused settings.
- Local Library shows the downside of exposing too much at once.

### 3. Upgrade the Setup Status card from passive summary to action summary

Recommended card structure:

- headline: `Ready`, `Needs attention`, `Disabled`, `Verification found issues`
- small checklist rows:
  - provider configured
  - manifest reachable
  - cache folder configured
  - verification passed or pending
- explicit next action line

Example next actions:

- `Next: generate a manifest, then run Verify setup.`
- `Next: save settings and run Update Game Library.`
- `Next: fix the manifest path shown in the verification report.`

### 4. Split Reports and Diagnostics into its own section

Current problem:

- Reports are mixed with setup checks, sample creation, cache folder, plugin data folder, and update instructions.

Recommended section:

- `Verify setup / generate report`
- `Open latest verification report`
- `Open reports folder`
- `Open diagnostics folder`
- `Open plugin data folder`

Keep `Create sample manifest` under advanced/testing, not beside primary verification.

### 5. Re-group buttons by task

Recommended button groups:

- Setup:
  - `Generate manifest from folder`
  - `Verify setup`
- Review outputs:
  - `Open generated manifest`
  - `Open generated report`
  - `Open latest verification report`
- Environment:
  - `Test rclone connection`
  - `Test manifest load`
- Folders:
  - `Open cache folder`
  - `Open reports folder`
  - `Open diagnostics folder`
  - `Open plugin data folder`

### 6. Improve wording for first-run users

Replace implementation-heavy wording with task wording.

Examples:

- `Source provider type` -> `Where is your library stored?`
- `Local manifest JSON path` -> `Manifest file`
- `Manifest relative path` -> `Manifest path inside the library root`
- `Allow downloads to local cache` -> `Allow copying or downloading selected games to the local cache`
- `How to update library` -> `What to do after setup`

### 7. Keep dark-theme safety as a release gate

The status card issue is fixed, but future settings changes should explicitly verify:

- readable text/background contrast,
- visual hierarchy for headers versus helper text,
- disabled-state readability,
- button grouping clarity in dark theme.

## Menu Recommendations

PCLS should add a small menu surface in `v0.3`, but it should stay much smaller than ROMcade and lighter than darklinkpower's maintenance tools.

### Recommended `Extensions > Personal Cloud Library Source` items

- `Open settings`
- `Verify setup / generate report`
- `Open reports folder`
- `Open diagnostics folder`
- `Open plugin data folder`

Optional:

- `Generate manifest from folder`

Not recommended right now:

- large nested tool trees,
- multiple report variants,
- broad bulk-maintenance commands,
- ROMcade-style import/remove/report menus.

### Recommended per-game context items

Only show these for PCLS-owned games, and only when valid:

- `Cache this game locally`
- `Remove cached copy`
- `Verify cached item`
- `Open cache folder`
- `Open source location`

`Open source location` should be:

- LocalFolder only,
- path-safe,
- disabled or omitted for rclone and cloud-only sources,
- never used to expose sensitive or unrelated system paths.

## Install/Cache/Uninstall Recommendations

### Short-term

- Keep current strict action gating.
- Add clearer user-facing refusal reasons when an install or uninstall action is unavailable.
- Add more explicit completion summaries after install/uninstall operations.

### Medium-term

- Add a queue/progress model before cache-before-launch automation.
- Support cancellation for long copy/download operations.
- Show per-item progress and outcome in a dedicated surface.

### Longer-term

- Add optional cache integrity checks for downloaded/copied items.
- Add size estimation where practical before starting long copy/download jobs.
- Improve directory-package install feedback for disc folders and Wii U layouts.

## Diagnostics and Reporting Recommendations

### P0 improvements

- Promote verification report access in the settings UI.
- Add a compact summary panel for the most recent report.
- Add clearer report categories:
  - configuration
  - manifest load
  - source path validity
  - cache path validity
  - install eligibility
  - warnings

### P1 improvements

- Add a source/library integration audit report:
  - imported item count
  - duplicate manifest IDs
  - plugin ownership mismatches
  - play-action eligibility counts
  - install-action eligibility counts

- Add optional metadata-gap reporting:
  - missing images
  - missing release year
  - missing platforms

This should be report-based first, not auto-tag-based first.

### P2 improvements

- Better formatting for operator reports:
  - top summary block
  - grouped warnings
  - specific recommended next steps
  - timestamps and provider context

## v0.3 Roadmap

### P0 - Must do before cache-before-launch

1. Restructure the settings page into a clearer guided flow.
2. Make provider-specific sections conditionally visible.
3. Add a small `Extensions > Personal Cloud Library Source` menu surface.
4. Add a better operator-facing verification summary and report entry points.
5. Design and implement a queue/progress model for copy/download operations.
6. Add cancellation support for long-running copy/download operations.
7. Improve refusal and error messaging for unavailable install/play/uninstall actions.

### P1 - Good `v0.3` candidates

1. Opt-in cache-before-launch design with explicit `Ask / Always / Never` behavior.
2. Per-game progress state and final operation summaries.
3. Copy/download size estimation when feasible.
4. Cache integrity verification tooling.
5. Better `sourceType` and package-role detection diagnostics.
6. Safe migration guidance or auto-detection for older manifests that omit `sourceType`.
7. Better rclone timeout and error messages, especially for path syntax and missing executable cases.

### P2 - Later polish

1. Optional metadata-gap reporting.
2. Improved first-run inline help instead of modal-only guidance.
3. More polished empty states and field validation styling.
4. Optional source-location helpers for LocalFolder items.
5. Light summary dashboard for recent generation/report activity.

## Do Not Merge / Do Not Build

- Do not merge ROMcade private-root assumptions.
- Do not merge ROMcade-specific family or household state concepts.
- Do not merge credential-heavy or provider-specific logic beyond current generic rclone/local scope without a product decision.
- Do not add startup-time bulk auto-download behavior.
- Do not add cache-before-launch automation before queue/progress/cancellation UX exists.
- Do not auto-tag libraries aggressively for diagnostics until a lighter report-first model is validated.
- Do not copy third-party plugin code into PCLS.

## Open Questions

1. Should PCLS keep all operator tools in settings plus a very small `Extensions` menu, or should it also gain a sidebar surface later for download queue visibility?
2. Should the future cache-before-launch prompt be per-game transient state only, or should it support a persistent per-plugin default policy?
3. Should metadata-gap visibility stay report-only, or should there be an optional user-controlled tagging mode?
4. Should LocalFolder generation and verification become a true two-step wizard later, or remain a single page with stronger conditional sections?

## Recommended Next Implementation Prompt

Use this as the next implementation prompt:

> You are working in `D:\PersonalCloudLibrarySource`. Implement a `v0.3` UI-and-operations polish pass only for `PersonalCloudLibrarySource`. Do not add cache-before-launch automation yet. Do not change the extension ID. Focus on: 1) restructure the settings UI into guided sections with provider-conditional visibility, 2) split reports/diagnostics into a clearer dedicated section, 3) add a small `Extensions > Personal Cloud Library Source` menu with settings/report actions only, 4) improve status-card next-step messaging, and 5) prepare a non-invasive download/copy queue design seam without changing current manifest schema. Keep all changes generic and public-safe.
