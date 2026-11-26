# Independent review remediation register — 2026-11-26

Status: in progress; not acceptance or release authorization. Both independent reviewers recommended NOT READY. Claude's review was partial (approximately 3,300 of 25,046 production lines); Grok's report covers ten areas but some claims lack exact reproduction. Owner authorized continued remediation after Claude exhausted usage. CH-Rm remains last; do not access it yet.

Qualified source: b090230dea6c9d5832dee3e222b579c7e84dd154. Pre-remediation branch checkpoint: 87625e22b1ae649a67c8fb83931b774239c5ea5e. Preserve both. No public main, releases, tags, installer feed, branch deletion or live user library changes.

## Claude register

| ID | Reported finding | Current disposition |
|---|---|---|
| P1 | Candidate version/identity collision | Internal candidate 0.3.2.1 (manifest and assembly), public feed unchanged; CI guard and validator fixture added. Official Toolbox and actual migration pending |
| P2 | Narrow CodeQL gate | Baseline artifact downloaded and digest verified; zero results across full C# SARIF scan. All-result CI gate added; qualified at 39dc6f3 (zero reported findings) |
| P3 | Free-space checks for UNC/ZIP/rclone | Local and ZIP now query actual destination ancestor capacity via Win32; Windows regression passed at 362d5d3. Unknown capacity remains best effort; rclone without reliable expected size still needs separate design/host test |
| P4 | ZIP32 limits vs advisory policy | Documented effective ZIP32 limits separately from format-neutral ceilings; ZIP64 remains unsupported; no expansion of supported formats |
| P5 | Temporary files retained on failures | Open; only remove verified owned files, preserve uncertain evidence |
| P6 | rclone :local: production configuration | Inline backend removed from plugin; opt-in real-rclone tests now create uniquely named isolated local config; both real-binary cases passed at ae0c9fa1 |
| P7 | Explorer argument quoting | Confirmed two raw directory arguments; now use existing Windows argument quoting. File selection already quoted; host behavior pending |
| P8 | Process diagnostics | Added exception type/HRESULT only, with regression assertions against message disclosure; Windows qualification passed at 362d5d3 |
| P9 | Queue ordering/concurrency settings | Dispatch uses registration order; clock regression added. Invalid concurrency fallback to one is an explicit tested conservative contract, retained |
| P10 | Worker fault containment | Executor and subscriber exceptions already handled/tested; repeated-failure test added. Unexpected internal bookkeeping fault remains investigation target |
| P11 | Legacy process code | Open; reachability/test audit |
| P12 | Old release workflow | Open; do not change main or public release controls without separate approval |
| P13 | Absolute/UNC LocalFile source paths | Ordinary absolute/UNC compatibility retained; ambiguous device/drive-relative/stream names rejected lexically. Consent/trust boundary for valid external paths remains open |
| P14 | Process environment contract | Open |
| H1 | Handle inheritance | Open; adversarial host test |
| H2 | Long paths | Open; fail safely and document/measure |
| H3 | Overlapping ZIP member ranges | Confirmed missing range check; sorted local header/data interval validation and two regression cases added; Windows CI passed at 39dc6f3 |
| H4 | Superscript Windows device names | Confirmed in two inspected policies; corrected with 12 additional parameterized cases; Windows CI passed at 39dc6f3 |
| H5 | Encrypted rclone config | Explicit no-password-prompt flag for plugin copy/cat/listremotes and standalone listing; encrypted-config host behavior still unqualified |
| H6 | Extended Windows path roots | Raw namespace/name checks at local source, cache resolver, manifest loader and containment; tests added, CI pending |

## Grok register

| ID | Reported finding | Current disposition |
|---|---|---|
| G1 | Non-atomic metadata replacement | Partially narrowed for three metadata stores: verify staged identity/bytes and lock original through validation; final File.Replace pathname race remains open |
| G2 | Ancestor-relative publication | Separate guard experiment retains qualified absolute native rename; below-root ancestor data-read handles deny delete sharing, identities checked before/after. All three workflows passed at 38c076c5; root mapping/reparse/SMB limitations remain |
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

Candidate CI 45773386451 failed one repository contract (976 standard passed, one failed, two opt-in skips) because Get-FileHash was not available in the test PowerShell process; preliminary version guard passed and Debug compiled without errors. Replace that cmdlet with the same .NET SHA-256 streaming primitive used in existing regression fixtures. Requalify exact corrected SHA; do not describe c25a1e2c as passing.

## Eighth correction batch: rclone named remotes and noninteractive behavior

Remove the test-only inline :local: exception from the plugin operand builder. Configured named local remotes remain available; no claim that named remotes are a sandbox. Both opt-in binary fixtures create unique named local configs inside their disposable roots, set/restore RCLONE_CONFIG, and one checks exact listremotes output to verify isolation. Add builder and manifest-reader rejection cases. Explicit --ask-password=false is supported by the documented rclone config flag; plugin copy/cat/listremotes and standalone lsjson use it. The CLI also closes redirected stdin. Reference: https://rclone.org/commands/rclone/. Real binary and encrypted-config behavior need qualification; no credential access or remote writes. Legacy inline users must configure a named remote or choose LocalFolder. CI pending.

## Ninth correction batch: raw Windows path aliases

Apply existing Windows namespace policy before normalization in source, cache and manifest paths, including the containment predicate. Reject drive-relative/root-relative source/cache paths, alternate streams, control/invalid characters, reserved device stems with spaces and device namespaces. Replace per-segment regex with ordinal device-name checks shared by cache fallback IDs and filesystem policy; the isolated archive policy retains its independent equivalent check. Ordinary fully rooted drive and UNC LocalFile sources are still supported; lexical UNC fixture makes no network access. This does not establish trust for valid external paths: P13 remains open for explicit consent/migration design. Added source/cache/containment and archive cases; CI pending.

## Qualification evidence — post-review continuation

| Exact source | Windows standard + real-rclone passed | Archive | Full CodeQL | Evidence |
|---|---:|---|---|---|
| `0ee6f7dfa2486acc1de70df8a54e014b3ebb2f31` | 977 + 2 | PASS | PASS | [Windows](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45773115113), [archive](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45773115024), [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45773115046) |
| `38c076c5c27f24a7ece2a58fc237cbbef8747338` | 978 + 2 | PASS | PASS, 0 findings | [Windows](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45773707857), [archive](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45773707882), [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45773708014) |
| `ae0c9fa1c16c7c760b300406e47563b2065b78b6` | 980 + 2 | PASS | Not yet verified at entry | [Windows](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45773877825), [archive](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45773877765), [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45773877849) |
| `e51e15b6f2833b1ca20fc65919bb92709a738fcd` | Pending at entry | PASS | Pending at entry | [Windows](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45774073827), [archive](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45774075185), [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45774073844) |

The two opt-in rclone cases are skipped in each standard run and executed separately; do not count the skips as passes. Windows builds at 38c076c5 and ae0c9fa1 both reported zero errors/warnings and zero failed tests. Candidate SHA-256 at 38c076c5: `A3BC3E9760F12CE5C50A8CB53510EE1A8CF07246420D80CD029B561EB08410DE`; at ae0c9fa1: `D5F43CA92AE6DFB0ADF5B0C2158A307B8321BA3090F00FCA63541EF0E9C2C950`. These digests belong to those exact artifacts only. The intermediate 1cd1ad23 Windows run also failed the same missing Get-FileHash contract before the correction; it did not qualify. Latest full pass verified at entry: 38c076c5. Do not infer Toolbox, real SMB, native Playnite, independent-review closure or release readiness from hosted workflows.

## Tenth correction batch: participating private metadata writer coordination

Starting checkpoint 6327eae passed Windows 45774316878 (998 distinct passing), archive 45774317134 and full CodeQL 45774316867. All three private metadata stores now validate input before acquiring a persistent per-record exclusive no-follow single-link write lock, then hold it across create/replace decision and publication. Contention fails promptly, with no stage or backup creation; never unlink the lock. Existing backup names and File.Replace semantics are retained. Replacement also holds the qualified below-root ancestor guards, compares original content digests before mutation, and checks output and backup content afterward. Postcondition failure preserves evidence; no rollback by path. Three profile regressions exercise contention on creation and replacement, retry, and hardlinked lock refusal. This coordinates participating writers only, not stale read-modify-write callers or arbitrary external programs. The closed-leaf-handle pathname race and backup collision window remain; no claim of atomic compare-and-swap or power-loss certification. CI pending.

## Eleventh correction batch: explicit contained process environment

WindowsJobProcessHandle previously passed a null native environment block regardless of ProcessStartInfo.EnvironmentVariables. Snapshot the supported variables, resolve unqualified executable names using that snapshot's PATH, serialize a bounded sorted double-NUL-terminated Unicode block, pass CREATE_UNICODE_ENVIRONMENT, and clear the temporary pinned character buffer after process creation. Invalid variable syntax fails closed; no environment values logged. Real Windows child fixture checks explicit Unicode override, removal, and unchanged parent environment. Existing default caller inheritance is retained through the ProcessStartInfo snapshot, including RCLONE_CONFIG and other configured environment variables. Per-drive pseudo-variables are not supported by this explicit contract. This is a process configuration/lifetime boundary, not environment isolation from user credentials or arbitrary execution sandboxing. CI pending; no host credential access.

## Twelfth correction batch: locked optimistic profile edits and bounded digest verification

SourceProfilesManagementService's stale-editor comparison previously preceded the file-store write lock. SaveAgainstBaseline now compares the canonical editor baseline inside that lock and again during replacement validation; an observed-existing record that disappears refuses rather than recreates. The public low-level Save API retains intentional unconditional-update behavior. Explicit first-use preview remains compatible; the UI does not yet carry a durable record-presence token from preview, so a record removed before the service's initial presence observation cannot be distinguished from first use. Lock lifetime now includes below-root directory guards, including create-only saves. Digest verification enforces each private record store's own byte budget before hashing. Added stale and disappeared-baseline regression; removed redundant test script construction. Coordination is limited to the cooperating record store and management path, not all plugin metadata or hostile external writers. CI pending.
