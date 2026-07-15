# PCLS 1.0 Release-Readiness Audit

**Audit date:** 2026-07-13

**Repository:** `KurtusCobain/PersonalCloudLibrarySource`

**Branch:** `feature/user-friendly-dashboard-cleanup`

**Audited HEAD:** `17141439c107cb96d2d0d34f88b9c413b22ebecd`

**Target release:** 1.0.0

**Scope:** Read-only release audit. No implementation changes are part of this pass.

## Executive result

**NOT RELEASE READY.** The branch contains a substantial working 0.3.2 implementation, but it cannot be promoted to 1.0.0 until four blockers and thirteen high-severity findings are closed. The current GitHub build is red, source files are omitted from both production and test projects, visible settings are disconnected, release metadata is inconsistent, import errors can look like empty libraries, and destructive filesystem behavior has not been hardened against reparse points.

Finding totals:

| Severity | Count |
|---|---:|
| Blocker | 4 |
| High | 13 |
| Medium | 13 |
| Low | 6 |
| Cleanup | 5 |
| **Total** | **41** |

## Evidence boundaries

Verified in this audit:

- Branch, commit, worktree, recent history, PR #9 state, and GitHub check state.
- Physical source inventory versus old-style MSBuild `<Compile>` entries.
- Extension, assembly, README, changelog, add-on, installer, package script, workflow, test, XAML, localization, and branding surfaces.
- Runtime references for every visible setting.
- Import, transfer, cancellation, retry, shutdown, path, deletion, event, and settings lifecycle code.
- Current official Playnite SDK documentation and the official maintained `JosefNemec/PlayniteExtensions` source tree.
- Repository-local Markdown links; no broken local links were found.

Not verified in this pass:

- Interactive behavior in an installed Desktop or Fullscreen Playnite instance.
- Real external drive, mapped drive, UNC/NAS, or rclone transfers.
- Toolbox `pack`, `verify addon`, or `verify installer`; a Playnite installation path was not established.
- Clean install and upgrades from 0.1.1 or 0.2.0.
- Remote documentation links beyond the official sources explicitly fetched.
- A final 1.0.0 package, checksum, release upload, or Add-on Database submission.

## Repository and pull-request state

- The isolated audit worktree is on `feature/user-friendly-dashboard-cleanup` at `17141439c107cb96d2d0d34f88b9c413b22ebecd` and matched `origin/feature/user-friendly-dashboard-cleanup` when inspected.
- PR #9 is open and draft, targets `main`, and uses this branch as its head. It remains the correct PR for the hardening work. Its title, `Verify PCLS 0.3.2 branding and package cleanup`, should be retitled after the 1.0 scope is approved.
- PR #9 has a failing build check and a successful CodeQL check. It must remain draft and must not be merged.
- PR #8 targets the older `feature/user-friendly-dashboard` branch and is not the release-hardening PR.
- The repository description is blank.

Local baseline verification on 2026-07-13 succeeded: Debug MSBuild completed with zero warnings/errors and NUnitLite reported 85/85 passing. This does not clear B1 because the local worktree contains an empty untracked `tools/assets` directory; GitHub's clean runner does not, and the same test fails there before packaging.

## Official Playnite compliance baseline

The audit used these primary sources:

- [Extension manifest](https://api.playnite.link/docs/master/tutorials/extensions/extensionsManifest.html): `extension.yaml` is mandatory; `Id`, `Name`, `Author`, valid .NET `Version`, `Module`, and `Type` define loading and display. `Icon` and `Links` are supported.
- [Plugin development](https://api.playnite.link/docs/master/tutorials/extensions/plugins.html): plugins target .NET Framework 4.6.2; the SDK is not fully thread-safe; UI-owned work must use `MainView.UIDispatcher`; non-SDK Playnite assemblies must not be referenced.
- [Library plugins](https://api.playnite.link/docs/master/tutorials/extensions/libraryPlugins.html): imported games need stable `GameId`, correct plugin ownership, installed state, play action, and install directory where applicable. Install and uninstall controllers provide optional library actions.
- [Extension data directory](https://api.playnite.link/docs/master/tutorials/extensions/dataDirectory.html): generated data belongs under `GetPluginUserDataPath()`, not the extension installation folder.
- [Extension lifecycle events](https://api.playnite.link/docs/master/tutorials/extensions/events.html): `OnApplicationStarted`, `OnApplicationStopped`, and `OnLibraryUpdated` are supported lifecycle boundaries.
- [Toolbox](https://api.playnite.link/docs/master/tutorials/toolbox.html): official packaging is `Toolbox.exe pack`; Add-on Database and installer manifests must be validated with `Toolbox.exe verify addon` and `Toolbox.exe verify installer`.
- [Official maintained extensions](https://github.com/JosefNemec/PlayniteExtensions): current library integrations use .NET Framework plugin settings, localized resources, stable import-error notification IDs, and removal of persistent import-error notifications after successful imports. These are relevant practices, not an architecture to copy wholesale.
- [Official Add-on Database](https://github.com/JosefNemec/PlayniteAddonDatabase): the submission manifests and linked installer are release artifacts and require official validation.

Compliant foundations already present:

- Production and tests target .NET Framework 4.6.2 and reference Playnite SDK 6.16.0.
- `extension.yaml` exists with the stable ID `61993828-67a8-4468-93a2-293442e36328`, `Type: GameLibrary`, module, icon, and links.
- Generated manifests, reports, backups, cache defaults, and diagnostics resolve through `GetPluginUserDataPath()` with a local-app-data fallback.
- Direct Playnite API UI updates found in the transfer/dashboard path generally use `UIDispatcher`.
- No references to non-SDK Playnite assemblies were found.

## Blockers

### B1 — The required GitHub build is failing

- **Files/classes:** `PersonalCloudLibrarySource.Tests/Ui/UiContractTests.cs`, method `BrandArtwork_UsesDirectReferencePngs`; `.github/workflows/build.yml`
- **Evidence:** The current PR check reports 85 tests, 84 passed, 1 error. The test enumerates `tools/assets`, but that empty directory is not tracked by Git and is absent on GitHub runners, causing `DirectoryNotFoundException`.
- **Why it matters:** A stable release cannot proceed with a red required build. Local success can be misleading when an untracked empty directory exists.
- **User impact:** No trustworthy package artifact is produced by the PR workflow.
- **Recommended fix:** Make the test tolerate a missing legacy directory or assert absence through tracked paths without requiring the directory. Add a clean-checkout test environment and ensure the workflow starts from tracked files only.
- **Required tests:** Run the full NUnitLite executable from a fresh clone/worktree with `tools/assets` absent; assert all tests pass.

### B2 — Setup reminder production code and tests compile nowhere

- **Files/classes:** `PersonalCloudLibrarySource/Services/SetupLaunchPolicyService.cs`, class `SetupLaunchPolicyService`; `PersonalCloudLibrarySource.Tests/Setup/SetupLaunchPolicyServiceTests.cs`; both `.csproj` files
- **Evidence:** The physical files exist, but neither old-style project includes its file. Production has 57 included versus 59 physical C# files, one of which is irrelevant package content; tests have 20 included versus 21 physical files.
- **Why it matters:** `ShowSetupReminders` is exposed in settings, yet its intended policy is absent from the assembly and its six apparent tests never execute.
- **User impact:** First-run and invalid-configuration reminder behavior does nothing while the UI claims it is configurable.
- **Recommended fix:** Add both files to their projects, wire the policy into startup, and test the lifecycle behavior through the plugin boundary.
- **Required tests:** Project-inclusion audit; six policy unit tests; startup integration tests for new, valid, dismissed, previously completed, disabled, and reminders-disabled states.

### B3 — There is no synchronized 1.0 release identity

- **Files:** `PersonalCloudLibrarySource/extension.yaml`, `Properties/AssemblyInfo.cs`, `README.md`, `CHANGELOG.md`, `playnite-addon/addon-database.yaml`, `playnite-addon/installer.yaml`, `.github/workflows/build.yml`, release docs
- **Evidence:** Runtime/assembly/workflow use 0.3.2; README and add-on manifests describe 0.2.0; historical design documents target 0.3.0; no 1.0.0 installer entry or release notes exist.
- **Why it matters:** Package naming, update discovery, user documentation, and Add-on Database submission cannot identify one release.
- **User impact:** Users may receive stale metadata, fail to discover upgrades, or install a package whose advertised behavior/version differs from its binary.
- **Recommended fix:** Establish a single release metadata validator and perform the final version bump only after qualification. Preserve historical documents as historical records, but synchronize every live release surface.
- **Required tests:** Automated version-surface test, YAML parse, package-manifest inspection, installer/package URL match, and checksum verification.

### B4 — No official package or Add-on Database qualification exists

- **Files:** `tools/package-extension.ps1`, `.github/workflows/build.yml`, `playnite-addon/addon-database.yaml`, `playnite-addon/installer.yaml`
- **Evidence:** Packaging uses `Compress-Archive` and a renamed ZIP. The workflow does not run Toolbox `pack`, `verify addon`, or `verify installer`; it does not parse YAML with a YAML parser; it is hard-coded to 0.3.2. No clean-install or upgrade evidence exists.
- **Why it matters:** The requested public release lacks the official validation path for package structure and distribution manifests.
- **User impact:** A package can pass repository checks yet fail Playnite installation, Add-on Database ingestion, or upgrade behavior.
- **Recommended fix:** Add non-mutating release validation that locates Toolbox, builds Debug and Release artifacts, invokes official pack/verify commands, parses both YAML files, inspects contents, and records clean-install/upgrade evidence.
- **Required tests:** Toolbox pack; verify addon; verify installer; Debug and Release package smoke tests; clean install; upgrades from 0.1.1 and 0.2.0.

## High findings

### H1 — Manifest failures appear as an empty library

- **File/method:** `PersonalCloudLibrarySource/PersonalCloudLibrarySource.cs`, `GetGames`
- **Why it matters:** The broad catch logs and writes diagnostics but returns an empty collection. Official maintained library plugins expose a persistent import-error notification and remove it on recovery.
- **User impact:** A missing drive, invalid manifest, timeout, or provider failure can look like the user has no games.
- **Recommended fix:** Add `ImportNotificationService` with a stable notification ID, localized message, diagnostics/settings action, and recovery removal. Preserve prior imported data according to Playnite behavior; never describe an operational error as a successful empty import.
- **Required tests:** Local missing file, invalid JSON, rclone failure, notification deduplication, action routing, and recovery removal.

### H2 — Recursive deletion does not defend against reparse points

- **Files/methods:** `PersonalCloudLibrarySource.cs`, `ResolveSafeUninstallTarget` and `IsPathInsideCacheFolder`; `PersonalCloudLibraryUninstallController.cs`, `Uninstall`; `Transfers/LocalTransferAdapter.cs` and `Transfers/RcloneTransferAdapter.cs`, partial cleanup helpers
- **Why it matters:** Safety is based on normalized lexical prefixes. Recursive `Directory.Delete` does not first reject a target or ancestor that is a symlink/junction/reparse point.
- **User impact:** A cache path containing a junction can redirect deletion outside the managed cache. Enabling `AllowUninstallOutsideCacheFolder` increases the blast radius.
- **Recommended fix:** Centralize canonical cache deletion policy; reject roots, cache root, reparse-point target/ancestors, ambiguous/nonexistent parents, and outside-cache paths by default. Separate deletion of transfer-owned partial paths from manifest-controlled targets.
- **Required tests:** Drive root, cache root, prefix collision, `..`, case differences, file/directory, junction/symlink target and ancestor, missing path, outside-cache opt-in, and partial cleanup ownership.

### H3 — Startup refresh and startup manifest generation settings are disconnected

- **Files/methods:** `PersonalCloudLibrarySourceSettings.cs`; `PersonalCloudLibrarySourceSettingsView.xaml`; `PersonalCloudLibrarySource.Navigation.cs`, `OnApplicationStarted`
- **Why it matters:** `AutoRefreshOnApplicationStart` and `AutoGenerateManifestOnApplicationStart` are visible and migrated but never read at runtime. Startup only refreshes dashboard state and optionally opens the dashboard.
- **User impact:** Users enable startup actions that never occur.
- **Recommended fix:** Introduce `StartupActionService` with deterministic ordering, eligibility rules, cancellation, one-run guards, and user-visible results.
- **Required tests:** Each option alone, both options, invalid setup, disabled plugin, duplicate startup call, failure, cancellation, and ordering.

### H4 — Post-transfer verification and incomplete-file cleanup settings are disconnected

- **Files/classes:** `PersonalCloudLibrarySourceSettingsV3.cs`; `PersonalCloudLibrarySourceSettingsView.xaml`; `Transfers/CloudTransferExecutor.cs`; both transfer adapters
- **Why it matters:** `VerifyAfterTransfer` and `RemoveIncompleteTransferFiles` are never read. Verification and cleanup currently happen unconditionally in some paths, so the UI misstates control and semantics.
- **User impact:** User choices are ignored; failure behavior cannot be predicted from settings.
- **Recommended fix:** Define safe 1.0 semantics. Verification should remain mandatory for correctness or the switch should be removed. Partial cleanup should default safe and be consistently owned; if configurable, wire it explicitly and explain retained partials.
- **Required tests:** Success, verification failure, cancellation, process failure, cleanup enabled/disabled if retained, file/directory parity, and retry after retained partial data.

### H5 — Notification settings are disconnected

- **Files/classes:** `PersonalCloudLibrarySourceSettingsV3.cs`; `PersonalCloudLibrarySourceSettingsView.xaml`; import, transfer, verification, and library-update paths
- **Why it matters:** All five notification toggles are visible and migrated but not consumed: library updates, transfer completed, transfer failed, source unavailable, and verification warnings.
- **User impact:** Meaningful failures may be silent while selected notification preferences do nothing.
- **Recommended fix:** Add small import and transfer notification services with stable IDs, severity mapping, deduplication, localized content, and settings-aware routing.
- **Required tests:** Every toggle on/off, stable ID replacement, recovery removal, action callback, and no duplicate notifications.

### H6 — Application shutdown does not cancel or await active transfers

- **Files/methods:** `PersonalCloudLibrarySource.Navigation.cs`, `OnApplicationStopped`; `PersonalCloudLibrarySource.Transfers.cs`, `DisposeTransferManager`; `Transfers/CloudTransferManager.cs`
- **Why it matters:** Shutdown only removes an event handler. It does not cancel jobs, stop rclone/local copies, bound waiting, or preserve a recoverable state.
- **User impact:** Playnite can exit while files are being written, leaving orphaned processes or partial content and an unknown installed state.
- **Recommended fix:** Give the queue a shutdown token and state transition, reject new work, cancel active/queued jobs, terminate rclone, wait for a bounded interval off the UI thread, and leave deterministic partial-file handling.
- **Required tests:** Queued and active local/rclone shutdown, timeout, partial cleanup, process termination, no new jobs, idempotent shutdown, and Playnite smoke test.

### H7 — Retry uses unobserved fire-and-forget tasks

- **File/method:** `PersonalCloudLibrarySource.GameCommands.cs`, retry command around `Task.Run`
- **Why it matters:** `ExecuteRclone` and `ExecuteLocal` are launched without observation, continuation, centralized scheduling, or shutdown ownership.
- **User impact:** Exceptions can be lost; retries can outlive the plugin lifecycle; UI state and notification behavior can become inconsistent.
- **Recommended fix:** Move execution ownership into `TransferQueueService`; commands enqueue only. Observe every worker, record terminal state, and bind workers to shutdown cancellation.
- **Required tests:** Worker exception, retry success/failure/cancellation, shutdown during retry, duplicate retry suppression, and terminal-state notification.

### H8 — The rclone timeout is a total-transfer deadline

- **Files/methods:** `Transfers/RcloneProcessRunner.cs`, `Run`; `PersonalCloudLibrarySourceSettings.cs`; setup draft/settings XAML
- **Why it matters:** The default 90 seconds and exposed 5–300 range are applied to the entire rclone process, not inactivity or connection establishment. Normal large transfers can be killed while making progress.
- **User impact:** Downloads predictably fail based on size or network speed despite healthy progress.
- **Recommended fix:** Define timeout semantics explicitly. Prefer an inactivity watchdog reset by output/progress plus a separate bounded process-start/connect timeout; allow an appropriate upper range.
- **Required tests:** Long progressing transfer, stalled transfer, no-output startup, boundary values, cancellation versus timeout, and error messaging.

### H9 — Dashboard recent activity is not integrated

- **Files/classes:** `Dashboard/DashboardActivityService.cs`; `Transfers/TransferActivityTracker.cs`; dashboard view and navigation wiring
- **Why it matters:** Both services and their unit tests exist, but production references show no integration. The exposed dashboard/recent-activity experience is incomplete.
- **User impact:** Completed, failed, and cancelled transfers do not reliably populate the promised activity history.
- **Recommended fix:** Connect terminal transfer events to one activity service and dashboard state; define lifetime, cap, ordering, localization, and whether history persists.
- **Required tests:** End-to-end completed/failed/cancelled activity, deduplication, ordering, cap, dashboard refresh, and shutdown/reopen behavior.

### H10 — Dashboard view models retain stale event subscriptions

- **File/class:** `Dashboard/CloudLibraryDashboardViewModel.cs`, constructor subscription to `DashboardStateStore.PropertyChanged`
- **Why it matters:** Each dashboard view creates a view model that subscribes to a long-lived store, but the view model has no dispose/unsubscribe path.
- **User impact:** Repeated dashboard opens can retain closed view models and multiply updates.
- **Recommended fix:** Implement a clear view-model lifetime and unsubscribe on view/window close, or use a weak event pattern.
- **Required tests:** Open/close cycles, subscriber count or weak-reference collection, single update after reopen, and no callback after disposal.

### H11 — Localization is materially incomplete

- **Files:** `PersonalCloudLibrarySourceSettingsView.xaml`, other views, controllers/navigation, `Localization/en_US.xaml`
- **Why it matters:** The audit found 85 localization keys but 74 literal user-facing XAML attributes in the settings view alone, plus many hard-coded dialogs and transfer messages in C#.
- **User impact:** The plugin cannot provide a consistent localized experience and mixes localized/fallback English in one UI.
- **Recommended fix:** Inventory every user-facing string, add stable `LOCPLS*` keys, use `ResourceProvider`, and keep only logs/technical identifiers unlocalized.
- **Required tests:** No forbidden literal user-facing XAML attributes, key uniqueness, every referenced key exists, formatting placeholders match, and Desktop/Fullscreen smoke review.

### H12 — Fullscreen game workflows have no qualification evidence

- **Files/classes:** `PersonalCloudLibrarySource.cs`, metadata mapping and controllers; game command/detail views; release checklist
- **Why it matters:** 1.0 explicitly requires imported games, installed state, play, install, uninstall/cache removal, and useful feedback in Fullscreen. No Fullscreen runtime test evidence exists, and desktop-only dialogs/windows cannot be assumed to cover Fullscreen.
- **User impact:** Core library operations may be absent, inaccessible, or unreadable in Fullscreen.
- **Recommended fix:** Keep the dashboard and setup wizard Desktop-only, but qualify core GameLibrary metadata/controllers and notification feedback in Fullscreen. Remove dependence on desktop-only custom surfaces for required game operations.
- **Required tests:** Fullscreen import, remote/cached installed state, play, install, cancellation feedback, uninstall refusal/success, source unavailable, and restart persistence.

### H13 — Upgrade safety is inferred, not qualified

- **Files/classes:** `SettingsMigrationService.cs`; `PersonalCloudLibrarySourceSettings.cs`; `PersonalCloudLibrarySourceSettingsV3.cs`; installer manifest
- **Why it matters:** Unit tests cover selected migration fields, but no real serialized 0.1.1/0.2.0 settings fixtures or installed-package upgrades have been exercised. Naming says V3 while `CurrentSettingsVersion` is 4.
- **User impact:** Existing paths, provider selections, uninstall safety choices, or UI preferences may reset or acquire unsafe defaults during the first stable upgrade.
- **Recommended fix:** Create immutable legacy fixtures, make migrations sequential/idempotent, document defaults, and run real upgrades in Playnite.
- **Required tests:** 0.1.1 and 0.2.0 serialized fixtures, current settings no-op, repeated migration, corrupt/partial settings, cancel/save lifecycle, and installed upgrade smoke tests.

## Medium findings

| ID | Finding | Evidence and release action |
|---|---|---|
| M1 | Queue workers busy-wait | `CloudTransferExecutor.WaitForExecutionTurn` polls with `Thread.Sleep(50)`. Replace with queue-owned signaling or tasks and test cancellation while queued. |
| M2 | Transfer scheduling responsibility is split | `CloudTransferManager` changes state to `Preparing`, while callers synchronously execute jobs and retries use `Task.Run`. Consolidate queue scheduling/execution ownership. |
| M3 | Legacy copier fallback duplicates semantics | `RcloneInstallController` retains `RcloneFileCopier`/`LocalFileCopier` fallback beside new adapters. Remove after one tested execution path exists. |
| M4 | Oversized plugin class mixes responsibilities | `PersonalCloudLibrarySource.cs` is 1,162 lines and owns import, parsing, mapping, paths, safety, diagnostics, reports, and settings. Extract only the risk-bearing boundaries in the design. |
| M5 | Settings view model is oversized | `PersonalCloudLibrarySourceSettings.cs` is 828 lines and mixes persistence, validation, browsing, tests, generation, reports, and UI status. Separate domain validation/actions without rewriting bindings wholesale. |
| M6 | Manifest generation and verification are large mixed services | `ManifestGenerationService.cs` is 572 lines; `LibraryVerificationService.cs` is 430 lines. Isolate parsing/validation/path resolution contracts used by import and tools. |
| M7 | Local/remote path guarantees are incomplete | Local source containment uses lexical prefix checks and remote paths are string-combined. Add explicit rooted/relative rules and traversal tests across providers. |
| M8 | External, mapped, and UNC support is assumption-based | Settings accept paths, but no targeted tests or runtime evidence cover disconnect/reconnect, credentials, latency, or UNC normalization. Add environment qualification. |
| M9 | Install/uninstall preparation failures are log-only | `GetInstallActions` and `GetUninstallActions` broadly catch and return no actions. Route meaningful failures through status/notifications without spamming menus. |
| M10 | Settings schema terminology is confusing | `PersonalCloudLibrarySourceSettingsV3ViewModel` and V3 model coexist with schema version 4. Rename only with serialization-safe compatibility or document the boundary. |
| M11 | Verification is size-only | Transfer verification compares existence and byte totals, not hashes or manifest-provided integrity. Define 1.0 integrity claims accurately and add optional checksum support only if the manifest supports it. |
| M12 | Documentation does not describe current behavior reliably | README says 0.2.0 and planned v0.3, while 0.3.2 code exists. Rewrite after implementation and distinguish Desktop management from Fullscreen core workflows. |
| M13 | Workflow validates one hard-coded package name | `.github/workflows/build.yml` embeds 0.3.2 three times. Derive version/package path from the extension manifest and validate synchronization. |

## Low findings

| ID | Finding | Release action |
|---|---|---|
| L1 | Assembly metadata is stale | Update empty description/company and 2019 copyright in `Properties/AssemblyInfo.cs` during final metadata pass. |
| L2 | Author naming is inconsistent | Standardize the extension manifest author, assembly company, README attribution, and Add-on Database author without changing the extension ID. |
| L3 | Repository description is blank | Add a concise GitHub description after the 1.0 scope and wording are approved. |
| L4 | Some fallback English is embedded in view models | Move dashboard fallbacks such as `Needs setup` and `Not configured` to localization resources. |
| L5 | Local Markdown checker covers local targets only | Add an allowlisted HTTP link check or periodic documentation validation with clear network-failure handling. |
| L6 | Historical documents can be mistaken for current release contracts | Add a short historical-status note or index; do not rewrite prior approved specs as if they were live 1.0 metadata. |

## Cleanup findings

| ID | Finding | Release action |
|---|---|---|
| C1 | Package-restored source appears under the production tree | Keep `PersonalCloudLibrarySource/packages/**` untracked/ignored and exclude it from physical-source audits. |
| C2 | Dead legacy copier path remains | Remove `RcloneFileCopier` and `LocalFileCopier` only after controller fallback is eliminated and parity tests pass. |
| C3 | Empty/legacy branding directory assumption remains in tests | Remove the `tools/assets` assumption rather than preserving an empty directory. |
| C4 | Release-facing comments contain dated operational notes | Remove the `6/2/2026` manifest-generation note and similar transient wording from live Add-on Database metadata. |
| C5 | PR title reflects the prior branding-only scope | Retitle PR #9 once these documents are approved; keep it draft until all release gates pass. |

## Documentation and metadata audit

| Surface | Current evidence | Required 1.0 action |
|---|---|---|
| `README.md` | Declares 0.2.0 current and v0.3 planned | Rewrite capabilities from the final matrix; add setup, provider, rclone, upgrade, troubleshooting, Desktop/Fullscreen, and legal boundaries. |
| `CHANGELOG.md` | Latest 0.3.2 | Add 1.0.0 only when behavior and qualification are complete. |
| `DEVELOPMENT.md` | Present but not a release qualification record | Add reproducible restore/build/test/package/Toolbox commands. |
| `CONTRIBUTING.md` | Present | Align required checks with final CI and source-inclusion audit. |
| `SECURITY.md` | Present | Ensure filesystem deletion and reporting guidance match final safety policy. |
| `docs/*` | Several v0.2/v0.3 planning documents | Preserve historical plans, update live guides, add troubleshooting and upgrade instructions. |
| `extension.yaml` | Valid shape, version 0.3.2 | Final synchronized 1.0.0 bump; confirm name, author, links, icon, and stable ID. |
| Add-on database | Description says 0.2.0 and contains dated note | Rewrite concise current description, tags/links, and validate with Toolbox. |
| Installer | Only 0.2.0 package entry | Add 1.0.0 entry with correct API version, date, URL, checksum if schema supports it, then verify. |
| Screenshots/images | PNG branding is wired and local links resolve | Perform final visual approval and screenshot review after UI/localization work. |

No missing local Markdown targets were found. Remote links and screenshots still require final review.

## Recommended first implementation pass

Make one release-baseline pass before feature work:

1. Repair B1 without preserving an empty directory assumption.
2. Add a deterministic source-file inclusion audit and include `SetupLaunchPolicyService` plus its tests.
3. Add a release metadata validator that reports drift but does not prematurely bump to 1.0.0.
4. Make Debug build, full tests, and Release package inspection green from a fresh checkout.
5. Record the baseline evidence in the release checklist.

This pass is intentionally narrow. It establishes trustworthy compilation and validation before path, import, transfer, startup, dashboard, localization, documentation, or final version changes.

## Release decision

- **PR #9:** Correct draft PR for this branch; keep draft, do not merge.
- **1.0.0 tag/package:** Not authorized and not ready.
- **Implementation:** Begin only after these audit/design/plan documents are reviewed and approved.
