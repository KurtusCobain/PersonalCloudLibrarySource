# PCLS Final Polish & Independent Review Gate

**Status:** working draft for the unmerged release-hardening PR #10, not release approval.
**Candidate:** 0.3.2 implementation / proposed 1.0 milestone. Public users remain on v0.2.0.
**Rule:** no changes to `main`, publishing feed, or user's real game/cloud files from this plan.

## Product promise (every reviewer should use)

PCLS catalogs game content supplied and legally controlled by the user across local files, removable drives, NAS/UNC shares, or configured rclone storage. A user can import metadata, **Install** an authorized local cached copy on demand, launch locally, and remove the cached copy without unintentionally changing the original collection. PCLS is not a store, content provider, streaming service, or emulator manager.

First-run experience should read: **Choose a source → preview results → configure cache → verify → Update Game Library → install selected game → play → remove cached copy safely.** Never claim a preview has imported games, or that existence/size checks are cryptographic verification.

## Before an owner laptop test: code review

| Area | Acceptance | Primary code |
|---|---|---|
| Preview and Finish lifecycle | Safe return/back/redo/cancel; bounded reading; no preview source writes; skipped reasons visible; finishing local/NAS setup does not scan on the UI thread, and cancellation does not save settings | `Setup/SetupWizardView.xaml.cs`, `ManifestGenerationService.cs` |
| Install lifecycle | Playnite controller returns promptly; exactly one terminal signal; no stale Game mutation after disposal, shutdown or cancel; cancellation race covered | `RcloneInstallController.cs`, `Transfers/TransferQueueService.cs` |
| Source health | Disconnected NAS/removable/rclone source gives understandable error; never treats it as an intentionally empty library or deletes cached games | `Services/ManifestLoader.cs`, import/report services |
| Filesystem safety | No traversal, reparse escape, source overwrite, cache-root deletion, or partial ownership confusion; outside-cache deletion stays high-risk and off by default | `Services/PathResolvers.cs`, `Services/SafeCacheDeletionPolicy.cs`, transfer adapters |
| Large library | Manifest bounded by input and item count; scan bounded; no unbounded logs, process output, or recursion through junctions | manifest generation/parser/loader and rclone reader |
| Upgrade preservation | Stable extension ID and settings serialization, game IDs/cache unchanged; never silently migrate/move source files | settings migration + `docs/upgrades.md` |
| Code health | Every retained control has runtime meaning; user-visible errors actionable; avoid unnecessary dependencies, generated assets and duplicate wrappers | WPF views, settings, projects, package and README |

No new auto-eviction, content scanning outside the chosen source, save/mod synchronization, GPL/AGPL code copying, credential storage, or real-game fixtures before 1.0.

## Final screenshot & brand acceptance sheet

**Do not fabricate screenshots and label them as genuine.** Obtain final captures from a disposable Playnite test profile on Windows after UI acceptance. Replace aliases with distinct, truthful images. Do not screenshot real ROMcade names, absolute user folders, tokens, mounted remotes, or private drives.

| Asset / shot | What it must prove | Acceptance |
|---|---|---|
| Playnite listing cover / icon | PCLS identity readable at small size, transparent icon, approved wide brand | Native asset renders cleanly without checkerboard or clipping |
| Source selection | Local, external/NAS, manifest, rclone choices are understandable | 100% and 150% DPI; no overlap or clipped labels |
| **Real** preview | Actual disposable detected titles; skipped details and re-scan/stop controls | File inventory matches fixtures; no private paths |
| Cache/setup summary | Source vs local cache vs catalog-only clearly distinct | Shows safe copy promise and advanced-risk wording |
| Dashboard | Valid current source health, cache count and next action | Live values match the disposable library |
| Transfer queue | Progress and pending/completed/retry/cancel statuses | Slow fixture transfer; state and timestamp meaningful |
| Playnite library | Imported example entry + Install / Play / Uninstall actions | Desktop and Fullscreen equivalents where supported |
| Troubleshooting | One authentic, actionable offline/missing source state | Shows recovery without treating source as empty |

**Existing screenshot alias caution:** `docs/images/pcls-workflow.png` currently contains the same blob as `pcls-dashboard-overview.png`; `pcls-settings-cache-uninstall.png` shares the `pcls-settings-cache-safety.png` blob. They temporarily restore published links; they are not independent final shots.

Keep the three approved runtime brand assets until the owner visually approves replacements. Old, large concept art must be inventoried for actual references before pruning; historical Git privacy remains a separate problem.

## External reviewer handoff (copy to Muse, Grok, Claude, Codex or independent GPT)

> Independently audit `KurtusCobain/PersonalCloudLibrarySource`, **draft PR #10 compared against main and public v0.2.0**. The goal is a dependable, easy-to-use, MIT-licensed Playnite game-library caching extension for the open-source community. Read the *current exact PR head*, not an old main-only snapshot. Do not change, publish, merge, or delete anything. Inspect production C#, XAML, tests, docs, package metadata, and official SDK expectations.
>
> Rank every finding Blocker/High/Medium/Low and report **exact file + line, failure mechanism, reproducible test, smallest safe patch and expected behavior**. Explicitly challenge cancellation/disposal/UI thread behavior, import-source disappearance, path traversal and reparse points, overbroad external uninstall permissions, duplicate IDs, partial commits, full drive/slow NAS/rclone, settings migration, localization, accessibility and package consistency. Distinguish code-confirmed failures from host-only hypotheses. Mark already-fixed prior-review issues as resolved when the current branch demonstrates it.
>
> Deliver **(1) new confirmed defects, (2) risky assumptions, (3) missing tests, (4) unclear UI/settings text, (5) README/listing/screenshots mismatches, (6) changes to defer after release**. Do not copy GPL/AGPL competitor implementations into this MIT repository. No arbitrary new features or rewrite recommendations unless justified by a measurable failure.

## Roles for cross-model review

- **Muse:** first-time user journey, UX comprehension, visual consistency, settings explanations and screenshots.
- **Claude:** C# architecture, cancellation, ownership/disposal, race conditions, WPF threading and backwards compatibility.
- **Grok:** competitor gaps, rclone/NAS disconnects, real-world Playnite user expectations and confusing first-run workflows.
- **Codex:** adversarial implementation review, tests, build artifacts, clean-checkout packaging and minimal corrective patches.
- **Independent GPT:** reconcile disagreements only against evidence, prioritize a bounded release gate, catch hidden regressions.

None of these reviews counts as validation merely because it was produced; every critical claim must be confirmed in source, tests or the Windows host before a release decision.

## Release signoff — all mandatory

1. Latest exact reviewed commit: Windows Debug+Release build, automated tests, package inspection **green**.
2. Installed Playnite Desktop smoke and Slow/Cancel/Retry/Dispose, Offline NAS/drive and real private rclone test with **disposable** content.
3. Genuine v0.2.0 → candidate upgrade with prior data/settings safely preserved, plus Fullscreen and high-DPI tests.
4. Updated truthful, privacy-clean screenshots, final icon/logo approval, accessible UI and documentation.
5. Official Toolbox addon and installer verification on final *same* package; recorded SHA-256 and content review.
6. Explicit maintainer selection of publish version (0.3.2 or 1.0.0), separate approval to merge and publish; installer feed untouched until then.

**Fail closed:** any destructive data risk, corrupted upgrade, unexplained Playnite freeze, invalid package, missing core action, or disclosure of real library details means HOLD.
