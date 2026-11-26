# Independent review remediation register — 2026-11-26

Status: in progress; not acceptance or release authorization. Both independent reviewers recommended NOT READY. Claude's review was partial (approximately 3,300 of 25,046 production lines); Grok's report covers ten areas but some claims lack exact reproduction. Owner authorized continued remediation after Claude exhausted usage. CH-Rm remains last; do not access it yet.

Qualified source: b090230dea6c9d5832dee3e222b579c7e84dd154. Pre-remediation branch checkpoint: 87625e22b1ae649a67c8fb83931b774239c5ea5e. Preserve both. No public main, releases, tags, installer feed, branch deletion or live user library changes.

## Claude register

| ID | Reported finding | Current disposition |
|---|---|---|
| P1 | Candidate version/identity collision | Open: distinct candidate package and Toolbox check, preserve public artifacts and migration |
| P2 | Narrow CodeQL gate | Baseline artifact downloaded and digest verified; zero results across full C# SARIF scan. All-result CI gate added; new run pending |
| P3 | Free-space checks for UNC/ZIP/rclone | Open; distinguish unknown capacity from known insufficiency |
| P4 | ZIP32 limits vs advisory policy | Open; align shipping ZIP parser and documentation without narrowing unrelated archive advisory contracts unnecessarily |
| P5 | Temporary files retained on failures | Open; only remove verified owned files, preserve uncertain evidence |
| P6 | rclone :local: production configuration | Open; assess supported provider semantics and tests before change |
| P7 | Explorer argument quoting | Open |
| P8 | Process diagnostics | Open; redact sensitive paths/credentials |
| P9 | Queue ordering/concurrency settings | Open |
| P10 | Worker fault containment | Open; preserve reservations on ambiguous publication |
| P11 | Legacy process code | Open; reachability/test audit |
| P12 | Old release workflow | Open; do not change main or public release controls without separate approval |
| P13 | Absolute/UNC LocalFile source paths | Confirmed explicitly supported by existing test; design consent/trust boundary and migration before restricting |
| P14 | Process environment contract | Open |
| H1 | Handle inheritance | Open; adversarial host test |
| H2 | Long paths | Open; fail safely and document/measure |
| H3 | Overlapping ZIP member ranges | Confirmed missing range check; sorted local header/data interval validation and two regression cases added; Windows CI pending |
| H4 | Superscript Windows device names | Confirmed in two inspected policies; corrected with 12 additional parameterized cases; Windows CI pending |
| H5 | Encrypted rclone config | Open; test prompt/EOF behavior |
| H6 | Extended Windows path roots | Open; inspect native path contracts |

## Grok register

| ID | Reported finding | Current disposition |
|---|---|---|
| G1 | Non-atomic metadata replacement | Open; validate threat model and alternatives without sacrificing backup guarantees |
| G2 | Ancestor-relative publication | Open; revisit prior withdrawn 125-failure experiment in isolation |
| G3 | Cooperative cross-process locking | Open; external nonparticipants cannot be universally serialized by a named mutex |
| G4 | Crash/power-loss recovery | Open; read-only diagnostics remain, no automatic deletion/release |
| G5 | Legacy recursive deletion | Open; disabled shipping behavior remains |
| G6 | Process containment | Open; Claude disputes breakaway claim; verify actual flags and lifecycle tests |
| G7 | Real-world race tests | Open; add disposable-host and CI fixtures where practical |
| G8 | Performance and operational behavior | Open; measure rather than extrapolate synthetic results |
| G9 | Dead code and complexity | Open; semantic review |
| G10 | Dependencies and supply chain | Positive observation; continue qualification |

Additional Claude trade-offs: source hash optional contract, quarantine recovery, correlated tests and incomplete review coverage. No proven exhaustive audit or production-readiness claim.

## First correction batch

Source inspection confirmed that ArchiveInspectionPolicy and CachePathResolver recognize ASCII COM/LPT digits but not Windows superscript 1/2/3 device aliases. Both reject those aliases now. Archive expansion ratio used truncating division, allowing a ratio slightly above 1000:1; use (bytes - 1) / compressed >= limit when bytes > 0 and compressed > 0, avoiding multiplication overflow. Four ratio-boundary cases added.

Baseline CodeQL artifact: https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45732477534/artifacts/15077312106
SHA-256: 19e177486246fc7f1c5342a2c7ef7a8862bb2fea895b0e36409d560589459009. Downloaded archive digest matched; csharp.sarif contained one run with zero results. This is full reported-result evidence for that scan, not proof of no vulnerabilities. Future workflow retains ZIP Slip check and additionally gates every reported result, retaining artifact even on failure.

Qualification pending for this batch. No Windows execution or Toolbox acceptance in this pass. Continue validating each open finding, record exact evidence and qualification, then address deferred CH-Rm before final independent review and owner host acceptance.

## Second correction batch: ZIP local range validation

The central inspector verified local offsets precede the central directory, but did not compare physical member intervals. A repeated offset or overlapping compressed range could pass that preliminary gate. The inspector now sorts offsets, reads bounded 30-byte local headers, validates their signatures and name/extra lengths, and refuses overlap or compressed data beyond the central-directory boundary. Two extraction regressions mutate central metadata to duplicate a local offset or extend a member beyond the data area. No stage or destination may be created and source bytes must remain unchanged. This checks header/data interval overlap, not the full semantics of optional data descriptors, and does not establish general malformed ZIP safety. Additional hosted Windows and provider compatibility qualification remains pending.
