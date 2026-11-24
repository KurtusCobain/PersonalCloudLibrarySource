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
