# Personal Cloud Library Source — User-Friendly Dashboard Design

**Status:** Ready for user review  
**Target release:** 0.3.0  
**Development branch:** `feature/user-friendly-dashboard`  
**Date:** 2026-07-11

## 1. Purpose

Personal Cloud Library Source already imports user-owned game catalogs from local manifests, folders, external drives, NAS locations, and rclone remotes. It also supports downloading or copying game files into a local cache, safe uninstall behavior, manifest generation, and verification reports.

The current experience is functional but configuration-heavy. Most useful commands are buried in one long settings view, the library client `Open()` action does nothing, and the plugin does not currently provide top-panel, sidebar, main-menu, or game-context-menu entry points.

Version 0.3.0 will make the plugin feel like a native part of Playnite by adding an everyday dashboard, guided setup, context-aware actions, transfer progress, and safer user-facing recovery flows.

## 2. Goals

1. Make the plugin accessible from Playnite's top panel, sidebar, main menu, and game context menu.
2. Give users a clear dashboard showing source, manifest, cache, transfer, and verification status.
3. Replace the first-run wall of settings with a guided setup wizard.
4. Preserve Playnite's native Install, Play, Uninstall, and library-update workflows.
5. Provide visible, cancellable transfer progress without blocking Playnite's UI.
6. Keep cached-file removal safe by default and prevent partial transfers from appearing installed.
7. Migrate existing settings without forcing current users through setup again.
8. Use Playnite theme resources, localization resources, keyboard navigation, and text-based status labels.
9. Develop and test everything on the feature branch before any merge, release, or Playnite Addon Database pull request.

## 3. Non-goals for 0.3.0

The following are explicitly deferred:

- Automatic rclone installation or self-update.
- OAuth authentication implemented inside the plugin.
- A full remote file explorer; the initial cloud flow lists configured remotes and top-level folders.
- True pause and resume unless the underlying transfer implementation can resume without restarting.
- Bandwidth limiting and scheduled downloads.
- Multiple simultaneous source profiles.
- Cross-device transfer synchronization.
- Delta patching or game update management.
- A full JSON manifest editor.
- External metadata API calls for automatic platform correction.
- A custom dashboard for Playnite Fullscreen mode. Native library import, install, play, uninstall, and notifications continue to work there where supported.

## 4. Product defaults

New installations use these defaults:

- Top-panel button: enabled.
- Sidebar dashboard: enabled.
- Setup reminders: enabled.
- Open dashboard at Playnite startup: disabled.
- Transfer concurrency: 1, configurable from 1 to 4.
- Verify after transfer: enabled.
- Remove incomplete temporary files after failed non-resumable transfers: enabled.
- Uninstall outside the managed cache: disabled.
- Missing cached files are treated as uninstalled: enabled.
- Technical details are collapsed by default.

Existing installations retain all safety-critical values during migration. Newly introduced visibility settings default to enabled unless the user later changes them.

## 5. Navigation model

### 5.1 Top-panel button

Implement `GetTopPanelItems()` and return one `TopPanelItem`.

- Title and tooltip: **Personal Cloud Library**.
- Icon: the plugin icon or a Playnite-compatible monochrome cloud-library glyph.
- Activation: open the dashboard window.
- Visibility: bound to `ShowTopPanelButton`.
- During active transfers, update the tooltip with a concise status such as `2 transfers active` or `1 transfer failed`.
- Do not animate the toolbar icon.

### 5.2 Sidebar dashboard

Implement `GetSidebarItems()` with a `SidebarItem` of type `View`.

- Title: **Cloud Library**.
- Visibility: bound to `ShowSidebarDashboard`.
- Opened callback: create or reuse the sidebar dashboard control.
- The sidebar item may expose aggregate byte-based transfer progress through `ProgressValue` and `ProgressMaximum`.
- The dashboard view must not perform folder scans, rclone calls, or file copies on the UI thread.

### 5.3 Main menu

Implement `GetMainMenuItems()` with a grouped section:

`Extensions > Personal Cloud Library`

Commands:

- Open Dashboard
- Update Library
- Generate or Refresh Manifest
- Verify Library
- Open Cache Folder
- Open Source Location
- Open Latest Report
- Settings

Commands that do not apply to the current configuration are hidden or disabled with an explanatory label.

### 5.4 Game context menu

Implement `GetGameMenuItems()` only for games belonging to this plugin. Mixed-library selections do not receive destructive batch actions.

Single-game commands:

- View Cloud Library Details
- Install to This Computer
- View Transfer Progress or Cancel Transfer, when active
- Open Cached Folder, when cached
- Open Source Location, when safely resolvable
- Verify This Entry
- Copy Source Path
- Copy Local Cache Path, when available
- Remove Cached Copy, when cached
- Retry Last Transfer, when retryable

Multi-selection commands for plugin-owned games:

- Install Selected Games
- Verify Selected Entries
- Remove Selected Cached Copies
- Copy Source Paths
- Open Dashboard

Batch removal is restricted to paths inside the managed cache in 0.3.0.

### 5.5 Library client action

Change `PersonalCloudLibrarySourceClient.Open()` from a no-op into a dashboard-opening action when Playnite invokes the library client command in Desktop mode. In Fullscreen mode it returns safely without trying to open the desktop dashboard.

## 6. Dashboard

### 6.1 Window behavior

The top-panel button opens a Playnite-owned, resizable dashboard window.

- Initial size: approximately 760 by 620 pixels.
- First opening: centered over Playnite.
- Remember size and position after the first opening.
- Reuse the existing window instead of opening duplicates.
- Restore an off-screen saved position to a visible monitor.
- Use Playnite theme brushes and typography.
- Support scrolling at smaller sizes.

The sidebar and window use separate view instances backed by shared observable dashboard state. This avoids WPF visual-parent conflicts while keeping all status and commands synchronized.

### 6.2 Header and overall state

Show:

- Personal Cloud Library title.
- Friendly source name and source type.
- One text status: Ready, Needs setup, Source unavailable, Verification warnings, Updating, Downloading, or Transfer failed.
- Status icons always have accompanying text.

### 6.3 Library status card

Show:

- Items in manifest.
- Games imported into Playnite.
- Cached locally.
- Available remotely or at source.
- Invalid or warning entries.
- Last library update.
- Last successful source connection.

Primary actions:

- Update Playnite Library.
- Verify Library.

The update action invokes Playnite's normal library update/import path. It must not independently perform a second database-import implementation from the dashboard.

### 6.4 Source card

Show:

- Friendly source type.
- Friendly source or remote name.
- Sanitized path.
- Connection status.
- Manifest location.
- Last successful connection test.
- Last manifest generation time when plugin-generated.

Actions:

- Open Source, when a safe local or browser-accessible location exists.
- Test Connection.
- Change Source.

Credentials, tokens, raw rclone configuration, and secret command-line values are never displayed.

### 6.5 Cache and storage card

Show:

- Cache path.
- Current cache size.
- Number of cached games.
- Free disk space.
- Active and failed transfer counts.

Actions:

- Open Cache Folder.
- Manage Cached Games.
- Clear Safe Temporary Files.

Safe cleanup removes only plugin-created temporary files, abandoned transfer metadata, and explicitly disposable files. It never removes installed cached games automatically.

### 6.6 Quick actions

Show context-aware actions:

- Update Library.
- Generate or Refresh Manifest.
- Verify Setup.
- Open Latest Report.
- Open Cache.
- Settings.

Rules:

- Do not offer manifest generation for an externally managed manifest unless the user explicitly chooses to create a plugin-owned copy.
- Show rclone testing only for cloud sources.
- Show source-folder actions only when the path is usable.

### 6.7 Recent activity

Store and show the most recent 50 compact activity records, displaying the latest five by default.

Examples:

- Library update completed with added and removed counts.
- Manifest generated.
- Transfer completed or failed.
- Source connection recovered.
- Verification completed with warnings.

Records may link to a selected game, report, retry action, or settings page. They must not contain credentials or a full private library inventory.

### 6.8 Empty and warning states

The dashboard provides dedicated states for:

- Setup incomplete.
- Setup complete but no library update performed.
- Empty source.
- Source unavailable.
- Invalid manifest.
- Cache unavailable or read-only.
- Verification warnings.

Each state includes at least one direct recovery action. Detailed exceptions remain in logs and reports.

## 7. First-run setup wizard

### 7.1 Launch rules

Open automatically when the plugin is enabled and no valid setup exists. Also open from Start Setup, Change Source, or Reset Setup Wizard.

If dismissed, do not repeatedly reopen it. Show a persistent dashboard notice and an optional actionable Playnite notification.

### 7.2 Draft-state safety

Use a separate `SetupDraft` object. Incomplete wizard values do not modify active settings. Active settings are updated only after final validation and confirmation.

Persist only safe recovery state:

- Selected source type.
- Source location.
- Last completed step.
- Scan-result file path.
- Selected cache path.

A cancelled or failed setup leaves an already working configuration unchanged.

### 7.3 Step 1 — source choice

Present four plain-language choices:

1. On this computer or an external drive.
2. On a NAS or network location.
3. In cloud storage through rclone.
4. I already have a manifest file.

Keep internal provider identifiers unchanged for compatibility, but do not expose `LocalFile`, `LocalFolder`, or `RcloneRemote` as primary labels.

### 7.4 Step 2 — source configuration

#### Local or external drive

- Browse for a folder.
- Confirm it exists and is readable.
- Show available disk information where useful.
- Offer Scan Folder.

#### NAS or network location

- Accept UNC or mapped-drive paths.
- Optional friendly name.
- Test host/share reachability and read access.
- Store generated manifests in plugin data by default so NAS write access is not required.
- Windows remains responsible for SMB credentials.

#### Cloud through rclone

- Detect rclone in the plugin tools directory, PATH, previous setting, and user-selected location.
- Show detected version.
- List configured remotes rather than requiring manual remote-name entry.
- List top-level folders for the selected remote.
- Test remote and selected path.
- Provide a collapsed technical-details section.
- When no remotes exist, provide Open rclone Configuration, Refresh, and Advanced Manual Setup.

#### Existing manifest

Validate immediately:

- File exists and is readable.
- JSON parses.
- Manifest version is supported.
- Items collection exists.
- Required identifiers exist.
- Duplicate IDs and invalid paths become warnings when possible.

Parsing errors block progress. Noncritical warnings may be reviewed and accepted.

### 7.5 Step 3 — scan and preview

For folder-based sources, run a cancellable background scan.

Show:

- Folders checked.
- Items detected.
- Skipped items.
- Warning count.

Preview columns:

- Include.
- Game.
- Platform.
- Type.
- Source.

Filters:

- All.
- Included.
- Warnings.
- Skipped.
- Unknown platform.
- Duplicate.

Allow basic corrections:

- Exclude an item.
- Rename its display title.
- Select a platform.
- Ignore a folder.

A full manifest editor remains out of scope.

### 7.6 Step 4 — cache and install behavior

Configure:

- Cache location.
- Download/copy on Install or catalog-only mode.
- Treat missing cached files as uninstalled.
- Safe uninstall behavior.

Default generated cache location is plugin data unless the user chooses another writable path.

Dangerous removal outside the managed cache is hidden under Advanced and requires an explicit warning confirmation.

### 7.7 Step 5 — review and completion

Show a readable summary of:

- Source.
- Manifest item and warning counts.
- Manifest ownership and storage location.
- Cache path and free space.
- Install and uninstall behavior.

Finish sequence:

1. Validate the draft.
2. Back up existing plugin-generated settings and manifest where applicable.
3. Save active settings.
4. Generate or copy the manifest.
5. Write a generation report.
6. Run verification.
7. Trigger Playnite's normal library update.
8. Open the dashboard.
9. Show a completion or recoverable-failure notification.

A final library-update failure does not discard the completed source setup.

## 8. Game details and native actions

### 8.1 Preserve Playnite actions

The plugin continues to provide Playnite install and uninstall controllers. Custom menu commands call Playnite's native install or uninstall operation rather than creating separate state-changing pathways.

- Install to This Computer invokes the normal Playnite install action.
- Remove Cached Copy invokes the normal Playnite uninstall action.
- Play remains Playnite's normal Play action.
- Update Library remains Playnite's normal library update.

### 8.2 Cloud Library Details window

Show:

- Game name and platform.
- Installation state.
- Friendly source.
- Relative source path.
- File or folder type.
- Expected size where known.
- Cache path and local size.
- Last transfer date.
- Verification result.
- Manifest ID, package role, notes, and last-seen scan time.

Actions are enabled only when applicable.

## 9. Transfer system

### 9.1 Components

#### `CloudTransferManager`

Owns pending, active, completed, failed, cancelled, and retryable jobs. Enforces concurrency and publishes observable queue state.

#### `CloudTransferJob`

Contains:

- Job ID.
- Game ID and display name.
- Sanitized source and destination.
- Provider type.
- Transfer status.
- Bytes transferred and total bytes when known.
- Start and completion times.
- Friendly error category.
- Cancellation token source.

#### `CloudTransferExecutor`

Delegates actual work to adapters around `LocalFileCopier` and `RcloneFileCopier`. Executors report structured progress and honor cancellation.

#### `CloudTransferHistoryService`

Persists the latest 50 completed, failed, or cancelled summaries without credentials or raw command output.

### 9.2 States

Valid main states:

- Queued.
- Preparing.
- Connecting.
- CalculatingSize.
- Transferring.
- Verifying.
- Finalizing.
- Completed.
- Cancelled.
- Failed.

Completed, Cancelled, and Failed are terminal for that job. Retry creates a new job linked to the previous attempt.

### 9.3 Progress and queue

The dashboard and sidebar share one queue state.

Show:

- Game name.
- Current state.
- Byte progress and percentage when total size is known.
- Transfer speed.
- Estimated time only when enough stable progress data exists.
- Cancel and Details actions.
- Retry for eligible failures.

Aggregate progress is byte-weighted. When total sizes are unknown, show an indeterminate state and active-job count.

### 9.4 Temporary destinations and finalization

All transfers write to plugin-recognizable temporary destinations such as `.pcls-partial`.

Sequence:

1. Validate source and destination.
2. Ensure the destination is inside the allowed cache unless an explicitly approved safe override applies.
3. Check free space when size is known.
4. Create temporary destination.
5. Transfer.
6. Perform basic or enhanced verification.
7. Atomically move or rename into final location when possible.
8. Mark installation complete through the Playnite install controller.
9. Remove temporary metadata.

Failed or cancelled partial data never appears installed.

### 9.5 Existing destination handling

- Valid existing copy: Use Existing Files, Verify Again, or Replace.
- Incomplete previous transfer: Start Over; offer Resume only after genuine resumability is implemented and tested.
- Unrelated conflict: require another destination or cancellation. Never overwrite automatically.

### 9.6 Cancellation and retry

Cancellation signals the adapter, waits briefly for clean shutdown, then terminates the child process only when required. Non-resumable partial data is removed when the user confirms cancellation and cleanup.

Retry revalidates source, authentication, destination, and available space. Persisted retry metadata excludes passwords, tokens, and secret environment values.

### 9.7 Verification levels

Basic verification:

- Destination exists.
- Expected launch file exists where defined.
- File or folder is nonempty.
- Size is plausible.
- Destination is inside the allowed cache.

Enhanced verification uses manifest-provided size, hash, launch file, or required structure.

Manual verification is available from the context menu and dashboard.

## 10. Safe uninstall and cleanup

Normal uninstall means removing the local cached copy while keeping the game entry and Playnite metadata.

It must not:

- Delete the source on cloud, NAS, external drive, or local library root.
- Remove the manifest entry.
- Delete Playnite artwork or metadata.
- Delete outside the configured cache by default.

After successful uninstall, the game remains in Playnite as not installed and can be installed again.

Path safety tests must defend against:

- Similar-prefix paths outside the cache.
- Parent traversal.
- Root paths.
- Empty paths.
- Redirected or symbolic paths where applicable.
- Batch removal escaping the cache.

## 11. Settings redesign

Replace the single long settings page with tabs.

### General

- Enable plugin.
- Library display name.
- Show top-panel button.
- Show sidebar dashboard.
- Open dashboard at startup.
- Setup reminders.
- Startup library update.
- Open Dashboard.
- Run Setup Wizard.

### Source

Show only fields relevant to the chosen friendly source type. Put Test Connection beside the fields it tests.

### Manifest

- Current path.
- Plugin-generated or external ownership.
- Last generation and successful load.
- Item count.
- Generate/refresh.
- Preview scan.
- Reports.
- Restore previous plugin-generated version.

### Downloads and Cache

- Allow installs.
- Cache path and free space.
- Concurrency 1–4.
- Verify after transfer.
- Temporary cleanup.
- Missing-file behavior.
- Safe uninstall.
- Advanced external-cache removal warning.

### Notifications

Independent options for:

- Setup reminders.
- Library-update results.
- Transfer completed.
- Transfer failed.
- Source unavailable.
- Verification warnings.

Progress remains in the dashboard rather than notifications.

### Diagnostics

- Plugin and API versions.
- Source status.
- Complete verification.
- Reports and diagnostics folders.
- Plugin data folder.
- Copy safe diagnostic summary.
- Detailed logging.

### Advanced

- rclone timeout.
- Custom relative manifest path.
- Startup generation.
- Compatibility options.
- Raw provider identifier.
- Dangerous uninstall option.
- Reset dashboard position.
- Reset wizard.
- Reset settings with preservation choices.

## 12. Settings migration

Set the redesigned settings schema marker to:

```text
SettingsVersion = 3
```

The current settings model has no schema marker, so missing or zero is treated as the legacy pre-0.3.0 format.

Migration behavior:

1. Load current settings.
2. Detect a missing, zero, or older schema marker.
3. Copy every existing source, cache, startup, diagnostic, and uninstall value.
4. Apply defaults only to new fields.
5. Validate migrated settings.
6. Save only after validation.
7. Back up the old settings file before replacement.
8. Log a concise migration result.

Existing valid configurations are considered setup-complete and do not reopen the first-run wizard.

## 13. Localization

Move every new user-facing string into localization resources, including:

- Toolbar, sidebar, and menu text.
- Dashboard labels and states.
- Wizard content.
- Transfer states.
- Errors and notifications.
- Confirmations.
- Accessibility labels.

Initial requirement:

- Complete `en_US.xaml` coverage.
- No missing keys.
- English fallback.
- Contributor documentation for additional translations.

Existing hard-coded settings strings are moved as part of the settings redesign rather than duplicated.

## 14. Accessibility and theming

Requirements:

- Logical Tab and Shift+Tab navigation.
- Enter activates the primary action.
- Space activates cards, buttons, and checkboxes as appropriate.
- Escape closes dialogs or confirms loss of wizard progress.
- Arrow-key navigation for source cards and tabs.
- Visible focus indicators.
- Screen-reader-friendly names.
- Text accompanying status icons.
- No color-only meaning.
- Playnite theme brushes rather than fixed dark backgrounds.
- Light, dark, high-DPI, and narrow-window support.
- Wrapped paths and errors.
- No fixed 540-pixel minimum widths across general fields.
- No required animation.

Progress announcements are throttled to meaningful milestones rather than every percentage point.

## 15. Architecture

Proposed structure:

```text
Dashboard/
  CloudLibraryDashboardView.xaml
  CloudLibraryDashboardViewModel.cs
  CloudLibraryDashboardWindowService.cs
  CloudLibrarySidebarItem.cs
  DashboardActivityService.cs
  DashboardStateStore.cs

Setup/
  SetupWizardView.xaml
  SetupWizardViewModel.cs
  SetupDraft.cs
  SetupStep.cs
  SetupValidationService.cs

Transfers/
  CloudTransferManager.cs
  CloudTransferJob.cs
  CloudTransferExecutor.cs
  CloudTransferHistoryService.cs
  ICloudTransferAdapter.cs

Views/
  CloudGameDetailsView.xaml
  CloudGameDetailsViewModel.cs

Services/
  LibraryStatusService.cs
  SourceConnectionService.cs
  CacheStatusService.cs
  PluginNavigationService.cs
```

Responsibilities:

- Main plugin class: Playnite registration and lifecycle delegation.
- Dashboard services: status aggregation and presentation-ready state.
- Setup services: draft validation and completion orchestration.
- Transfer services: queue, execution, progress, cancellation, retry, and history.
- Existing manifest, copy, verification, and safe-write services remain the source of core behavior and are adapted rather than duplicated.

The main plugin class must not absorb the dashboard, wizard, transfer, and file-operation implementations.

## 16. Threading and lifecycle

- Network, NAS, folder scan, manifest generation, cache-size calculation, and transfer operations run off the UI thread.
- All long operations accept cancellation.
- UI updates marshal through the WPF dispatcher.
- Plugin shutdown signals active operations and performs bounded cleanup; it must not block indefinitely.
- Sidebar or dashboard closure does not cancel transfers.
- Dashboard state remains available when neither view is open.
- Event subscriptions are removed during disposal or application shutdown.

## 17. Error handling and privacy

Every user action ends in Success, Warning, Actionable Error, or Cancelled.

Friendly categories:

- Source did not respond.
- Authentication required.
- Source path missing.
- Insufficient cache space.
- Cache not writable.
- Manifest entry incomplete.
- rclone stopped unexpectedly.
- Verification mismatch.

Logs may include operation, provider type, sanitized path, exception type, timeout, and stack trace.

Never log or display:

- Passwords.
- Authentication tokens.
- Full rclone configuration.
- Secret environment variables.
- A full private game inventory in the safe diagnostic summary.

## 18. Testing strategy

### Unit tests

Dashboard:

- Ready, setup-needed, unavailable, empty, warning, updating, active-transfer, and failed-transfer states.
- Friendly provider names and context-aware commands.

Wizard:

- All four source choices.
- Back/next navigation.
- Blocking errors versus accepted warnings.
- Cancellation leaves active settings unchanged.
- Completion promotes the draft only after validation.
- Current values prefill Change Source.

Migration:

- Existing manifest.
- Local folder.
- UNC NAS.
- rclone.
- Custom cache.
- Disabled plugin.
- Dangerous uninstall setting.
- Partially configured legacy setup.

Menus:

- No selection.
- One plugin game.
- Multiple plugin games.
- Mixed selection.
- Cached, remote-only, transferring, failed, and invalid states.

Transfers:

- All valid state transitions.
- Invalid terminal-state transitions rejected.
- Cancellation from each active state.
- Retry categories.
- Byte-weighted aggregate progress.

Cache safety:

- Inside-cache paths.
- Similar-prefix external paths.
- Traversal.
- Root and empty paths.
- Temporary cleanup.
- Batch removal.

### Integration tests

Use temporary folders and fake adapters for:

- Local source.
- External-drive-style source.
- NAS-like structure.
- Existing manifest.
- Interrupted transfer.
- Destination conflict.
- Permission failure where testable.
- rclone success, auth error, timeout, malformed output, progress, cancellation, and nonzero exit.

Automated tests must not require a real cloud account.

### Manual Playnite matrix

Test:

- Default light and dark themes.
- At least one custom theme.
- 1366x768 and 1920x1080.
- Narrow window.
- 100%, 125%, 150%, and 200% scaling.
- Existing manifest, local folder, external drive, mapped drive, UNC NAS, and rclone.
- Offline source and expired authentication.
- Fresh install and upgrade.
- Wizard cancellation and completion.
- Install, cancel, retry, verify, and uninstall.
- Hidden toolbar and sidebar.
- Reset and recovery flows.

## 19. Acceptance criteria

The branch is ready for user testing only when:

1. Toolbar opens the dashboard.
2. Sidebar loads without blocking Playnite.
3. Main-menu commands work.
4. Game-context actions appear only when applicable.
5. The wizard completes all four source paths.
6. Existing settings migrate without losing safety-critical values.
7. Transfers show progress and can be cancelled.
8. Partial files never appear installed.
9. Default uninstall cannot remove outside the managed cache.
10. Dashboard and wizard work in light and dark themes.
11. Keyboard navigation covers primary workflows.
12. Automated tests pass.
13. Debug and Release builds pass.
14. A test `.pext` package is produced and inspected.
15. Setup, dashboard, transfer, and troubleshooting documentation is updated.
16. No merge, release, version publication, or public database PR occurs without the user's explicit approval.

## 20. Development and approval workflow

1. Keep all work on `feature/user-friendly-dashboard`.
2. Commit this design specification first.
3. After user review and approval, write the detailed implementation plan.
4. Implement in reviewable phases with tests.
5. Build and package a 0.3.0 test artifact from the feature branch.
6. User tests it in Playnite.
7. Fix issues on the feature branch.
8. Only after explicit approval:
   - merge to `main`;
   - finalize version and changelog;
   - build the release package;
   - create or update the Playnite Addon Database submission.

No pull request is created merely by writing this design document. The feature branch remains an isolated review and development branch until the user authorizes the next publication step.
