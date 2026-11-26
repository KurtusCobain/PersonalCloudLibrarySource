# PCLS independent review handoff — 2026-11-25

**Status: pre-review code freeze; Windows and archive passed, exact CodeQL pending at this entry. Not a release authorization or manual acceptance.**

Repository: `KurtusCobain/PersonalCloudLibrarySource`. Branch: `audit/pcls-stabilization-20261124`.

- **Frozen code revision:** [`b090230dea6c9d5832dee3e222b579c7e84dd154`](https://github.com/KurtusCobain/PersonalCloudLibrarySource/tree/b090230dea6c9d5832dee3e222b579c7e84dd154). Review this exact tree, not a moving branch tip.
- Starting integration ancestor: `e82a27862cb2cce10b1b635735c5de0467d978d7`.
- Public main baseline: `d414e2f19375d3ba4a2142dd70a10aad323843d0`. Published v0.3.2 and installer feed remain unchanged.
- 123 stabilization commits from combined integration to the proposed code freeze; 146 changed paths in that comparison. Documentation-only handoff commits may follow; do not mix revisions.
- [Engineering history](stabilization-baseline-20261124.md) and [qualification register](stabilization-qualification-20261124.md).
- [Exact code Windows run](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45732477687), [archive compatibility](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45732477526), [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45732477534). Windows and archive passed. CodeQL pending at this entry; check its exact conclusion before review.
- Frozen code Windows checkpoint `b090230d`: 954 standard tests passed, 2 standard opt-in skips separately executed with pinned rclone v1.76.0 (2/2 passed); zero failures/warnings. Linked-output CLI regression passed. Larger mixed-tree qualification 1,024 files / 70 MiB in 8.414 s at this revision; previous hosted runs 8.276 s and 10.327 s. Synthetic correctness/timing observations, not actual NAS throughput.
- Prior fully verified code checkpoint `43729f39`: 947 standard + 2 real-rclone, all three workflows passed.

## Scope and constraints

Read-only independent review. No main merges, tags, publication, settings changes, branch deletion, live cloud data, real game libraries, or code edits by reviewers. CH-Rm/fork review is deliberately deferred; do not access that repository during this review. Keep permanent ZIP cleanup and recursive directory uninstall disabled, preserve unresolved transfer markers and staging, no automatic recovery release. The shipping path remains ZIP-only for managed extraction; isolated archive decoder evidence does not qualify RAR/7z installation. Do not approve outside-cache deletion, remote writes, or execution of unknown packages.

## Review deliverables

Both reviewers independently examine the exact same code SHA. Start by checking the actual Git commit, the changed-file inventory, previous issue/PR history, and exact CI logs (not just workflow badges). Return a prioritized findings table with severity BLOCKER/HIGH/MEDIUM/LOW, confidence, exact file/line/commit, violated invariant, reproduction or concrete failure mechanism, minimal correction, regression test and host acceptance requirement. Separate proved issues, plausible risks, design trade-offs, and test-required hypotheses. Include any previously overlooked work, dead code/obsolete compatibility layers, duplication, performance or accessibility findings. Do not manufacture findings to meet a quota. State an independent recommendation: READY FOR DISPOSABLE HOST ACCEPTANCE / NOT READY. A passed workflow is not absence-of-vulnerabilities evidence.

### Claude — correctness, architecture and adversarial security

1. Review all production C#/XAML and relevant scripts, particularly transfer queue ownership, cancellation/disposal, completion claim, recovery journal integrity, process Job Object launch/drain/kill, native handle identity/rename/disposition, rollback, local/Rclone/ZIP staging and receipt boundaries.
2. Confirm new record I/O has strict byte and UTF-8 bounds. Evaluate remaining replacement windows in SafeFileWriteService and SourceProfilesFileStore, SourceCatalogSnapshotStore and SourceCatalogDisplaySnapshotStore. Existing File.Replace calls and backup paths are not identity-atomic. Inspect the persistent lock namespace and cooperation coverage.
3. Evaluate create-to-open, ancestor-path substitution, reparse/hardlink, cross-process interleavings, manifest and settings freshness, source/game identity routing, launcher approvals, recovery move membership, and untrusted cleanup manifests/intents. Review the withdrawn relative-parent rename experiment and its recorded 125-failure regression, not just the current implementation.
4. Verify semantic branch preservation, explicit compile includes, unused-member candidates (lexical audit is not semantic reachability), SDK API correctness, config migration, malformed JSON/ZIP budgets, invariant enforcement at every authority boundary and test coverage quality.

### Grok — real-world compatibility, operations, performance and usability

1. Examine Desktop/Fullscreen lifecycle, actual Playnite API semantics, local/external/NAS/UNC/rclone remotes, disconnect/reconnect and stale state, source-profile isolation, expected working directories, installed size, cache file-only uninstall, and behavior when process/host terminates.
2. Review usability of recovery diagnostics, preservation and restart behavior; note that read-only recovery inspection does not authorize release. Verify no hidden automatic deletion or unrequested execution, and no misleading success when manual attention is required.
3. Inspect manifest generator parity (PowerShell and C#), linked source/output policy, bounded listing and memory, package validation, provenance, pinned workflows, release authorization, public docs versus candidate and real provider compatibility. CLI process handling is not the same whole-job containment as the native plugin.
4. Assess throughput claims using actual evidence: synthetic queue/index and preview measurements plus 256/1024-file local workloads, not extrapolated to actual NAS speed or complete app startup. Identify bottlenecks only with a reproducible measurement plan.
5. Check keyboard, high DPI, theme, dialog clarity, migration/support docs and likely manual acceptance failures. Visual claims require actual screenshots.

Optional Nova review: first-run workflow, plain-language UX, privacy/support and release messaging. No reviewer has been contacted by this handoff.

## Known limitations to challenge explicitly

| Risk or incomplete gate | Current disposition |
|---|---|
| Complete Windows ancestor-relative namespace pinning and atomic directory membership | Not established; native rename uses exact source handle and no-replace sibling name; source and root identities reobserved. |
| Private metadata replacement | New create-only records use verified handles. Existing profile/catalog and generated output replacement uses File.Replace with backup; final path substitution remains possible. |
| Cross-process transfer and ZIP coordination | Participating writers share journal/lifecycle locks; nonparticipating direct callers, external writers and normal game process lifecycle are not globally serialized. |
| Unresolved marker release / crash and power loss | Read-only review; origin-local completion only; preserve ambiguous evidence. No manual release UI or power-loss certification. |
| Permanent cleanup and directory uninstall | Host flags remain disabled. Internal legacy executor and cleanup fixtures are not user authority. |
| Local source/archive and native filesystem compatibility | Same archive stream qualified on hosted Windows; Windows native IDs and no-follow checks may refuse unsupported volume/SMB cases. Test mapped/NAS compatibility separately. |
| Experimental multi-source and cached install metadata | Native Playnite update/launch/uninstall, migration and stale/disconnected behavior still need owner acceptance. |
| Full semantic audit | Structural inventory of 334 tracked files and 119 production C# plus targeted semantic reviews; no exhaustive dead-code proof, no claim every branch change is semantically identical. |
| Official Toolbox, actual GUI, real providers, and release provenance | Native acceptance deferred until review findings resolved. Current public artifacts unchanged. |

## Acceptance sequence (do not run on a personal library)

After both independent reviews, reconcile findings in a separate correction pass, rerun exact CI and identify the corrected frozen revision. Use the [host acceptance evidence checklist](../tests/stabilization-host-acceptance-20261125.md). Then owner performs a consolidated disposable Windows Playnite campaign per [existing acceptance plan](v1-code-complete-review-acceptance.md):

1. Start with a new portable or disposable Playnite instance, fixture source/cache roots, and backup; never install this development package into the regular profile. Candidate still carries the stable extension ID and v0.3.2 version, so it can replace a real installation if misused.
2. Restore/build Debug, run NUnitLite and PowerShell contracts, build the exact Release package, record its SHA-256, and run official Toolbox separately. Missing Toolbox is BLOCKED, not PASS. Use documented commands in [DEVELOPMENT.md](../../DEVELOPMENT.md).
3. Test first-run, import, refresh, setup cancellation and migration from both public v0.2.0 and v0.3.2 on isolated copies. Verify game IDs, settings, installed size, launch/working directory, source and user-save hashes.
4. Desktop and Fullscreen at 100% and 150% DPI, keyboard/controller, dark/light themes, queue progress, cancellation/retry/shutdown, local file/directory, disconnected source, corrupted/oversized manifest, low disk, receipt conflicts, ZIP keep/extract/approve/revoke/quarantine/restore and unresolved transfer review. No permanent cleanup action or automatic directory removal.
5. Use one disposable UNC/NAS fixture and separately isolated real-rclone configuration, not personal credentials. Record version, config type, timings, exact result, screenshots and logs with secrets/private paths redacted.
6. PASS/FAIL/BLOCKED per case, exact candidate and package digest, stop on possible data loss or unexplained freeze. Return evidence to owner and reviewers. CH-Rm selective review remains a separate deferred gate; any eventual imported changes require regression and re-review before final acceptance. No release without explicit authorization.
