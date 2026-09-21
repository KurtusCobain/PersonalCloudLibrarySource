# PCLS 1.0 Feature Completeness Matrix

**Branch:** `feature/user-friendly-dashboard-cleanup`

**Evidence commit:** `17141439c107cb96d2d0d34f88b9c413b22ebecd`

**Target:** PCLS 1.0.0

## Status definitions

- **Complete:** The inspected implementation covers the stated repository-level behavior and has relevant tests. Runtime qualification may still be a release gate.
- **Partial:** A meaningful implementation exists, but required behavior, integration, or tests are incomplete.
- **Disconnected:** The UI/model exposes the feature, but runtime code does not consume it or supporting code is not wired.
- **Untested:** Code exists, but no focused automated or environment test supports the claim.
- **Broken:** Confirmed failure prevents the feature or release pipeline from working as intended.
- **Documentation-only:** Described or planned without a runtime implementation.

Desktop and Fullscreen entries distinguish management UI from GameLibrary behavior. A dedicated Fullscreen dashboard and setup wizard are explicitly out of scope for 1.0.

## Providers, manifests, and import

| Feature | Files involved | Status | Desktop behavior | Fullscreen behavior | Tests present | Tests missing | Release action |
|---|---|---|---|---|---|---|---|
| LocalFile provider | `PersonalCloudLibrarySource.cs`; settings/view; `LocalTransferAdapter.cs` | Partial | Selects a manifest file and imports/copies referenced content | Imported games/controllers should work; not runtime-qualified | Settings migration; transfer adapter | Real file provider import/install/play/uninstall | Qualify end to end and surface missing/invalid file errors |
| LocalFolder provider | Same plus manifest-relative path resolution | Partial | Accepts library root and relative manifest path | Core game flow expected; unverified | Setup/status recognition; local directory transfer | Root containment, relative traversal, disconnect/reconnect | Centralize resolver and qualify |
| External-drive support | Local path settings/resolvers | Untested | Paths can point to removable media | Imported state after removal/reconnect unknown | None focused | Drive-letter changes, unavailable media, reconnect | Add environment tests and useful source-unavailable feedback |
| Mapped-drive support | Local path settings/resolvers | Untested | Windows mapped paths are accepted as strings | Unknown | None | Disconnected mapping, credentials, restart | Qualify and document limits |
| NAS/UNC support | Local path settings/resolvers | Untested | UNC paths appear accepted | Unknown | None | UNC normalization, latency, credentials, offline NAS | Qualify and document limits |
| RcloneRemote provider | `RcloneManifestReader.cs`; transfer runner/adapter; settings | Partial | Loads remote manifest and transfers via rclone | Core controllers expected; unverified | Command builder, parser, adapter, executor | Real rclone, credentials, long transfer, Fullscreen | Fix timeout semantics and qualify real remote |
| Manifest loading | `LoadManifestJson`; `RcloneManifestReader` | Partial | Local/remote JSON loaded | Drives imported games | Reader and downstream tests are limited | Provider failures, large/corrupt manifest, persistent notification | Extract loader contract and error notification |
| Manifest parsing | `PersonalCloudLibrarySource.cs`, `ParseManifest` | Partial | Deserializes and normalizes manifest | Same imported metadata | Indirect tests | Schema/version, duplicates, malformed fields, limits | Extract parser/validator and define supported schema |
| Manifest generation | `ManifestGenerationService.cs`; settings commands | Partial | Generates v3-style manifest/report from folder | Desktop management only | Existing generation coverage is not comprehensive | Collision, traversal, large library, deterministic output | Keep Desktop-only; strengthen service and tests |
| Manifest validation | `LibraryVerificationService.cs`; dashboard state | Partial | Generates report and status | Useful feedback should reach Fullscreen via notifications | Dashboard verification state | Full schema/path checks, provider failures, notification integration | Share validator with import; define severity |
| Library import | `GetGames`; mapper/path helpers | Partial | Imports manifest entries | Required GameLibrary behavior | Some state/services tests | Import error notification, malformed item isolation, Fullscreen | Stop failures appearing as empty success |
| Import error feedback | `GetGames`; diagnostics | Broken | Logs/writes diagnostics but returns empty list | No useful persistent feedback demonstrated | None | All failure/recovery cases | Add stable localized import notification |

## Setup, settings, and startup

| Feature | Files involved | Status | Desktop behavior | Fullscreen behavior | Tests present | Tests missing | Release action |
|---|---|---|---|---|---|---|---|
| Setup wizard | `Setup/*`; navigation; wizard XAML | Partial | Multi-step Desktop wizard edits a draft and saves | Dedicated Fullscreen wizard deferred | Wizard view-model tests | Installed Playnite, provider-specific success/failure, close/reopen | Qualify Desktop and document Fullscreen setup boundary |
| Setup reminders | `SetupLaunchPolicyService.cs`; `ShowSetupReminders` | Disconnected | UI toggle exists; service and tests excluded from projects | Notification behavior absent | Six physical tests do not compile | Startup integration and notification UI | Include files and wire startup policy |
| Settings validation | settings view model; setup validation | Partial | Provider-dependent checks and test commands exist | Desktop management only | Selected setup/status tests | Path edge cases, rclone semantics, installed UI | Consolidate validation contracts |
| Settings edit/cancel/save | `PersonalCloudLibrarySourceSettings.cs` | Partial | Clone on begin, restore on cancel, save on end | Not a Fullscreen surface | Migration clone test | Full settings round trip, nested lifecycle, action mutations | Add contract tests and prevent incidental saves |
| Settings migration | `SettingsMigrationService.cs`; V3 settings | Partial | Preserves selected prior values and applies defaults | Affects all modes | Four migration areas | Real 0.1.1/0.2.0 fixtures, corrupt settings, repeated migration | Make sequential/idempotent and qualify upgrades |
| Automatic startup refresh | `AutoRefreshOnApplicationStart`; `OnApplicationStarted` | Disconnected | Visible toggle is never read | No behavior | None | All startup conditions | Implement through `StartupActionService` or remove setting |
| Automatic startup manifest generation | `AutoGenerateManifestOnApplicationStart`; startup event | Disconnected | Visible toggle is never read | Desktop management only | None | Ordering, invalid setup, failure, cancellation | Implement through startup service or remove setting |
| Open dashboard at startup | navigation/settings | Partial | Opens Desktop dashboard when enabled | Dedicated Fullscreen dashboard deferred | No focused integration test | Installed Desktop startup | Add one lifecycle test and runtime smoke test |

## Installed state and game actions

| Feature | Files involved | Status | Desktop behavior | Fullscreen behavior | Tests present | Tests missing | Release action |
|---|---|---|---|---|---|---|---|
| Installed-state detection | mapper; `CacheStatusService`; path resolvers | Partial | Uses launch file/install directory existence | Required; unverified | Status and command-policy tests | File/directory parity, stale cache, media disconnect, Fullscreen | Centralize `LibraryItemStateResolver` and qualify |
| Play actions | metadata mapping in `PersonalCloudLibrarySource.cs` | Partial | File play actions generated for installed entries | Required; unverified | No focused play-action suite | Arguments/working dir, missing executable, Fullscreen | Add metadata/controller tests and runtime qualification |
| Install/download actions | `GetInstallActions`; `RcloneInstallController` | Partial | Local/rclone copy to cache with summaries | Required; unverified | Transfer adapters/executor and command policy | Controller integration, failures, Fullscreen feedback | Use one queue/execution path and qualify |
| Context-aware game commands | `GameCommandPolicyService`; game commands | Partial | Install/open/cancel/retry/remove policy exists | Custom Desktop menu availability not guaranteed | Policy/service tests | Installed Playnite and multi-select integration | Core operations must not depend on Desktop-only custom commands |
| Game details window | `Views/CloudGameDetails*` | Partial | Desktop custom details surface | Deferred/not required in Fullscreen | No focused view lifecycle tests | Data refresh, close/dispose | Treat as Desktop enhancement only |
| Uninstall/cache removal | uninstall controller; path helpers | Partial | Deletes file/folder and updates installed state | Required; unverified | Command/path policy coverage | Controller integration, reparse points, Fullscreen | Harden deletion policy and qualify |
| Deletion safety | `ResolveSafeUninstallTarget`; adapter cleanup | Partial | Blocks roots/cache root/outside cache by default | Same controller logic expected | Outside-cache and command-policy examples | Junction/symlink, ancestor reparse, prefix collision, partial ownership | Add `SafeCacheDeletionPolicy` before release |

## Transfers

| Feature | Files involved | Status | Desktop behavior | Fullscreen behavior | Tests present | Tests missing | Release action |
|---|---|---|---|---|---|---|---|
| Transfer queue | manager/executor/controller/dashboard | Partial | Jobs and states exist; callers still own execution | Controller path expected | Manager/executor tests | Queue-owned workers, shutdown, integration | Consolidate scheduling/execution ownership |
| Concurrent transfers | `CloudTransferManager` | Partial | Limits jobs marked active to 1–4 | Same shared service expected | Concurrency unit tests | Real concurrent copies/rclone, race tests | Qualify actual worker concurrency |
| Cancellation | jobs/manager/adapters/process runner | Partial | Cancels tokens and removes partials | Required feedback unverified | Adapter/executor tests | Queued/active race, shutdown, Fullscreen feedback | Centralize and qualify |
| Retry | manager/game commands | Partial | Creates linked attempt; fire-and-forget execution | Required recovery feedback unverified | Manager/policy tests | Observed worker, exception, shutdown, Fullscreen | Queue owns retry execution |
| Progress reporting | parser/manager/sidebar/dashboard view models | Partial | Aggregate/sidebar progress exists | Useful feedback unverified | Parser/aggregate/view-model tests | Real rclone/local integration, throttling, UI lifecycle | Define update cadence and qualify |
| Shutdown during transfer | `OnApplicationStopped`; transfer wiring | Broken | Event is detached only; work is not stopped/awaited | Same process risk | None | All shutdown states | Add bounded queue shutdown contract |
| Partial-file cleanup | both adapters | Partial | `.partial` data removed unconditionally on failures | Same execution path expected | Cancellation/success tests | Cleanup failure, retained-partial setting semantics, reparse safety | Define ownership and setting behavior |
| Post-transfer verification | executor/adapters; `VerifyAfterTransfer` | Disconnected | Size/existence verification occurs, but visible toggle is ignored | Same path expected | Success/failure by size | Setting semantics, hash claims, file/directory parity | Make mandatory/remove toggle or wire explicit safe semantics |
| Remove-incomplete setting | `RemoveIncompleteTransferFiles` | Disconnected | Visible toggle ignored; adapters clean unconditionally | Same | None for toggle | On/off behavior if retained | Decide safe contract and align UI |
| Rclone timeout | process runner/settings | Partial | Total process killed after configured 5–300 seconds | Same | Default/migration tests | Long progressing transfer, inactivity | Replace total deadline with explicit inactivity/connect semantics |

## Dashboard, status, activity, and notifications

| Feature | Files involved | Status | Desktop behavior | Fullscreen behavior | Tests present | Tests missing | Release action |
|---|---|---|---|---|---|---|---|
| Dashboard | dashboard views/services; navigation | Partial | Top-panel/sidebar/main-menu access and window | Dedicated Fullscreen dashboard deferred | Navigation/status/view-model units | Installed Desktop lifecycle, repeated open/close | Qualify Desktop and fix subscription lifetime |
| Dashboard status | state/status/verification/cache services | Partial | Ready/setup/unavailable/warning/transfer states | Status must be conveyed through core feedback | Good service-level tests | Real provider state transitions | Connect import/transfer notifications and qualify |
| Transfer queue presentation | dashboard XAML/view models | Partial | Queue state/progress controls exist | No dedicated Fullscreen queue required | Queue item tests | Production binding and interactive cancel/retry | End-to-end Desktop test |
| Recent activity | activity service/tracker | Disconnected | Services/tests exist but production integration is absent | No dedicated Fullscreen activity required | Unit tests | Terminal-job integration and dashboard binding | Wire one activity stream |
| Library update notifications | `NotifyLibraryUpdates` | Disconnected | Toggle is never consumed | Useful feedback absent | None | Toggle and outcomes | Implement notification routing |
| Transfer completed notifications | `NotifyTransferCompleted` | Disconnected | Toggle is never consumed | Useful feedback absent | None | Toggle/dedup/action | Implement notification routing |
| Transfer failed notifications | `NotifyTransferFailed` | Disconnected | Toggle is never consumed | Required useful feedback absent | None | Toggle/dedup/action | Implement notification routing |
| Source unavailable notifications | `NotifySourceUnavailable` | Disconnected | Toggle is never consumed | Required useful feedback absent | None | Toggle/recovery | Implement persistent status notification |
| Verification warning notifications | `NotifyVerificationWarnings` | Disconnected | Toggle is never consumed | Useful feedback absent | None | Toggle/recovery | Implement notification routing |

## Diagnostics, presentation, distribution, and upgrades

| Feature | Files involved | Status | Desktop behavior | Fullscreen behavior | Tests present | Tests missing | Release action |
|---|---|---|---|---|---|---|---|
| Import diagnostics | `WriteImportDiagnostics`; safe writer | Partial | Writes last-import text under plugin data when enabled | Accessible outside Fullscreen through file/system | Safe writer indirectly | Failure writing, redaction, disabled mode | Define support bundle and privacy/redaction |
| Verification reports | verification service/navigation | Partial | Generates and opens report | Useful failures need notification summary | Dashboard verification tests | Report schema/golden output, file errors | Stabilize report and link from notifications |
| Manifest reports/backups | generation/safe writer | Partial | Writes under plugin data with backups | Desktop management only | Limited | Rotation, failure, permissions | Define retention and test |
| Branding | icon, runtime wide logo, docs logo, XAML/package wiring | Complete | Corrected supplied-reference PNGs are wired | Extension/game branding available | PNG/XAML tests | Final human approval in installed Playnite | Keep current assets; repair clean-checkout test defect |
| Localization | `Localization/en_US.xaml`; XAML/C# | Partial | Some menus/resources localized; 74 settings XAML literals plus C# strings remain | Core feedback English-only in places | No completeness test | Key/reference/literal audit and visual review | Localize all user-facing text |
| Desktop support | settings, wizard, dashboard, commands | Partial | Broad management experience exists | N/A | Service/view-model tests | Installed end-to-end qualification | Complete exposed settings and qualify |
| Fullscreen game support | metadata/controllers/notifications | Untested | N/A | Import, state, play, install, uninstall required but unverified | Component tests only | Fullscreen runtime matrix | Qualify core game workflows; no dedicated dashboard/wizard |
| Debug package | project output/workflow | Untested | No Debug `.pext` qualification | Same install format | Debug build occurs | Debug pack/install/content | Add release checklist evidence |
| Release package | package script/workflow | Broken | Workflow does not reach trustworthy green artifact | Same | Content checks exist after package step | Clean checkout, Toolbox, install | Repair CI and use official validation |
| Add-on Database metadata | `playnite-addon/addon-database.yaml` | Broken | Advertises 0.2.0-era behavior | Same listing | None | YAML and Toolbox addon verify | Rewrite after features complete |
| Installer metadata | `playnite-addon/installer.yaml` | Broken | Only 0.2.0 package entry | Same updater | None | YAML, Toolbox installer verify, URL/package | Add qualified 1.0.0 entry last |
| Upgrade from 0.1.1 | migration/installer | Untested | No installed qualification | Same settings/data | Generic migration units | Real fixture and Playnite upgrade | Required release gate |
| Upgrade from 0.2.0 | migration/installer | Untested | No installed qualification | Same settings/data | Generic migration units | Real fixture and Playnite upgrade | Required release gate |
| Version synchronization | all release surfaces/workflow | Broken | 0.2.0/0.3.0/0.3.2 disagree | Same package identity | None | Automated cross-surface validation | Add validator; bump to 1.0.0 only at final gate |

## Exposed-setting disposition

Every visible setting is accounted for below.

| Setting | Runtime status | 1.0 disposition |
|---|---|---|
| `Enabled` | Connected | Retain and test disabled behavior |
| `LibraryDisplayName` | Connected | Retain and localize surrounding UI |
| `ShowTopPanelButton` | Connected | Retain; Desktop-only |
| `ShowSidebarDashboard` | Connected | Retain; Desktop-only |
| `OpenDashboardAtStartup` | Connected | Retain; Desktop-only; add lifecycle test |
| `ShowSetupReminders` | Disconnected | Include/wire policy and tests |
| `AutoRefreshOnApplicationStart` | Disconnected | Implement through startup service or remove before 1.0 |
| `AutoGenerateManifestOnApplicationStart` | Disconnected | Implement through startup service or remove before 1.0 |
| Provider/path/rclone fields | Connected, partially qualified | Retain; centralize validation/path resolution |
| Generated-manifest status fields | Connected | Retain as internal persisted status; validate migration |
| `LocalCacheFolder` | Connected | Retain; harden path/deletion policy |
| `AllowDownloads` | Connected | Retain and test controllers |
| `TreatMissingFilesAsUninstalled` | Connected in state mapping | Retain and add state matrix tests |
| `VerifyAfterTransfer` | Disconnected from choice | Prefer mandatory verification and remove the misleading toggle; otherwise wire/test semantics |
| `RemoveIncompleteTransferFiles` | Disconnected from choice | Define safe partial ownership; wire or remove toggle |
| `TransferConcurrency` | Connected | Retain; test actual workers and migration |
| `UninstallBehavior` | Connected | Retain; harden safety and test file/directory parity |
| Five `Notify*` toggles | Disconnected | Implement through notification services |
| `EnableDiagnostics` | Connected | Retain; test write failure/redaction |
| `RcloneTimeoutSeconds` | Connected with unsafe semantics | Redefine as inactivity/connect timeout and migrate/document |
| `AllowUninstallOutsideCacheFolder` | Connected, dangerous | Keep advanced and disabled by default; reparse/root guards remain mandatory |

## 1.0 acceptance interpretation

- No entry marked **Broken** or **Disconnected** may remain visible at 1.0.
- **Partial** entries must meet the completion criterion in the implementation plan and release checklist.
- **Untested** environment claims must be qualified on representative Windows/Playnite systems or explicitly removed from release claims.
- Desktop owns setup, settings, dashboard, diagnostics, and management tools.
- Fullscreen must pass the core imported-game workflow matrix, but it does not require a custom dashboard or setup wizard.
