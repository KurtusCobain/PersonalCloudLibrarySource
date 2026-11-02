# PCLS 1.0 implementation/review/acceptance sequence

Owner-approved development sequence, November 1, 2026. **Do not conduct the expensive installed-Windows acceptance campaign until after candidate coding and independent reviews**; keep small automated CI checks on every code change.

## Current working branches

| Draft PR | Purpose | Status at creation of this plan |
|---|---|---|
| [#11](https://github.com/KurtusCobain/PersonalCloudLibrarySource/pull/11) | Public docs, proposed upstream listing, historical QA archive and 1.0 roadmap | CI green; unmerged |
| [#15](https://github.com/KurtusCobain/PersonalCloudLibrarySource/pull/15) | Shared-cache per-file install-size candidate | CI green; Open installation folder and legacy size behaviors unresolved |
| [#16](https://github.com/KurtusCobain/PersonalCloudLibrarySource/pull/16) | Read-only directory-cache diagnostics, inventory, nonauthorizing V2 receipt comparison | CI green on initial phase; per-file diagnostics evolving |

Track the complete schedule and gates in [issue #17](https://github.com/KurtusCobain/PersonalCloudLibrarySource/issues/17). Do not confuse green CI with native Windows Playnite approval.

## Phase 1 — finish scoped implementation in isolated draft PRs

1. Implement contained issue #12 per-game size correction, retaining launch and file-only uninstall behavior. The candidate must explicitly resolve whether it is acceptable to lose Playnite's native Open installation folder feature for implicit file-type games and how previously inflated saved sizes are handled.
2. Complete issue #13 safety infrastructure: per-file inventory data and read-only ownership/delta planning, with no deletion authorization. Design any later deletion execution separately with fail-closed proof, cancellation and recovery requirements.
3. Add issue #14 resilience fixes where code-confirmed: offline/disconnected source state must not erase the catalog or modify source files, transient errors must reach a stable queue state and support bounded retry.
4. Implement concrete accessibility, DPI, Fullscreen and onboarding corrections supported by reproducible tests, avoiding feature creep.
5. Keep changes separate, reviewable and with new deterministic unit/integration regression tests. All PRs run Windows Debug/Release build, unit tests, static/release metadata checks and package inspection. Do not run real connected providers, destructive data operations or repeat multi-hour GUI automation during this phase.

## Phase 2 — integrate and freeze code for review

- After each scoped draft is green, stage one integration **candidate** branch pinned to exact base and head SHAs, preserve commit lineage and merge gates.
- Re-run complete GitHub CI on that candidate and attach concise changed-file inventory, test totals, known limits, new risks, screenshots already captured, and unresolved test-required hypotheses.
- Freeze features; independent reviewers should examine the **same exact candidate SHA**. Do not let reviewers work against divergent snapshots.
- Ask **Claude, Grok and Nova to review**, with explicit read-only permissions. Reviewer feedback is advisory and must not mutate production sources automatically.
- Resolve their findings with minimal, evidence-backed patches, then rerun CI and freeze a new candidate SHA. Repeat review only when changes materially affect previously reviewed risks.

## Reviewer handoff template (populate exact commit after code freeze)

> **PCLS 1.0 independent code-complete review — read-only, advisory only**
>
> Repo: `KurtusCobain/PersonalCloudLibrarySource`.
> Candidate SHA: `[ADD EXACT FROZEN SHA]`.
> Scope: local/external/NAS and separately configured rclone catalog-and-cache Playnite GameLibrary plugin, C#/WPF, MIT.
>
> Published v0.3.2 is unchanged. Do not merge, publish, tag, run tests on real library data, add cloud credentials, or enable recursive directory deletion.
>
> Compare the exact candidate to the last public release and inspect production source, all changed C#/XAML, manifests, tests, release docs and known limitations. Review the documented Playnite SDK behavior rather than assuming methods work differently.
>
> For every claim: classify **BLOCKER / HIGH / MEDIUM / LOW**. Provide exact file+line, failure mechanism, minimal reproducible scenario, smallest safe correction, and what verification evidence is necessary. Distinguish code-proven problems from hypotheses requiring real Playnite.
>
> No material changes or merge without owner approval. Return a concise prioritized findings table and explicit **READY FOR MANUAL ACCEPTANCE / NOT READY** recommendation.

### Claude focus

Technical adversarial review of C#/WPF architecture, Playnite SDK lifecycle and correctness, asynchronous install/disposal/races, cancelled rclone staging, cache path and reparse-point containment, per-game installed size metadata, upgrade compatibility, ownership-receipt integrity, file-by-file deletion threat model, and coverage quality. Do not infer that a V2 hash match is deletion authorization.

### Grok focus

Real-world Playnite extension usability, source disconnects and NAS/UNC remapping, rclone authorization and throttling, hidden assumptions about cache layout, launcher/working directory, Fullscreen game actions, existing game metadata after import refresh and regressions against common workflows. Validate competitor observations independently; no copying GPL/AGPL code into this MIT repo.

### Nova focus

First-run user journey, clear local/NAS/remote setup language, accessible labels and messages, screen-reader/keyboard/150%-DPI hazards, privacy of reports and release images, migration/rollback documentation, FAQ/support triage, and consistency between public listing and actual features. Mark visual observations unverified without real screenshots.

## Phase 3 — one consolidated acceptance campaign

Only after reviewer blockers are fixed and code frozen:

- Use disposable **Windows Playnite Desktop and Fullscreen** profiles and fixture libraries, keeping personal ROMs and user data untouched.
- Cover 100% and 150% DPI, keyboard navigation, first-run preview/import, file/directory installs, actual file-size display, Play, file-only Uninstall, normal setup, source disconnect/reconnect, slow transfer, Cancel/Retry/Dispose, corrupted manifest, low disk, reparse-safety, and stable game IDs.
- Run v0.2.0 and v0.3.2 upgrades to final candidate from separate disposable installed profiles; verify hashes, settings and metadata.
- Run one disposable real NAS/UNC scenario and one sandbox-configured rclone remote, with no real personal cloud account or game archive.
- Collect genuine screenshots and concise PASS/FAIL/BLOCKED evidence. Stop on suspected data corruption, deletion risk, broken Playnite integration or unexplained freeze.
- Run official Playnite Toolbox validation of the exact final installer and package. Do not publish until final explicit owner signoff.

## Nonnegotiable limits

- **No automatic recursive game-folder deletion** unless separately proven safe and explicitly enabled by the owner after independent review and Windows QA; a safe 1.0 with the feature disabled is preferable.
- No broad UI automation or repeated screenshots during phase 1.
- CI and deterministic checks remain mandatory during coding. Review and acceptance are different gates and cannot replace one another.
- Published v0.3.2 GitHub release, tag and Playnite installer feed remain unchanged until a new release is authorized.
