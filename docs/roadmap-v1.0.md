# PCLS roadmap: v0.3.2 → v1.0

Updated November 1, 2026. **This is a proposed engineering plan**, not a statement that unimplemented features exist or a committed release schedule. PCLS v0.3.2 is publicly available. See [release status](release-status.md) and [known limits](known-limits.md).

## Product promise

> A dependable Playnite GameLibrary extension that catalogs user-controlled local, external, NAS and rclone libraries; copies selected games into a local cache; launches them through normal Playnite controls; and never silently damages original content or user-added files.

PCLS is **not** a ROM provider, store, emulator frontend, game streaming product, save-sync service or blanket cloud downloader.

## Next public patch: v0.3.3 (scope proposal)

| Priority | Task | Measurable acceptance |
|---|---|---|
| P0 documentation | Update official upstream Playnite Addon Database v0.2.0 description | Upstream record states current v0.3.2, correct permissions/boundaries and points to existing installer URL; no unverified feature claims |
| P0 release visuals | Replace old alias/duplicate listing pictures with 3–6 genuine privacy-clean captures | Every image depicts actual Playnite, correct state, readable at listing size and no real user paths; source capture aspect ratio preserved or padding/cropping separately approved; public URLs load |
| P1 bug | Correct shared-cache install-size accounting | File-item size reports only its owned file; directory-item size scoped to its own cache subtree where safe; multi-game shared-cache fixture does not attribute unrelated bytes to a game |
| P1 docs | Reconcile README, upgrades, known limits, final-polish gate and upstream listing | No 'draft PR #10', 'latest v0.2.0', 'Windows acceptance pending' as active status; historical evidence archived, not erased |
| P1 feedback | Triage crashes and installation defects on a dedicated 0.3.x milestone | Reproduction + severity for each report; no silent behavior changes to already published binaries |

Nonsecurity polish can use normal PR + CI. **Fix any verified data-loss vulnerability immediately instead of holding for a scheduled version.**

## The path to 1.0: prioritized engineering tracks

### Track A — cache ownership, uninstall safety and recovery (P0)

Current state: default file-only cache deletion is guarded; recursive directory-cache deletion remains disabled. V1/V2 receipts and bounded SHA-256 inventories exist but are **not** a cryptographic ownership mechanism or automatic deletion authorization.

1. Define **file-granular ownership** with a per-game cache root and verifiable per-file provenance. Preserve saves/mods/configuration that appear after installation, and refuse cleanup if ownership is ambiguous.
2. Explicitly model pre-existing caches, read-only source paths, legacy manifests, junctions/reparse points, nested links, hardlinks, symlinks, overlapping game roots, moved mounts and stale receipts.
3. Exercise full lifecycle: normal install, cancel, retry, shutdown, interrupted receipt write, power loss, out-of-space, partially copied folder, manual user mutation, and reinstall.
4. Use an isolated, nonrecursive file-by-file removal plan or an entirely separate risk-reviewed recursive strategy; validate identity/containment as close to mutation as possible. Never remove user-added files automatically.
5. Accept a **1.0 with directory uninstall still disabled** if a reviewed deletion contract cannot be proven safe. Reliability is more important than feature parity.

Acceptance: independently reviewed threat model; deterministic fixture tests and installed Windows trials on deliberately modified test folders; no observed deletion outside precisely owned disposable cache content.

### Track B — real storage and faults (P0)

Current state: local-only installed Playnite and rclone `:local:` tests passed. Real Google Drive/OneDrive/Dropbox/NAS integrations are still environmental dependencies, not validated support matrices.

- Test a disposable UNC/NAS share with disconnect/reconnect, share permissions, latency, canceled copy, source unavailable while cached game remains playable, and a second Windows session.
- Test one configured rclone remote using a **throwaway, independent account or sandbox** with bounded files. Include expired credentials, 429/throttling, timeout and reconnect.
- Document what remains untested for other providers. No personal ROM collection, credentials, saves, or production cloud folder in fixtures.

Acceptance: public-safe logs, no source modification, no destructive permissions, measured cancel/retry state, and clear errors and remediation steps.

### Track C — transfer and filesystem robustness (P1)

- Validate queue state and progress against real user-visible transfer stages; ensure cancellation races cannot publish a half-finished final file or leave orphan staging content.
- Exercise low disk space, permission denial, large file, empty directory, Windows long paths, AV file locking, app close during transfer, and corrupt/missing manifest.
- Confirm cache accounting by item rather than shared-root and show free space truthfully. Avoid scanning entire libraries on every UI refresh.
- Profile representative 1k/10k+ item disposable catalogs under realistic local disk and delayed network conditions; bound time, memory and UI blocking.

Acceptance: reproducible faults with stable terminal state, protected originals, useful diagnostics and explicitly recorded limits.

### Track D — polished and accessible Playnite journey (P1)

- Validate the first-run sequence: choose source → preview → choose cache → verify → import → cache a selected game → play → safely remove eligible content.
- Test Desktop settings and game actions at 100% and 150% Windows scale, keyboard Tab/Enter/Escape, screen-reader labeling where supported, focus visibility, light/dark themes, and recovery from validation errors.
- Test standard game Install/Play/Uninstall controllers in Fullscreen; **do not invent** a dedicated Fullscreen setup/dashboard unless justified by users.
- Ensure game IDs and metadata survive source outages and settings upgrades; no duplicate imports.

Acceptance: signed-off manual matrix with screenshots from disposable Playnite and automated regressions for actual bugs. Do not treat images as evidence of behavior they cannot show.

### Track E — packaging, documentation and upgrade (P0 final gate)

- Upgrade installed v0.2.0 and v0.3.2 profiles to the exact 1.0 release candidate, preserving provider settings, identities and recorded bytes. Address earlier v0.1.1 separately if promised.
- Run Windows Debug and Release, test suite (including opted-in real-rclone fixtures), official Playnite Toolbox checks and SHA-256 package validation on the same commit.
- Verify public add-on listing, icon, approved screenshots, changelog, installation and rollback instructions, license and endpoint URLs.
- Owner explicitly authorizes merge and publishing only **after** signoff.

Acceptance: zero unresolved P0 safety bugs; high-severity defects have an explicit owner decision; all public claims grounded by evidence.

## What to defer beyond 1.0

Auto-eviction, automatic full-library caching, save/mod sync, storefront-like discovery, AI recommendations, game streaming, cross-device execution, cloud account management and a specialized Fullscreen dashboard are not 1.0 entry requirements. Do not import incompatible GPL/AGPL implementation code into this MIT plugin.

## Delivery rhythm

Use small scoped PRs, CI + deterministic fixtures, and one verified Windows host retest when observable behavior changes. Avoid repeating complete GUI acceptance after documentation-only changes. Update [release status](release-status.md) and [known limits](known-limits.md) with evidence, not optimism.
