# PCLS 1.0 Release Checklist

**Branch:** `feature/user-friendly-dashboard-cleanup`

**Pull request:** #9 (must remain draft until all release gates pass)

**Target:** 1.0.0

**Initial status:** Not release ready

## Evidence rules

- Check a row only after recording the command or manual procedure, environment, date, result, and artifact/log location.
- “Not applicable” requires a written product/design justification; it cannot replace a failed required gate.
- A failed Blocker/High gate stops release preparation. Fix it with a regression test and rerun affected downstream gates.
- Use a fresh checkout/worktree for final build/package evidence.
- Do not merge PR #9 or publish/tag 1.0.0 from this checklist automatically.

## 1. Scope and repository state

- [ ] Active branch is exactly `feature/user-friendly-dashboard-cleanup`.
- [ ] HEAD equals the reviewed PR #9 head commit.
- [ ] PR #9 targets `main`, remains draft, and no competing release PR was created.
- [ ] Worktree is clean before qualification.
- [ ] No generated, temporary, self-mutating workflow, automation loop, or one-off mutation script is tracked.
- [ ] Stable extension ID remains `61993828-67a8-4468-93a2-293442e36328`.
- [ ] All Blocker and High audit findings are closed with linked tests/evidence.

Evidence:

| Date | Commit | Command/check | Result | Evidence location |
|---|---|---|---|---|
| | | | | |

## 2. Branding approval

- [ ] `PersonalCloudLibrarySource/icon.png` is the approved Reference A-derived 512×512 transparent icon.
- [ ] `PersonalCloudLibrarySource/Assets/pcls-logo-wide.png` is the approved transparent wide/header logo.
- [ ] `docs/assets/pcls-logo-full.png` is the approved Reference B-derived full logo.
- [ ] Images decode, have expected dimensions/alpha, and transparent corners where required.
- [ ] Dashboard, setup wizard, extension manifest, docs, and package use the correct variant.
- [ ] No malformed, blurry, obsolete SVG, base64 payload, chunk, or materialization script remains.
- [ ] Human visual approval is recorded in installed Playnite and rendered docs.

Evidence:

| Reviewer/date | Assets reviewed | Automated result | Installed/rendered result |
|---|---|---|---|
| | | | |

## 3. Source inclusion and static contracts

- [ ] Production source-file inclusion audit passes.
- [ ] Test source-file inclusion audit passes.
- [ ] Every project `<Compile>` entry exists.
- [ ] `SetupLaunchPolicyService.cs` compiles in production.
- [ ] `SetupLaunchPolicyServiceTests.cs` compiles and its six tests execute.
- [ ] Restored `packages`, `obj`, and `bin` content is excluded from tracked-source audits.
- [ ] No visible setting is marked Broken or Disconnected in the final feature matrix.
- [ ] No unobserved fire-and-forget transfer task remains.
- [ ] Event subscriptions and disposable/process resources have explicit lifecycle ownership.

Evidence:

| Date | Audit/test | Result | Counts/notes |
|---|---|---|---|
| | | | |

## 4. Build and automated tests

- [ ] NuGet restore succeeds from a fresh checkout.
- [ ] Debug build succeeds with no unexplained warnings.
- [ ] Full NUnitLite test suite passes; executed test count is recorded.
- [ ] Release build succeeds with no unexplained warnings.
- [ ] GitHub Actions build check passes on PR #9.
- [ ] CodeQL check passes or has an approved, documented disposition.
- [ ] `git diff --check` passes.
- [ ] Build/tests do not modify tracked source files.

Commands:

```powershell
nuget restore .\PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln
msbuild .\PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln /t:Rebuild /p:Configuration=Debug /p:Platform="Any CPU"
& .\PersonalCloudLibrarySource.Tests\bin\Debug\PersonalCloudLibrarySource.Tests.exe --noheader '--result=TestResult.xml;format=nunit2'
msbuild .\PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln /t:Rebuild /p:Configuration=Release /p:Platform="Any CPU"
git diff --check
git status --short
```

Evidence:

| Date | Environment | Debug | Tests/count | Release | CI URL |
|---|---|---|---|---|---|
| | | | | | |

## 5. Package and official Playnite validation

- [ ] Debug package test passes.
- [ ] Release package test passes.
- [ ] Playnite Toolbox `pack` succeeds for the Release output.
- [ ] Toolbox `verify addon` succeeds.
- [ ] Toolbox `verify installer` succeeds.
- [ ] `extension.yaml`, add-on YAML, and installer YAML parse with a real YAML parser.
- [ ] Package-content inspection passes.
- [ ] Package contains DLL, `extension.yaml`, icon, localization, and required branding assets.
- [ ] Package excludes PDB/source/temp/legacy asset payloads unless a separately approved symbols artifact is used.
- [ ] Packaged extension ID, type, module, version, icon path, and required API version are correct.
- [ ] Package filename is derived from the manifest and is not a stale hard-coded version.
- [ ] Package installs in Playnite without manual archive modification.

Commands:

```powershell
& .\tools\package-extension.ps1
& "$env:PLAYNITE_TOOLBOX" pack .\PersonalCloudLibrarySource\bin\Release .\dist
& "$env:PLAYNITE_TOOLBOX" verify addon .\playnite-addon\addon-database.yaml
& "$env:PLAYNITE_TOOLBOX" verify installer .\playnite-addon\installer.yaml
```

Evidence:

| Date | Toolbox version/path | Debug package | Release package | Addon verify | Installer verify | Inspection log |
|---|---|---|---|---|---|---|
| | | | | | | |

## 6. Install and upgrade qualification

- [ ] Clean Playnite Desktop install succeeds.
- [ ] First-run setup behavior is correct.
- [ ] Upgrade from 0.1.1 succeeds with settings/data preserved.
- [ ] Upgrade from 0.2.0 succeeds with settings/data preserved.
- [ ] Reinstalling/updating does not store generated data in the extension installation directory.
- [ ] Settings migration is sequential and idempotent.
- [ ] Edit/cancel/save behavior preserves or persists exactly the intended values.
- [ ] Extension data, manifests, reports, diagnostics, backups, and cache remain under the designated plugin data directory.

Evidence:

| Date | Playnite version/mode | Starting PCLS | Ending PCLS | Settings/data result | Evidence |
|---|---|---|---|---|---|
| | | Clean | 1.0.0 | | |
| | | 0.1.1 | 1.0.0 | | |
| | | 0.2.0 | 1.0.0 | | |

## 7. Provider qualification

For each provider/environment, test valid import, valid empty manifest, missing/unavailable source, invalid manifest, installed-state detection, install/download where applicable, play, uninstall, restart/recovery, diagnostics, and user feedback.

- [ ] LocalFile testing passes.
- [ ] LocalFolder testing passes.
- [ ] External-drive testing passes, including disconnect/reconnect.
- [ ] Mapped-drive testing passes, including unavailable mapping.
- [ ] NAS/UNC testing passes, including unavailable share.
- [ ] RcloneRemote testing passes with a real configured remote.
- [ ] Rclone prerequisite/setup instructions are accurate.
- [ ] Healthy long rclone transfer is not killed by a total-duration timeout.
- [ ] Stalled/no-progress rclone behavior follows the documented timeout policy.
- [ ] Source failure produces actionable feedback and recovery clears it.

Evidence:

| Date | Provider/environment | Import | State/play | Install | Uninstall | Failure/recovery | Evidence |
|---|---|---|---|---|---|---|---|
| | LocalFile | | | | | | |
| | LocalFolder | | | | | | |
| | External drive | | | | | | |
| | Mapped drive | | | | | | |
| | UNC/NAS | | | | | | |
| | RcloneRemote | | | | | | |

## 8. Transfer lifecycle

- [ ] Transfer queue starts admitted jobs in deterministic order.
- [ ] Concurrent transfers honor configured limits for actual workers.
- [ ] Duplicate active install/retry work is prevented.
- [ ] Progress reporting is accurate and does not flood/freeze the UI.
- [ ] Transfer cancellation works while queued.
- [ ] Transfer cancellation works during local file copy.
- [ ] Transfer cancellation works during local directory copy.
- [ ] Transfer cancellation works during rclone copy.
- [ ] Retry succeeds after a retryable failure/cancellation.
- [ ] Retry failure is observed and reported.
- [ ] Post-transfer verification is enforced according to the approved 1.0 contract.
- [ ] Partial-file cleanup follows the approved contract for success, failure, cancellation, verification failure, and shutdown.
- [ ] Existing final destinations are not silently overwritten.
- [ ] Playnite shutdown during transfer cancels/stops work within the approved bound.
- [ ] No orphan rclone process remains after cancellation, timeout, or shutdown.
- [ ] Installed state changes only after launch readiness is verified.

Evidence:

| Date | Scenario | Provider/file kind | Result | Partial/final state | Evidence |
|---|---|---|---|---|---|
| | | | | | |

## 9. Filesystem and uninstall safety

- [ ] Safe uninstall of a cached file passes.
- [ ] Safe uninstall of a cached directory passes.
- [ ] Drive root deletion is refused.
- [ ] Share root deletion is refused.
- [ ] Cache root deletion is refused.
- [ ] Outside-cache deletion is refused by default.
- [ ] Advanced outside-cache opt-in still refuses roots and reparse traversal.
- [ ] Prefix-collision and `..` traversal paths are refused.
- [ ] Junction/symlink target is refused for recursive deletion.
- [ ] Junction/symlink ancestor is refused for recursive deletion.
- [ ] File/directory intent mismatch is refused.
- [ ] Missing target produces a safe, accurate result.
- [ ] Transfer partial cleanup can delete only an exact queue-owned path.
- [ ] Uninstall failure does not mark the game uninstalled.

Evidence:

| Date | Path fixture | Requested action | Expected | Actual | Evidence |
|---|---|---|---|---|---|
| | | | | | |

## 10. Desktop management experience

- [ ] Setup wizard completes for every supported provider.
- [ ] Setup reminder policy works for new, valid, dismissed, previously completed, disabled, and reminders-disabled states.
- [ ] Automatic startup refresh behaves exactly as labeled.
- [ ] Automatic startup manifest generation behaves exactly as labeled and only for eligible configuration.
- [ ] Both startup actions run once in the documented order.
- [ ] Desktop dashboard opens from top panel, sidebar, and menu where enabled.
- [ ] Dashboard status matches setup/source/verification/transfer state.
- [ ] Transfer queue presentation, cancel, and retry work.
- [ ] Recent activity records completed, failed, and cancelled jobs once.
- [ ] Repeated dashboard open/close does not retain stale view models or duplicate events.
- [ ] Diagnostics and reports generate/open from plugin data.
- [ ] Every notification preference changes behavior as labeled.

Evidence:

| Date | Desktop scenario | Expected | Actual | Evidence |
|---|---|---|---|---|
| | | | | |

## 11. Fullscreen imported-game behavior

No dedicated Fullscreen dashboard or setup wizard is required.

- [ ] Imported remote-only game is visible with correct state.
- [ ] Imported cached game is visible as installed and launch-ready.
- [ ] Play action launches the expected executable with correct working directory/arguments.
- [ ] Install/download action is accessible and succeeds.
- [ ] Cancellation produces useful feedback.
- [ ] Retry/recovery is possible through supported core behavior/feedback.
- [ ] Uninstall/cache removal is accessible and uses the same safety policy.
- [ ] Unsafe uninstall refusal is understandable.
- [ ] Source/manifest/transfer failure produces useful status or notification feedback.
- [ ] Restart preserves imported/installed state correctly.
- [ ] No required core workflow depends on the Desktop dashboard, setup wizard, or custom details window.

Evidence:

| Date | Playnite version | Fullscreen scenario | Result | Evidence |
|---|---|---|---|---|
| | | | | |

## 12. Localization and presentation

- [ ] Localization key uniqueness/reference tests pass.
- [ ] No unapproved literal user-facing XAML string remains.
- [ ] No unapproved hard-coded user-facing C# string remains.
- [ ] Formatting placeholders render correctly.
- [ ] Settings, setup, dashboard, controllers, notifications, and reports were reviewed.
- [ ] Desktop review passes at standard and high DPI.
- [ ] Fullscreen core feedback is readable.
- [ ] Branding is not clipped, stretched, blurred, or shown on an unintended background.

Evidence:

| Reviewer/date | Locale | Mode/DPI | Surfaces | Result/evidence |
|---|---|---|---|---|
| | en_US | | | |

## 13. Documentation and screenshots

- [ ] README matches the final feature matrix.
- [ ] Setup instructions cover every provider.
- [ ] Upgrade instructions cover 0.1.1 and 0.2.0.
- [ ] Troubleshooting covers missing/unavailable sources, invalid manifests, rclone, transfers, diagnostics, and reports.
- [ ] Desktop versus Fullscreen scope is explicit.
- [ ] Cache/deletion safety and advanced option risks are clear.
- [ ] Legal/use boundary is clear and consistent.
- [ ] Naming and author information are consistent.
- [ ] Local documentation link check passes.
- [ ] Remote documentation link check passes or every network-limited result is manually verified.
- [ ] Screenshot review confirms current UI, version-neutral content, correct branding, and no private paths/data.
- [ ] Historical v0.2/v0.3 specs are visibly historical and not treated as live release metadata.
- [ ] GitHub repository description is concise and current.

Evidence:

| Date | Check | Result | Evidence |
|---|---|---|---|
| | | | |

## 14. Final version synchronization and artifact

- [ ] `extension.yaml` says 1.0.0.
- [ ] Assembly version/file version say 1.0.0.0.
- [ ] README current release says 1.0.0.
- [ ] CHANGELOG top release says 1.0.0 with approved notes/date.
- [ ] Add-on Database description reflects verified 1.0 behavior.
- [ ] Installer contains a correct 1.0.0 package entry, required API version, date, and URL.
- [ ] Workflow/package artifact names derive to 1.0.0.
- [ ] Release metadata validator reports no mismatch.
- [ ] Final package was built from the reviewed clean commit.
- [ ] Final package checksum was generated with SHA-256.
- [ ] Published/downloaded package checksum matches the recorded value.
- [ ] Final package was reinstalled and smoke-tested after checksum generation.
- [ ] PR #9 title/body/checklist describe 1.0 hardening accurately.
- [ ] PR #9 leaves draft only after explicit reviewer approval.
- [ ] No automatic merge was enabled.

Final artifact record:

| Source commit | Package filename | Size | SHA-256 | Toolbox result | Install smoke | Recorded by/date |
|---|---|---:|---|---|---|---|
| | | | | | | |

## Release sign-off

| Role | Name | Decision | Date | Notes |
|---|---|---|---|---|
| Product/maintainer | | | | |
| Code review | | | | |
| Desktop qualification | | | | |
| Fullscreen qualification | | | | |
| Release/package verification | | | | |

**Release decision:** ☐ Hold ☐ Approved for 1.0.0 packaging/publishing
