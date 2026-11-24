# PCLS v1.0 directory cache cleanup — read-only assessment, phase A

**Status:** draft safety foundation, not a user-facing feature and **not deletion permission**. Public v0.3.2 remains unchanged.

## Existing behavior

PCLS can cache directory-type game packages, issue V2 SHA-256 inventory receipts after a successful transfer, and compare a later snapshot against those receipts. A fingerprint match is not proof of exclusive ownership, nor protection against filesystem races. The existing `CacheOwnershipAssessment.MayDeleteAutomatically` is always `false`, and recursive directory removal remains disabled.

## Phase A addition

`CacheDirectoryCleanupReadinessService.Inspect(cacheRoot, itemId, expectedDirectory, token)` provides bounded diagnostic classification without changing any file, receipt, cache path, authorization policy, or Playnite metadata.

The result reports one of:

- Unsafe cache path / cache root / linked or escaped target
- Missing directory
- Missing or invalid ownership receipt (legacy/unverified)
- Legacy V1 receipt without inventory
- Receipt destination mismatch
- Changed directory inventory
- Matching V2 inventory **but untrusted and nonauthorizing**
- Inventory unavailable due to filesystem errors
- Cancelled read-only scan

**Invariant: `CanDeleteAutomatically` always returns false.** No call to `File.Delete`, `Directory.Delete`, `SafeCacheDeletionExecutor.Delete`, or another mutation API is reachable from this service. A user-edited or maliciously replaced receipt can still match the current directory because V2 receipts are not authenticated. Never conflate hash agreement with authorization.

## Tests (new, disposable only)

1. Matching V2 receipt does not authorize deletion.
2. Added personal save changes the snapshot; both files remain.
3. Modified installed payload changes the snapshot; payload remains.
4. V1 receipt stays legacy/untrusted.
5. Preexisting cache without receipt is not adopted.
6. Different destination from the one in the receipt fails.
7. Cache root and outside-cache destination are rejected and untouched.
8. Missing game directory does not affect a sibling file.
9. Pre-cancelled review leaves game and V2 receipt intact.

## Additional read-only per-file observations

The optional `CacheTreeInventoryScanner.ScanDetailed(cacheRoot, directory, token)` now returns bounded, sorted **relative** file and subdirectory entries with per-file length and SHA-256. The ordinary `Scan` method and the existing V2 receipt fingerprint format remain unchanged, without the extra in-memory entry list.

`CacheDirectoryInventoryDiffService.Compare(earlierDetailedScan, currentDetailedScan)` describes added, missing, modified and unchanged observed entries. A simple use case is to notice that a personal save appeared since an earlier in-memory snapshot. Both observations are **unauthenticated** and remain subject to filesystem races; neither establishes which files PCLS owns. The diff service's `CanDeleteAutomatically` is permanently false.

No per-file baseline is persisted yet, and a legacy V2 receipt contains only the aggregate fingerprint, not the per-file history needed for a historical diff. Scans with missing details are rejected rather than silently treated as an empty file list.

## Explicitly excluded

- No recursive or file-by-file directory deletion.
- No UI button, setting, notification, status promotion, or Playnite integration in this PR.
- No persistent review grant; no auto-adoption of legacy caches.
- No cryptographic receipt authentication, OS file-handle pinning, or TOCTOU resolution.
- No guarantee for junction/symlink swaps concurrent with inspection.

## Next phases (separately reviewed)

1. Specify immutable file-granular ownership records, content identity and a no-adoption rule for existing installs.
2. Implement a **read-only** per-file cleanup plan showing what is owned, changed or unknown; never silently treat unknown files as disposable.
3. Adversarial Windows tests for link swaps, user mutations, shared roots, stale receipts and concurrent process access.
4. Only after that, review a race-conscious, narrowly scoped file-by-file deletion strategy with clear stop/recovery behavior and an owner approval gate.
5. If the safety case fails, keep directory removal disabled even in v1.0.

Issue: [#13](https://github.com/KurtusCobain/PersonalCloudLibrarySource/issues/13). Public safety guide: [known limits](https://github.com/KurtusCobain/PersonalCloudLibrarySource/blob/main/docs/known-limits.md).
