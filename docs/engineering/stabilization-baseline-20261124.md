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
