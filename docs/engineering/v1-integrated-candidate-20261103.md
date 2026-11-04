# PCLS v1.0 combined integration candidate — 2026-11-03

**Status:** Draft integration-only branch. Not approved for distribution, merging or installation.

## Inputs

- Draft PR #21, package pipeline HEAD `0c129df3cc60905cdf5b3c0438181efe1fed78e7`.
- Draft PR #22, multi-source pipeline HEAD `56664f96cb21625258b52aa4056240253b04c296`.
- Neither source PR is merged or altered. This integration branch is a separate candidate.

## Integrated boundaries

- The multi-source profiles settings UI, source-specific catalog/import plan, per-profile source identity, live configuration fingerprint, health checks, display snapshots and source-bound transfer action factory now coexist with PR #21's ZIP inspection, explicit extraction, immutable ownership receipts, launcher approvals, pre-launch verification, reversible quarantine and double-confirmed permanent cleanup.
- Existing single-source behavior remains the default because `EnableMultiSourceProfiles` defaults to `false`. The successful single-source import still applies verified ZIP launcher approvals before `ImportExecutionPolicy.Complete(outcome)`; unsuccessful import remains a typed failure with no partial publishing.
- Source-bound `GetInstallActions` and `GetUninstallActions` use the frozen multi-source plan with live configuration rechecks. Source-scoped uninstall also checks the managed ZIP footprint and passes ownership records to the legacy controller's execution-time guard.
- **Deliberate fail-closed boundary:** Source-bound ZIP approval/recovery and game-level legacy context menus are **not yet integrated for multi-source IDs**. Multi-source mode shows only the safe Dashboard context action; it does not reuse single-source ZIP approval files, inventory, repair or deletion permissions. The managed ZIP launch gate expressly denies legacy approvals when multi-source profiles are enabled. This is a release gate, not missing authorization to be inferred.
- A ZIP archive's original source and cached extraction are separate from any multi-source route. There is no implicit cross-provider package migration.
- The destructive permanent cleanup path remains draft and needs adversarial review, filesystem race mitigation, performance testing and real Windows Playnite acceptance before any public exposure.

## Next validation gates

1. Windows Debug/Release build and full NUnit suite on **this exact integrated HEAD**.
2. Independent adversarial review of route/provenance separation; ZIP importer and mutable-save policy; cleanup race and crash recovery.
3. Safe `7z`/`RAR` backend evaluation: current plugin targets .NET Framework 4.6.2, so do not silently add a modern archive library requiring .NET Framework 4.8. Never shell out to unspecified archive binaries or treat a file signature as validated content. Full 7z/RAR extraction support is **not yet implemented**.
4. One consolidated native Windows Playnite acceptance with valid, corrupted, oversized and modified synthetic archives, source-profile permutations, cancellation and restart. No real user game files should be used for destructive tests.

## Release constraints

PR #21 and PR #22 remain draft/unmerged. Integration PR also stays draft; no main merge, release, tag, installer-feed mutation or public 1.0 claim.
