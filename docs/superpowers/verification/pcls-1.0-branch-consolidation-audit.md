# PCLS 1.0 Branch Consolidation Audit

**Repository:** `KurtusCobain/PersonalCloudLibrarySource`  
**Primary branch:** `feature/user-friendly-dashboard-cleanup`  
**Audit date:** 2026-07-13  
**Snapshot head:** `17141439c107cb96d2d0d34f88b9c413b22ebecd`

## Scope and safeguards

This audit compares every active development or CI branch associated with the dashboard work against `main` and the designated cleanup branch.

No branch was merged, deleted, force-updated, or rebased during this audit. No pull request was closed. The only repository change made by this pass is this report.

The intended final branch set remains:

- `main`
- `feature/user-friendly-dashboard-cleanup`

## Executive conclusion

No release-critical implementation exists only on one of the four temporary CI branches. Their branch-only changes are trigger or verification files used to start temporary workflows.

The old `feature/user-friendly-dashboard` branch contains historical cleanup and packaging commits after its divergence point, but the current cleanup branch already contains the required dashboard, setup, settings, transfer, game-command, localization, testing, and packaging work. Its remaining differences are either independently represented, superseded by the direct-asset branding approach, or suitable for the later general repository-cleanup audit rather than a branch merge.

No cherry-pick or manual recovery is required from the audited branches.

After explicit approval, the following branches are deletion candidates:

- `feature/user-friendly-dashboard`
- `ci/package-test-0.3.0`
- `ci/package-fix-0.3.1`
- `ci/verify-0.3.2-red`
- `ci/apply-0.3.2`

PR #8 is superseded by PR #9 and is a closure candidate. PR #9 must remain open and draft.

## Branch inventory

Ahead/behind values are measured from the named branch's point of view.

| Branch | Snapshot SHA | PR | Purpose | Versus `main` | Versus cleanup branch | Unique branch commits | Unique useful work | Obsolete or risky content | Represented or superseded in cleanup | Recommended action | Final status |
|---|---|---:|---|---:|---:|---:|---|---|---|---|---|
| `main` | `6e1f4c7ea23b15ca28d12cfe544be8ace057df6d` | — | Stable release base | baseline | 0 ahead / 334 behind | — | Public baseline | Does not contain current dashboard work | Intentionally remains unchanged | Keep | Required |
| `feature/user-friendly-dashboard-cleanup` | `17141439c107cb96d2d0d34f88b9c413b22ebecd` | #9 open draft | Sole 1.0 development and review branch | 334 ahead / 0 behind | baseline | — | All current release development | Current CI failure described below | This is the consolidation target | Keep; do not merge yet | Required |
| `feature/user-friendly-dashboard` | `555dcced052e9d1f69b617999bb6c48759b7d3b5` | #8 open draft; #7 closed historical | Original dashboard branch and repeated verification history | 327 ahead / 0 behind | 13 ahead / 20 behind | 13 | No missing release feature identified | Historical automated asset materialization, encoded assets, one-time scripts, and older packaging approach | Dashboard, wizard, settings, transfers, commands, tests, localization, and packaging are present on cleanup; current workflow error handling is stronger and current packaging uses direct assets | Close #8, then delete branch after approval | Safe deletion candidate |
| `ci/package-test-0.3.0` | `ecdc3e61f50924fdc4e227b613863f6bc8712625` | #3 closed draft | Temporary 0.3.0 package build | 235 ahead / 0 behind | 1 ahead / 100 behind | 1 | None | Branch-only change adds `automation/package-test-pr.txt` | All actual implementation continued on later feature/cleanup history | Delete after approval | Safe deletion candidate |
| `ci/package-fix-0.3.1` | `47f15587e895f99a27773aa2999ad441a830e40a` | #4 closed draft | Temporary settings-crash/package verification | 249 ahead / 0 behind | 3 ahead / 88 behind | 3 | None requiring recovery | Branch-only comparison resolves to the temporary `automation/package-fix-0.3.1.txt` trigger state | Settings migration and package fixes exist in newer cleanup code and tests | Delete after approval | Safe deletion candidate |
| `ci/verify-0.3.2-red` | `7f985e7c964d017800497ac9c62b29cd64504abc` | #5 closed draft | Intentional red-test verification | 256 ahead / 0 behind | 1 ahead / 79 behind | 1 | None | Adds `automation/verify-0.3.2-red.txt`; associated CI was intentionally failing | Regression tests were subsequently implemented and passed on the clean branch baseline | Delete after approval | Safe deletion candidate |
| `ci/apply-0.3.2` | `c5fecb6069bf0d213612d5b734bcf27e9f33ed3e` | #6 closed draft | Temporary one-time implementation/application branch | 273 ahead / 0 behind | 2 ahead / 63 behind | 2 | None requiring recovery | Adds `automation/apply-0.3.2.txt` and belongs to the temporary mutation workflow era | Resulting valid implementation is present in cleanup; one-time application machinery is intentionally absent | Delete after approval | Safe deletion candidate |

## Evidence for the old feature branch

The old dashboard branch and the cleanup branch diverged at `9fe1cc51be2fcaeb12548bd863ff1ef2006c0039`.

The old branch's 13 post-divergence commits concern cleanup and package verification rather than a separate missing feature set. The comparison identifies changes around:

- `.github/workflows/apply-0.3.2-pr.yml`
- `.github/workflows/materialize-brand-assets.yml`
- `automation/apply-0.3.2-trigger.txt`
- `tools/apply-0.3.2-fixes.ps1`
- encoded or split branding sources
- `.github/workflows/build.yml`
- `tools/package-extension.ps1`
- `msbuild.cmd`

The current cleanup branch was checked directly:

- The self-applying workflow is absent.
- The asset-materialization workflow is absent.
- The old trigger file is absent.
- The current build workflow restores packages, builds Debug, runs the complete NUnitLite executable, uploads results, builds a Release package, inspects package contents, and uploads the package.
- Its package error path preserves diagnostics and rethrows the failure rather than replacing it with a less descriptive exit.
- The current package script gets its version from `extension.yaml`, uses the corrected `Move-Item -Destination` parameter, and packages the direct source assets instead of depending on `decode-brand-assets.ps1`.
- The cleanup branch contains the dashboard, setup wizard, versioned settings, migration service, transfer manager/executor/adapters, game context commands, details view, localization additions, and corresponding test projects.

The old branch therefore does not need to be merged into cleanup. `msbuild.cmd` and any remaining development-only helpers should be judged during the broader 1.0 file/code cleanup, not recovered through a historical branch merge.

## Evidence for temporary CI branches

Comparison against the cleanup branch isolated these branch-only changes:

- `ci/package-test-0.3.0`: one commit adding `automation/package-test-pr.txt`
- `ci/package-fix-0.3.1`: three commits whose remaining unique file is `automation/package-fix-0.3.1.txt`
- `ci/verify-0.3.2-red`: one commit adding `automation/verify-0.3.2-red.txt`
- `ci/apply-0.3.2`: two commits adding `automation/apply-0.3.2.txt`

These are workflow triggers or historical verification artifacts. None should be cherry-picked into the 1.0 branch.

Closed PRs #3 through #7 preserve the historical purpose and discussion after branch deletion.

## Preserved work

No commits were cherry-picked or recreated.

No source, test, packaging, workflow, or branding file was recovered from an old branch because no required unique implementation was found.

## Validation state

### Current cleanup head

GitHub Actions run #72 tested cleanup head `17141439c107cb96d2d0d34f88b9c413b22ebecd`.

- NuGet restore: passed
- Debug build: passed
- Test executable: ran the full suite
- Test result: **85 total, 0 failures, 1 error**
- Release package build: skipped after test error
- Package inspection: skipped

The single error is:

`PersonalCloudLibrarySource.Tests.Ui.UiContractTests.BrandArtwork_UsesDirectReferencePngs`

It throws `DirectoryNotFoundException` because the test calls `Directory.GetFiles` on the removed `tools/assets` directory. This is associated with the in-progress direct-reference branding cleanup and is not caused by branch consolidation.

The current cleanup head is therefore **not release-ready and must not be merged into `main`** until that test is updated and the complete package workflow passes again.

### Last fully green cleanup baseline

Cleanup commit `8587b0b4d77cfa26f119254e5543d3938cb21017` passed GitHub Actions run #71, including the full test suite and package workflow. This earlier success is useful historical evidence but does not validate the newer head.

Fresh green verification is required after the branding task finishes.

## Files changed by this consolidation pass

- Added `docs/superpowers/verification/pcls-1.0-branch-consolidation-audit.md`

No production code or tests were changed.

## Pull-request actions proposed

| PR | Current state | Proposed action |
|---:|---|---|
| #9 | Open draft, cleanup branch to `main` | Keep open and draft. Later retitle and rewrite from 0.3.2 branding verification to the 1.0 release hardening scope. Do not merge yet. |
| #8 | Open draft, old feature branch to `main` | Close as superseded by #9 after approval. |
| #7 | Closed draft | No action. |
| #6 | Closed draft | No action. |
| #5 | Closed draft | No action. |
| #4 | Closed draft | No action. |
| #3 | Closed draft | No action. |

## Approved end state after the destructive cleanup pass

Branches to retain:

- `main`
- `feature/user-friendly-dashboard-cleanup`

Branches proposed for deletion:

- `feature/user-friendly-dashboard`
- `ci/package-test-0.3.0`
- `ci/package-fix-0.3.1`
- `ci/verify-0.3.2-red`
- `ci/apply-0.3.2`

No deletion or PR closure should occur until the repository owner explicitly approves those exact actions.
