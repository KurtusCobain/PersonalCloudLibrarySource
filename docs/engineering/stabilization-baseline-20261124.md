# Stabilization baseline — 2026-11-24

Status: isolated engineering candidate only. No authorization to merge, publish, modify settings, delete branches or change the public feed.

## Preserved references

| Branch | Original HEAD |
|---|---|
| main | d414e2f19375d3ba4a2142dd70a10aad323843d0 |
| audit/release-hardening-20261030 | 95db6786719314b188669554d6f9b6fdc67f8d2d |
| backup/pre-kubuntu-20260818 | db8f14d48c4a1bb820a699b61aee8c9232dcb8a1 |
| docs/v032-public-polish-20261101 | c051f3dfe63a1afb5581621ec4d08377df186024 |
| feature/user-friendly-dashboard-cleanup | 1c59e0f4e344fc419c5fc7bb913ec09989d0632c |
| feature/v1-combined-integration-20261103 | e82a27862cb2cce10b1b635735c5de0467d978d7 |
| feature/v1-multisource-identity-foundation-20261101 | 56664f96cb21625258b52aa4056240253b04c296 |
| feature/v1-package-discovery-foundation-20261101 | 0c129df3cc60905cdf5b3c0438181efe1fed78e7 |
| fix/v033-implicit-file-size-20261101 | ce338ff9125740b5dd0083fae6b4118df431a7bc |
| safety/v1-readonly-cleanup-review-20261101 | 552d9a61f3f2df3cbb240b53db5c1141a6e9aeef |

## Consolidation results

PR #21's head is an ancestor of PR #23. Do not merge it again. PR #22 diverges topologically: of 40 changed paths compared to main, 33 match the integration candidate exactly and seven have integration edits. Review the seven differences before closing either PR. PR #11: ten nonoverlapping files incorporated in this commit; external-link-checks.md overlaps and is pending manual reconciliation. PR #15: six nonoverlapping and two overlapping paths; preserve both the size correction and source-specific identity. PR #16: six nonoverlapping and three overlapping paths; preserve existing archive changes and keep review nonauthorizing. Historical backup contains unique research and obsolete listing/feed edits; do not restore old feed. No branch deleted.

## Governance proposal (not applied)

Owner approval required for repository settings: protect main with PRs and required checks, block force pushes/deletions, protect version tags, enable private vulnerability reports, review immutable releases and release environment approval. Keep stable feed and package versions unchanged. Reusable release authorization, checksums, SBOM and provenance remain future work. Five workflows now pin Action revisions and checkout does not persist credentials; normal build is read-only. A successful previous CodeQL job is not clearance of all findings. Verify exact new HEAD runs before promotion.

## Fork triage

C-H-Rm/Cloud-Storage-Integration HEAD 9e2e3570fd9599ec2b354ee6417cf31e1ca26490; comparison against common ancestor identifies 60 commits. Reviewed commit subjects for all 60, not full patch contents. Prioritize persistent history, transfer cancellation/retry, diagnostics redaction, package receipts and explicit consent patterns. Compare existing upstream timeout and cache policies before porting. Defer remote-write mod features to a separate consent and security review; retain upstream UUID, assembly and distribution identity. Do not remove archive entry limits or adopt fixed high concurrency without benchmarks. Preserve MIT notices and contributor credit for imported code.

## Qualification gates

1. Complete per-file static audit and branch conflict resolution.
2. Verify archive, filesystem, profile identity, cancellation and recovery invariants with disposable fixtures.
3. Measure performance before optimizing; no claims based on file counts.
4. Run Windows CI, inspect all findings and package contents at exact candidate HEAD.
5. Independent review and native Playnite Desktop/Fullscreen migration/acceptance before release. No real user data in destructive tests.

This is an engineering ledger, not a claim of release readiness.

## Subsequent stabilization checkpoint

Windows build, package inspection and archive compatibility passed at 04c0d7ab (781 passed, 0 failed, 2 opt-in real-rclone tests skipped). PR #11, #15 and #16 changes have been reconciled into the isolated candidate; verify exact latest HEAD before promotion. The multi-source uninstall factory now requires receipt storage and execution checks both original and scoped game identities. Permanent ZIP cleanup is withheld from the Playnite host pending handle-bound filesystem identity and race qualification; reversible quarantine, restoration and isolated cleanup fixtures remain intact. No new deletion authority is granted. The ZIP32 compiler warning was removed without relaxing its existing format bounds or central directory byte cap.

## Inventory scan hardening

The read-only scanner now reuses a single 1 MiB buffer per scan, rather than allocating one per file. The stream's actual length must match the initial stat, reads are bounded to that length and growth/truncation is refused. The existing fingerprint format remains unchanged; mixed-size and empty-file regression fixtures independently compute the legacy fingerprint. This reduces allocation pressure by construction but is not a measured performance benchmark or proof against all filesystem races.

## Execution-bound authority and file-only removal

The multi-source factory binds controller invocation to the currently published and configuration-validated plan and the original Playnite identity. Host-created controllers require a live plan validator. Disabled settings and expired plans refuse new actions; already-running transfers are not retroactively cancelled. Legacy single-source manifest freshness and the gap between a check and filesystem mutation remain qualification items. File-only uninstall now uses an explicit nonrecursive executor path, refusing directory substitution and never invoking the directory callback. This closes a type-confusion path without claiming full handle-bound filesystem safety.

## Fork selective patch review — first batch

Compared the content/changed-file lists of eight CH-Rm commits (8ef45f9b, 6630d767, ce15ba47, 1e41ff13, 78fbe708, 99f626a2, ceb8e040, 1ad697ff). Adapted the Windows path namespace/ancestor protection pattern from ce15ba47 into the upstream policy, with new tests and attribution to the MIT-licensed fork. The progress-aware timeout already exists upstream but monotonic elapsed time and meaningful-progress detection warrant separate review. Structured export without local paths is a useful candidate. Do not import fixed 32-way concurrency or remove archive count limits. Fast listing is a provider-specific benchmark candidate. First-upload destination revalidation is useful but not a distributed lock; remote writes remain out of this integration. The other 52 unique commit patches still need review; this is not a completed fork audit.

## Rclone process lifecycle checkpoint

Verified 13c87a33: 792 passed, zero failures/compiler warnings, two opt-in skips; all three CI workflows green. Adapted the CH-Rm progress-watchdog concept (8ef45f9b) using monotonic elapsed time and advancing byte/total observations. Repeated log lines no longer extend inactivity indefinitely. Listing/check-only activity and provider-specific timeout behavior require real-rclone qualification. Process setup exceptions attempt bounded stop; cancellation before launch does not spawn a process. Output drain is bounded. Unconfirmed exit preserves job staging and is a failure, not clean cancellation. Raw provider stderr is not propagated to manifest error or transfer history. This does not yet bound the framework's individual line buffers, guarantee descendant termination, or sanitize every diagnostic producer.

## Bounded manifest reading

Replaced line-based manifest capture with an 8 Ki-character chunk reader that consumes at most the configured character limit plus one. Stderr is drained without retention; process exit and pipe EOF share the bounded retrieval deadline. Tests exercise exact preservation, zero budget and a synthetic million-character line without allocating it. This is a managed reader bound, not a limit on OS pipe buffers or a general process-tree containment proof.

## Queue retry authorization and recovery quarantine

Verified e997bf7b: 797 passed, zero failures/compiler warnings, two opt-in skips; Windows packaging and archive workflows green. Queue captures independent transfer settings, refuses changed provider/cache identity and disabled downloads, and does not combine global multi-source settings with old scoped transfer paths. Unconfirmed subprocess exit is explicitly marked as requiring manual recovery. The queue blocks retry and new overlapping destination/game attempts for the session; this is not durable process containment or restart recovery. Job rows suppress retry for quarantined attempts. Manager Changed observer exceptions cannot interrupt committed state mutations. Legacy manifest content drift, cross-session recovery records and bounded history remain open audit items.

## Staging collision preservation

Both transfer adapters now refuse pre-existing staging paths instead of deleting them at startup. Local file cleanup only becomes eligible after successful exclusive file creation; local directory cleanup becomes eligible after creation and checks descendant links/size bounds. Rclone cleanup is withheld on runner exceptions with unconfirmed process exit. Local directory verification uses the existing nonrecursive safe traversal rather than a recursive GetFiles call. These are conservative preflight and cleanup improvements, not handle-bound race closure; directory creation and subsequent path replacement remain qualification items.

## Shared transfer verification and bounded traversal

Verified 262c377c: 803 passed, zero failures/compiler warnings, two opt-in skips; Windows packaging and archive compatibility green. Consolidated deletion descendant inspection, local source discovery, local/rclone staging size verification and final destination verification around one bounded top-down walker. Verification no longer scans once for links and then recursively enumerates unchecked descendants. Directory copies reuse one application copy buffer for all files. Rclone cleanup refuses a staging type substitution and flags cleanup failure for session quarantine. The walker limits entries to 150,000, observes cancellation in prepublication verification, and uses checked byte totals. This is pathname-level defense; not an atomic filesystem snapshot or cryptographic integrity verification. No runtime throughput benchmark has been claimed.

## Indexed transfer lookups and destination reservations

The manager now indexes jobs by ID and per-game attempt list while retaining original history order and retry ancestry. ID lookups avoid linear history scans; game lookups avoid allocating/sorting every repository-wide attempt. Aggregate byte totals fail to indeterminate on overflow. Completed and recovery-quarantined requests release their retained configuration snapshot, not their history or completion result. The queue refuses overlapping active/queued cache reservations across different games. Persistent history, cross-session recovery quarantine and a bounded retention policy remain open; no historical record was silently discarded. Complexity improvements are structural, not measured throughput claims.

## Audit sequencing and dashboard update consolidation

Owner direction: defer all further CH-Rm research, patch review and selective integration until PCLS's own repository remediation is complete; preserve prior work and do not touch the fork in the interim. Verified 246174c8: 808 tests passed, zero failures or compiler warnings, two opt-in skips; Windows packaging and archive compatibility passed. Dashboard queue, recent activity and state notifications use independent acknowledged refresh gates rather than rebuilding both lists per activity or byte update. The gate releases before the callback reads state so a mid-refresh event can schedule a follow-up. Manager lifecycle revision distinguishes byte-only progress from changes requiring full library status refresh; ordinary progress updates only update sidebar progress and the queue view. The activity burst fixture now requires one submission for 55 records, with the latest 50 preserved. Native UI responsiveness and end-to-end throughput remain to be measured.

## Settings transaction and configuration predicate cleanup

SettingsEditSession publishes its committed snapshot only after the persistence callback succeeds. Failed saves leave the session active and allow correction or cancellation without publishing the failed candidate. The current-version invalid transfer concurrency normalization now reports a migration repair, allowing the normalized value to be persisted once rather than re-normalized at every load. Dashboard setup completeness and legacy setup migration share one source-configuration presence predicate while preserving the deliberate difference for disabled but previously configured libraries. No schema version change or migration removal. This is a transaction-state repair; physical persistence atomicity, draft-versus-runtime consumers and future-schema handling remain separate review items.
