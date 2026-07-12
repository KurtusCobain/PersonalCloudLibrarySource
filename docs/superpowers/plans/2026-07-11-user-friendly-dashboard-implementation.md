# User-Friendly Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver Personal Cloud Library Source 0.3.0 with native Playnite toolbar/sidebar/menu access, a shared dashboard, guided setup, context-aware game actions, observable transfer progress, safer cache handling, migrated settings, localization, and release validation.

**Architecture:** Keep `PersonalCloudLibrarySource` as the Playnite integration boundary and move dashboard, wizard, transfer, status, and navigation behavior into focused services and view models. Reuse Playnite's native install, uninstall, play, and library-update pathways; UI commands delegate to those pathways rather than duplicating state changes.

**Tech Stack:** C# 7-compatible .NET Framework 4.6.2, WPF, Playnite SDK 6.16.0, old-style MSBuild projects, NUnit 3 test project, GitHub feature branch `feature/user-friendly-dashboard`.

## Global Constraints

- Target release: `0.3.0`.
- Required Playnite API remains `6.16.0`.
- Do not merge to `main` or open the public release PR before Austin explicitly approves the test package.
- Default transfer concurrency is 1 and may be configured from 1 to 4.
- Default uninstall must never remove source files or paths outside the managed cache.
- Failed or cancelled partial transfers must never appear installed.
- New UI uses Playnite theme resources and localized strings; no fixed 540-pixel-wide general layout.
- Existing valid 0.2.0 configurations migrate without forcing the first-run wizard.

---

### Task 1: Add test infrastructure and settings migration foundation

**Files:**
- Create: `PersonalCloudLibrarySource.Tests/PersonalCloudLibrarySource.Tests.csproj`
- Create: `PersonalCloudLibrarySource.Tests/packages.config`
- Create: `PersonalCloudLibrarySource.Tests/SettingsMigrationServiceTests.cs`
- Create: `PersonalCloudLibrarySource/SettingsMigrationService.cs`
- Modify: `PersonalCloudLibrarySource/PersonalCloudLibrarySourceSettings.cs`
- Modify: `PersonalCloudLibrarySource/PersonalCloudLibrarySource.csproj`
- Modify: `PersonalCloudLibrarySource/PersonalCloudLibrarySource.sln`

**Interfaces:**
- Produces: `SettingsMigrationService.Migrate(PersonalCloudLibrarySourceSettings settings) : SettingsMigrationResult`.
- Produces: schema marker `PersonalCloudLibrarySourceSettings.CurrentSettingsVersion = 3`.
- Produces: navigation, notification, transfer, and verification defaults required by later tasks.

- [ ] Write tests proving a legacy settings object preserves source/cache/uninstall values while receiving 0.3.0 defaults.
- [ ] Write tests proving a current settings object is not overwritten.
- [ ] Run the tests and confirm they fail because migration and new properties do not exist.
- [ ] Implement the minimum migration service and settings properties.
- [ ] Run migration tests and the complete test suite.
- [ ] Commit with `Add settings migration foundation`.

### Task 2: Add friendly source naming and dashboard state services

**Files:**
- Create: `PersonalCloudLibrarySource/Dashboard/FriendlySourceNameProvider.cs`
- Create: `PersonalCloudLibrarySource/Dashboard/CloudLibraryDashboardState.cs`
- Create: `PersonalCloudLibrarySource/Dashboard/LibraryStatusService.cs`
- Create: `PersonalCloudLibrarySource/Dashboard/CacheStatusService.cs`
- Create tests under: `PersonalCloudLibrarySource.Tests/Dashboard/`
- Modify: `PersonalCloudLibrarySource/PersonalCloudLibrarySource.csproj`

**Interfaces:**
- Produces: `FriendlySourceNameProvider.GetDisplayName(string providerType) : string`.
- Produces: immutable dashboard snapshots with source, manifest, import, cache, warning, and transfer counts.

- [ ] Test friendly labels for LocalFile, LocalFolder, RcloneRemote, null, and unknown values.
- [ ] Test dashboard state for ready, setup-incomplete, source-unavailable, warning, and transfer-active cases.
- [ ] Implement pure status mapping first, then filesystem/Playnite adapters.
- [ ] Run tests and commit with `Add dashboard status services`.

### Task 3: Add dashboard shell and native Playnite navigation

**Files:**
- Create: `PersonalCloudLibrarySource/Dashboard/CloudLibraryDashboardView.xaml`
- Create: `PersonalCloudLibrarySource/Dashboard/CloudLibraryDashboardView.xaml.cs`
- Create: `PersonalCloudLibrarySource/Dashboard/CloudLibraryDashboardViewModel.cs`
- Create: `PersonalCloudLibrarySource/Dashboard/CloudLibraryDashboardWindowService.cs`
- Create: `PersonalCloudLibrarySource/Dashboard/CloudLibrarySidebarItem.cs`
- Create: `PersonalCloudLibrarySource/Services/PluginNavigationService.cs`
- Modify: `PersonalCloudLibrarySource/PersonalCloudLibrarySource.cs`
- Modify: `PersonalCloudLibrarySource/PersonalCloudLibrarySourceClient.cs`
- Modify: `PersonalCloudLibrarySource/PersonalCloudLibrarySource.csproj`
- Add command-visibility tests.

**Interfaces:**
- Produces: `PluginNavigationService.OpenDashboard()` and `OpenSettings()`.
- Provides: `GetTopPanelItems`, `GetSidebarItems`, `GetMainMenuItems`.

- [ ] Test menu visibility and command routing without opening real windows.
- [ ] Implement a reusable dashboard window and a separate sidebar view instance sharing one view model state store.
- [ ] Add top-panel button, sidebar view, grouped main-menu commands, and functional library-client Open behavior.
- [ ] Verify light/dark theme resources and keyboard focus manually.
- [ ] Commit with `Add dashboard navigation shell`.

### Task 4: Add first-run setup wizard and draft-state safety

**Files:**
- Create files under `PersonalCloudLibrarySource/Setup/` for `SetupDraft`, step model, validation service, view model, XAML, and window service.
- Add wizard tests under `PersonalCloudLibrarySource.Tests/Setup/`.
- Modify settings and navigation services.

**Interfaces:**
- Produces: draft-only editing until `CompleteSetup()` succeeds.
- Supports local/external folder, NAS, rclone remote, and existing manifest flows.

- [ ] Test step navigation, cancellation, validation blocking, warning acceptance, and final draft-to-active copy.
- [ ] Add cancellable scan preview and safe rclone remote discovery adapters.
- [ ] Add cache behavior review and completion sequence.
- [ ] Commit with `Add guided setup wizard`.

### Task 5: Add game context actions and details view

**Files:**
- Create files under `PersonalCloudLibrarySource/Views/` for cloud game details.
- Create `PersonalCloudLibrarySource/Services/GameCommandService.cs`.
- Modify `PersonalCloudLibrarySource/PersonalCloudLibrarySource.cs`.
- Add context-menu tests.

- [ ] Test single, multi, mixed-library, cached, remote-only, invalid, and active-transfer selections.
- [ ] Implement context-aware actions that call Playnite's native install/uninstall operations.
- [ ] Add safe details display with sanitized source information.
- [ ] Commit with `Add cloud game actions`.

### Task 6: Add shared transfer manager and safe partial-file workflow

**Files:**
- Create files under `PersonalCloudLibrarySource/Transfers/` for job, state machine, manager, executor, adapters, and history.
- Modify install controllers and copy helpers.
- Add transfer and cache-safety tests.

- [ ] Test all valid and invalid state transitions.
- [ ] Test byte-weighted aggregate progress, cancellation, retry classification, and terminal states.
- [ ] Test `.pcls-partial` cleanup and prevention of outside-cache destinations.
- [ ] Route existing local and rclone copies through the manager.
- [ ] Commit with `Add managed transfer queue`.

### Task 7: Redesign settings, localization, and accessibility

**Files:**
- Replace the single settings layout with tabbed theme-aware XAML.
- Expand `PersonalCloudLibrarySource/Localization/en_US.xaml`.
- Add localization-key and settings-validation tests.

- [ ] Move all new user-facing strings into localization resources.
- [ ] Add General, Source, Manifest, Downloads and Cache, Notifications, Diagnostics, and Advanced tabs.
- [ ] Verify tab order, focus visibility, wrapping, high-DPI behavior, and non-color status labels.
- [ ] Commit with `Redesign settings and localization`.

### Task 8: Documentation, packaging, and test package

**Files:**
- Modify `README.md`, `CHANGELOG.md`, and setup/troubleshooting documentation.
- Update extension metadata only when the test build is ready.

- [ ] Run Debug and Release MSBuild.
- [ ] Run all NUnit tests.
- [ ] Package `PersonalCloudLibrarySource-0.3.0-test.pext`.
- [ ] Inspect archive contents and manifest IDs.
- [ ] Provide the test package and manual matrix to Austin.
- [ ] Do not merge or create the public release PR until Austin approves.
