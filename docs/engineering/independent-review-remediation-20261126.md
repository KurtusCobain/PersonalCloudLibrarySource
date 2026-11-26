# Independent review remediation register — 2026-11-26

Status: in progress; not acceptance or release authorization. Both independent reviewers recommended NOT READY. Claude's review was partial (approximately 3,300 of 25,046 production lines); Grok's report covers ten areas but some claims lack exact reproduction. Owner authorized continued remediation after Claude exhausted usage. CH-Rm remains last; do not access it yet.

Qualified source: b090230dea6c9d5832dee3e222b579c7e84dd154. Pre-remediation branch checkpoint: 87625e22b1ae649a67c8fb83931b774239c5ea5e. Preserve both. No public main, releases, tags, installer feed, branch deletion or live user library changes.

## Claude register

| ID | Reported finding | Current disposition |
|---|---|---|
| P1 | Candidate version/identity collision | Internal candidate 0.3.2.1 (manifest and assembly), public feed unchanged; CI guard and validator fixture added. Official Toolbox and actual migration pending |
| P2 | Narrow CodeQL gate | Baseline artifact downloaded and digest verified; zero results across full C# SARIF scan. All-result CI gate added; qualified at 39dc6f3 (zero reported findings) |
| P3 | Free-space checks for UNC/ZIP/rclone | Local and ZIP now query actual destination ancestor capacity via Win32; Windows CI pending. Unknown capacity remains best effort; rclone without reliable expected size still needs separate design/host test |
| P4 | ZIP32 limits vs advisory policy | Open; align shipping ZIP parser and documentation without narrowing unrelated archive advisory contracts unnecessarily |
| P5 | Temporary files retained on failures | Open; only remove verified owned files, preserve uncertain evidence |
| P6 | rclone :local: production configuration | Open; assess supported provider semantics and tests before change |
| P7 | Explorer argument quoting | Confirmed two raw directory arguments; now use existing Windows argument quoting. File selection already quoted; host behavior pending |
| P8 | Process diagnostics | Added exception type/HRESULT only, with regression assertions against message disclosure; qualification pending |
| P9 | Queue ordering/concurrency settings | Dispatch uses registration order; clock regression added. Invalid concurrency fallback to one is an explicit tested conservative contract, retained |
| P10 | Worker fault containment | Executor and subscriber exceptions already handled/tested; repeated-failure test added. Unexpected internal bookkeeping fault remains investigation target |
| P11 | Legacy process code | Open; reachability/test audit |
| P12 | Old release workflow | Open; do not change main or public release controls without separate approval |
| P13 | Absolute/UNC LocalFile source paths | Confirmed explicitly supported by existing test; design consent/trust boundary and migration before restricting |
| P14 | Process environment contract | Open |
| H1 | Handle inheritance | Open; adversarial host test |
| H2 | Long paths | Open; fail safely and document/measure |
| H3 | Overlapping ZIP member ranges | Confirmed missing range check; sorted local header/data interval validation and two regression cases added; Windows CI passed at 39dc6f3 |
| H4 | Superscript Windows device names | Confirmed in two inspected policies; corrected with 12 additional parameterized cases; Windows CI passed at 39dc6f3 |
| H5 | Encrypted rclone config | Open; test prompt/EOF behavior |
| H6 | Extended Windows path roots | Open; inspect native path contracts |

## Grok register

| ID | Reported finding | Current disposition |
|---|---|---|
| G1 | Non-atomic metadata replacement | Partially narrowed for three metadata stores: verify staged identity/bytes and lock original through validation; final File.Replace pathname race remains open |
| G2 | Ancestor-relative publication | Separate guard experiment retains qualified absolute native rename; below-root ancestor data-read handles deny delete sharing, identities checked before/after. CI pending; root mapping/reparse/SMB limitations remain |
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

## Third correction batch: queue order, navigation and diagnostics

Both preceding correction commits passed all three workflows. Exact checkpoint 39dc6f3afa470a83e4163e72ef949bda0d29e04a: 972 standard tests and two separately executed real-rclone tests passed, zero warnings, zero full CodeQL results and archive compatibility passed. Evidence: https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45770962482 and https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45770962455 and https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45770962465.

Remove wall-clock sorting from the queue's already ordered live list. Retain the tested invalid-concurrency fallback. Quote Explorer directory arguments in both entry points, leaving existing file selection syntax unchanged. Preserve exception class and HRESULT in rclone failures without emitting raw exception text, and strengthen existing launch-failure tests. Repeated executor exceptions are already caught inside ExecuteObserved; add a twelve-failure regression exceeding the worker count rather than rewrite terminal/recovery logic without a concrete defect. New batch CI pending; Explorer behavior requires Windows host acceptance. No release or CH-Rm changes.

## Fourth correction batch: destination capacity

Replace drive-letter-only capacity query with GetDiskFreeSpaceEx on the nearest existing destination ancestor (including UNC and mounted volumes where supported). Check ZIP's declared total output after read-only preflight but before creating staging. Preserve best-effort behavior when capacity is unavailable; do not claim space reservation or guaranteed protection against disk exhaustion. Add read-only existing-ancestor regression (zero and impossible byte requirements, no destination creation). Rclone lacks a reliable pre-transfer size on this path; do not add an unbounded remote enumeration or silently treat missing metadata as zero. Actual UNC and low-disk behavior remain host acceptance gates. CI pending.

## Fifth correction batch: shared private metadata replacement preflight

Profile, catalog identity and display snapshot stores used three duplicated replacement writers without verified staging identity/content. All now preserve a single create-versus-replace decision and delegate existing-record replacement to TransferPartialPathPolicy. The common helper rejects occupied stage/backup names, holds the original read handle during schema validation and stage creation, captures file identity, verifies bytes and rechecks both identities and original validity before File.Replace. Shared stage creation/byte verification also serve create-only publication. Existing backup names, format and atomic File.Replace semantics remain. Two regression cases exercise invalid-original refusal with a locked original and occupied-backup refusal before staging. Uncertain temporary evidence is intentionally retained; no pathname cleanup. The final closed-handle File.Replace race, backup substitution and nonparticipating writer coordination are still unresolved, not represented as closed. CI pending.

## Sixth correction batch: distinguish internal candidate

Use numeric version 0.3.2.1 only on the isolated development branch (extension manifest and assembly/file version). Retain extension ID for real migration testing, so this is not safe to install into an ordinary profile. Public v0.3.2 artifacts, installer feed, add-on listing, main, tags, license and release-control remain unchanged. CI checks positive fourth component and strictly newer than every published installer version, and validates candidate package metadata and the known public v0.3.2 digest. The PowerShell fixture exercises rejected published/equivalent/older versions, valid candidate package, and accidental candidate feed inclusion. Official validator has explicit -Candidate mode and clearly separates candidate pack verification from public feed verification. Toolbox compatibility of the four-component version and disposable migration remain unqualified; no public version decision is made. CI pending.

## Seventh correction batch: isolate ancestor guarding from native rename syntax

The withdrawn d1d74d74 experiment combined ancestor guarding with parent-relative FileRenameInfo and failed 125 Windows tests. This batch leaves the qualified absolute no-replace rename buffer/RootDirectory unchanged. It opens non-volume/share-root ancestor directories top-down with FILE_LIST_DIRECTORY and FILE_READ_ATTRIBUTES, no-follow, read/write sharing but no delete sharing; captures identities and reobserves them before/after publication, then verifies the published pathname against the same source identity. A disposable test checks that ordinary parent/grandparent rename is refused during the guard, child creation remains possible, and rename works after release. API references: https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_rename_info and https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilea. This is an isolated compatibility experiment until exact CI passes. It narrows ordinary ancestor rename/delete races, not full ancestor-relative namespace resolution, root mapping changes, arbitrary reparse changes or transactional member locking. Directory listing permission/SMB behavior may refuse previously usable paths; verify on real providers before acceptance. No additional deletion or overwrite authority.
