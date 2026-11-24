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

## Queue retry authorization and recovery quarantine

Verified e997bf7b: 797 passed, zero failures/compiler warnings, two opt-in skips; Windows packaging and archive workflows green. Queue captures independent transfer settings, refuses changed provider/cache identity and disabled downloads, and does not combine global multi-source settings with old scoped transfer paths. Unconfirmed subprocess exit is explicitly marked as requiring manual recovery. The queue blocks retry and new overlapping destination/game attempts for the session; this is not durable process containment or restart recovery. Job rows suppress retry for quarantined attempts. Manager Changed observer exceptions cannot interrupt committed state mutations. Legacy manifest content drift, cross-session recovery records and bounded history remain open audit items.

## Staging collision preservation

Both transfer adapters now refuse pre-existing staging paths instead of deleting them at startup. Local file cleanup only becomes eligible after successful exclusive file creation; local directory cleanup becomes eligible after creation and checks descendant links/size bounds. Rclone cleanup is withheld on runner exceptions with unconfirmed process exit. Local directory verification uses the existing nonrecursive safe traversal rather than a recursive GetFiles call. These are conservative preflight and cleanup improvements, not handle-bound race closure; directory creation and subsequent path replacement remain qualification items.

## Shared transfer verification and bounded traversal

Verified 262c377c: 803 passed, zero failures/compiler warnings, two opt-in skips; Windows packaging and archive compatibility green. Consolidated deletion descendant inspection, local source discovery, local/rclone staging size verification and final destination verification around one bounded top-down walker. Verification no longer scans once for links and then recursively enumerates unchecked descendants. Directory copies reuse one application copy buffer for all files. Rclone cleanup refuses a staging type substitution and flags cleanup failure for session quarantine. The walker limits entries to 150,000, observes cancellation in prepublication verification, and uses checked byte totals. This is pathname-level defense; not an atomic filesystem snapshot or cryptographic integrity verification. No runtime throughput benchmark has been claimed.

## Indexed transfer lookups and destination reservations

The manager now indexes jobs by ID and per-game attempt list while retaining original history order and retry ancestry. ID lookups avoid linear history scans; game lookups avoid allocating/sorting every repository-wide attempt. Aggregate byte totals fail to indeterminate on overflow. Completed and recovery-quarantined requests release their retained configuration snapshot, not their history or completion result. The queue refuses overlapping active/queued cache reservations across different games. Persistent history, cross-session recovery quarantine and a bounded retention policy remain open; no historical record was silently discarded. Complexity improvements are structural, not measured throughput claims.

## Audit sequencing and dashboard update consolidation

Owner direction: defer all further CH-Rm research, patch review and selective integration until PCLS's own repository remediation is complete; preserve prior work and do not touch the fork in the interim. Verified 246174c8: 808 tests passed, zero failures or compiler warnings, two opt-in skips; Windows packaging and archive compatibility passed. Dashboard queue, recent activity and state notifications use independent acknowledged refresh gates rather than rebuilding both lists per activity or byte update. The gate releases before the callback reads state so a mid-refresh event can schedule a follow-up. Manager lifecycle revision distinguishes byte-only progress from changes requiring full library status refresh; ordinary progress updates only update sidebar progress and the queue view. The activity burst fixture now requires one submission for 55 records, with the latest 50 preserved. Native UI responsiveness and end-to-end throughput remain to be measured.

## Settings transaction and configuration predicate cleanup

SettingsEditSession publishes its committed snapshot only after the persistence callback succeeds. Failed saves leave the session active and allow correction or cancellation without publishing the failed candidate. The current-version invalid transfer concurrency normalization now reports a migration repair, allowing the normalized value to be persisted once rather than re-normalized at every load. Dashboard setup completeness and legacy setup migration share one source-configuration presence predicate while preserving the deliberate difference for disabled but previously configured libraries. No schema version change or migration removal. This is a transaction-state repair; physical persistence atomicity, draft-versus-runtime consumers and future-schema handling remain separate review items.

## Persistent transfer reservation journal

Verified dashboard commit 40acd381: 811 passed, zero failures/compiler warnings, two opt-in skips; Windows package and archive workflows passed. Introduced a bounded write-ahead reservation journal in plugin data/transfer-recovery. Before queue execution, an exclusive journal lock protects conflict checking and creation of a per-job marker containing only game ID, job ID and normalized destination. Markers are flushed before execution. A confirmed result may release its own marker only if the expected staging path is absent. Interrupted work, uncertain exit, failed marker release and malformed records retain evidence and refuse overlapping future execution. No automatic recovery deletion, relaunch or credential persistence. The fixture includes restart simulation, unrelated transfers, cleanup refusal, corrupt records and production queue gate integration. Manual recovery UI, actual power-loss durability, Windows process-tree containment, path race qualification and history retention remain open. CH-Rm remains deferred.

## Recovery journal boundary hardening and compile inventory

Verified 78721afc: 820 passed, no failures or compiler warnings, two opt-in skips; package and archive compatibility passed. The reservation journal now checks both final and staging path overlaps, refuses destinations inside or above journal storage, inspects directory-shaped pending records rather than ignoring them, and permits bounded retry for Windows sharing violations on the exclusive journal lock. The original destination is checked before normalization. No recovery marker or user data is automatically deleted on refusal. Repository tree at 78721afc contains 330 tracked blobs: 219 C#, 53 Markdown, 6 XAML, 11 YAML/YML, 8 PowerShell/PS module, 3 project files, 2 config files, 17 PNG and 11 other files. Both explicit legacy projects match their respective 118 production and 99 test C# files exactly, without duplicate or missing Compile includes. The SDK archive project has two default source files and two explicit linked production sources; not orphaned files. This is structural inventory, not a completed semantic dead-code audit. CH-Rm remains deferred.

## Compile inventory regression and framework-reference reduction candidate

Added a tracked-source compile inventory regression for both legacy C# projects. It compares git's tracked source paths with XML Compile includes and rejects duplicate, missing or unlisted sources. Removed five candidate legacy framework references (System.Data.DataSetExtensions, System.Data, Microsoft.CSharp, System.Net.Http and System.Xml.Linq) from the shipping project and two unused imports in settings/dashboard sources. Retain only if Debug/Release/WPF compilation, tests and package verification pass; native host loading still requires acceptance. This is a scoped reference cleanup candidate, not a claim of comprehensive semantic dead-code elimination. The isolated archive project and its explicit dependencies are unchanged.

## Compile cleanup correction

Recovery boundary candidate c62426cc verified 822 passing tests, zero failures/compiler warnings, two opt-in skips and successful packaging/archive gate. Candidate d498ac30 failed compilation with CS0246 for DashboardStateStore.ObservableObject. Explicitly import Playnite.SDK, the existing SDK namespace used by the base settings class. Framework reference removals remain candidates until Windows build and regression pass. No main/feed change.

## Ownership evidence and plan freshness

Receipt footprint probes now conservatively block legacy uninstall when storage is malformed, inaccessible, overlapping cache, linked or cannot establish absence. Receipt reads have a fixed 4097-character cap and path preflight. Managed ZIP footprint checks consult the receipt even if the cache directory is absent, and fail closed on invalid identity or unverified path. Multi-source action plan revalidation rechecks reference identity under the publication lock before returning a previously validated plan. This closes a stale-plan return window, not all subsequent execution races. Three disposable fixture regression cases added. Permanent ZIP cleanup remains disabled; no fork work performed.

## Restore proven dashboard namespace dependency

The explicit Playnite.SDK import correction at 9b0966ec did not resolve CS0246. Restore DashboardStateStore.cs byte-for-byte from the last successful Windows checkpoint c62426cc (including System.Collections.Generic). Do not classify this import as unused. The five project-reference reductions remain independently subject to CI; the last verified test count remains 822.

## UI recovery visibility and localization audit

At de4cf887, the English resource dictionary had 441 unique keys; all LOCPLS references in the four UI views resolved (dashboard 28, settings 113, details 18, wizard 37 per-view distinct keys), and the only external resource referenced by those views was host TextBrush. Settings already omit legacy no-effect toggles under localization regression coverage; retain serialized properties for migration compatibility. Completed jobs requiring recovery remain visible in the transfer list, with localized recovery label and guidance and no retry action. Queue quarantine changes notify presentation even after terminal state. Expected retry refusals are surfaced through the host dialog instead of log-only reporting. Two regression fixtures added. Persistent recovery listing across restart and a separately authorized recovery action remain open; no deletion or fork work.

## Live transfer index and synthetic performance qualification

Verified fb0ee526: 829 passed, no failures/compiler warnings, two opt-in skips, successful Debug/Release/package and archive compatibility. The five framework-reference reductions now pass Windows build and full regression with required SDK namespace restored. Manager active/queued indexes and failed count replace repeated scans of completed history on progress and scheduling paths. Existing history, identity, retry ancestry and CreatedAt ordering remain intact. Executor recovery flags route through the manager's notification-aware API. Added mixed-state invariant test and 5,000 historical/4 live job synthetic benchmark (2,000 reads against a conservative reference history scan), with exact byte-total equivalence and no flaky wall-clock threshold. Timing is not yet measured until CI returns. This is a focused in-memory benchmark, not a native Playnite, disk/network or startup performance qualification. CH-Rm deferred.

## Measured live-index benchmark and queue lifecycle hardening

Verified c6270bf2 Windows run 45350511001: 831 passed, zero failures/warnings, two opt-in skips, Debug/Release and package inspection passed; archive gate passed. Synthetic benchmark: 5,000 historical and four active jobs, 2,000 progress reads; reference scan 294.432 ms, indexed 0.675 ms, identical totals (~436x for this fixture only). New queue change bounds wakeup credits at five, serializes external wakeups with shutdown/disposal, and defers semaphore disposal until all workers actually exit if shutdown times out. Cancellation callback exceptions no longer strand cancellation. Destination reservation remains in force until the queue completion task resolves, including terminal publication and journal handling. Result retains recovery requirement through terminal normalization. Three additional tests cover late disposal calls, throwing cancellation callback and a blocked terminal observer. This does not establish a hard deadline for arbitrary synchronous observer callbacks or process-tree containment. CH-Rm deferred.

## Manifest discovery scaling

Replace repeated detected-directory prefix scans with normalized ancestor hash lookups; preserve case-insensitive membership, boundary semantics and detected-directory report ordering. Select preferred package launch file in one pass using the existing extension priority and stable ordinal-ignore-case path comparison. Remove unused private ScanEntry.IsDirectory flag (separate file/directory lists already carry the distinction). Added real disposable package selection/boundary fixture and 2,500-package/7,500-entry synthetic preview benchmark, with count and unique ID assertions. No generation rules intentionally changed; timing and regression qualification pending. CH-Rm deferred.

## Generated catalog and backup persistence

Verified queue checkpoint 0ed191f0: 834 pass, zero failures/warnings, two opt-in skips and successful Windows package/archive gates. Repeated live-progress benchmark 225.171 ms vs 0.483 ms. SafeFileWriteService uses exclusive CreateNew staging, flushes bytes before publication, and uses unique backup names with overwrite disabled. It no longer deletes an unowned staging path after successful publication. Manifest generation validates serialized UTF-8 byte and character limits before replacing prior output, and refuses a report path identical to the manifest path. Three regression fixtures cover multibyte limits, output collision preservation and repeated backup preservation. This does not claim cross-process backup/replace transaction isolation or handle-bound cleanup. No generated user data touched. CH-Rm deferred.

## Manifest benchmark verification and preflight correction

Windows run 45350964498 at d13d0ed7: 836 passed, zero failures/warnings, two opt-in skips; Debug/Release and package inspection passed; archive gate passed. Synthetic preview: 2,500 packages / 7,500 entries, 55.771 ms; this is not a real filesystem throughput claim. At 8e276f7f Windows run 45351094785, 838 passed and one test failed because the host-dependent SDK serializer was invoked before the invalid output-path check in standalone NUnit. Move output/report equality validation before scanning and serialization for non-preview generation. Preserve the regression expectation and do not bypass it. Public release remains unchanged.

## PR #22 overlap reconciliation and documentation index

At 8e276f7f, compared all 40 files changed by original draft PR #22 (56664f96) against main d414e2f1 and the candidate: 27 byte-identical, 13 evolved. Reviewed differences across all 13: both project files retain every original Compile include (shipping framework references separately verified); GameWorkflowControllerTests and SettingsMigrationServiceTests retain all original test names and substantive lines; MultiSourceControllerFactoryTests retains original cases with receipt-store argument hardening; all 379 PR localization keys and their lines retained; V3 settings and migration preserve multi-source setting/default and snapshot copy with subsequent migration normalization; coordinator only changes stale integration comment; root plugin/controller/factory preserve original public declarations while adding current ownership, path and action validation; external links preserve issue #19 and restore PR #22 verification. This is source reconciliation evidence, not proof of all possible behavioral equivalence. PR #22 remains open draft at its original SHA. No merge or deletion. The historical backup branch db8f14d48c4a1bb820a699b61aee8c9232dcb8a1 retains unique research docs docs/playnite-library-addon-research.md (blob 8be35ce585efaadfffc10a65c0fe0748baa19a47) and docs/romcade-audit-report.md (blob 5f180f3298d299e4a9924a79bc3f76ee2f0bcad2); both inspected for provenance, retain there rather than republish historical local environment material. Old installer/manifest edits not restored. Added a separate candidate qualification index and clarified README's file-only uninstall and stable-versus-candidate status. All release/feed changes remain prohibited; CH-Rm deferred.

## Manifest identity and enumeration integrity

Verified e0c23dcf Windows run 45351269054: 839 pass, no failures/compiler warnings, two opt-in skips; package and archive compatibility passed. Synthetic preview 2,500 packages/7,500 entries in 50.686 ms. Discovery now counts all enumerated entries before filtering, requires canonical direct children and rejects repeated paths, checks each real directory again before traversal and skips linked files as well as linked child directories. Existing scan caps retained. Generated ID collisions refuse preview/generation rather than silently discard a game or change established IDs. Three disposable/synthetic tests cover punctuation-derived ID collisions, duplicate and outside enumeration. This is bounded pathname validation, not handle-bound ancestor-race protection. CH-Rm deferred.

## Shared release ZIP inspection and PowerShell coverage

Reviewed the eight tracked PowerShell scripts/modules for filesystem/process mutation sites. The previous official Toolbox validator separately extracted the entire package, unlike the bounded baseline inspection. Consolidate ZIP index, metadata reads and bounded YAML text parsing in ReleaseValidation.psm1; both validators now inspect archives without extraction. Add archive/file byte budgets, duplicate/path/parent conflicts, special/link entry checks where framework attributes are available, and retain strict Toolbox required-file/manifest identity checks. Add traversal/duplicate/parent-conflict fixture tests and explicitly run the existing release validator PowerShell suite in Windows build CI. Legacy standalone manifest generation script and package staging cleanup still require further review. No Toolbox publish, feed or release mutation. CH-Rm deferred.

## Release-validator exit status and updated qualification

Verified 67e6ff98 Windows run 45351509513: 842 pass, zero failures/warnings, two opt-in skips, package and archive compatibility passed. Manifest discovery benchmark 54.056 ms for 2,500 packages/7,500 entries. Shared release inspection candidate 20e12dd5: all seven PowerShell test cases reported passing, yet CI step failed because expected missing-Toolbox child process exit status was not reset. Explicitly return exit 0 only after the existing failed-case count check; no failure suppression. Latest Windows qualification remains pending. CH-Rm deferred.

## Repeatable tracked-file audit

Add tools/audit-repository.ps1 and a read-only CI evidence artifact: exact checkout SHA, each tracked file size/hash, PowerShell AST parsing, XML well-formedness with external resolution/DTD prohibited, JSON parsing, distribution YAML subset parsing, and conservative lexical private-member triage across production C#, tests, XAML and scripts. Findings are candidates only, not deletion authority. All other YAML and binary/Markdown files are inventoried, not claimed fully parsed/reviewed. The generated report is written outside the checkout and not packaged or tracked. CI emits candidate names and counts for follow-up review; no user data or provider credentials consulted. CH-Rm deferred.

## Standalone manifest generator modernization

The CLI scripts retain existing flags and add a bounded rclone listing timeout (default 300 seconds, range 5–3600, wrapper forwards it). Replace sequential unbounded stdout/stderr reads with concurrent bounded-chunk consumption, suppress raw provider stderr and stop the child on failure best effort. Local enumeration is iterative, skips reparse children, rechecks directories and caps all scanned entries. CLI package membership uses ancestor lookups; identity collisions and importer item/UTF-8 size limits fail before publication. Manifest/report output path collision refuses; explicit -Overwrite permits staged replacement with unique sibling backup, not silent truncation. Preserve compatibility -Recurse switch (always recursive) while removing unused local variable. Add disposable CLI tests for dry run, generation, replacement backup and collision refusal. Real rclone remote behavior and process-tree containment remain separate gates. CH-Rm deferred.

## First tracked-file audit and dead wrapper cleanup

Run 45351838525 at 4f7edc19 completed the repository audit: 332 tracked files, 118 production C# files, four lexical private-member candidates, zero syntax/inventory failures. Candidate inspection: ParseManifest and ConvertManifestItemsToGameMetadata were unreferenced private wrappers superseded by ImportOutcomeService's direct parser/mapper calls; RefreshSetupStatusFromErrors and RefreshSetupStatusFromValidation were superseded by the live verification-report presentation. Removed all four after cross-file identifier evidence and inspection of active replacements; removed the latter wrapper's obsolete unique resource key. Preserve the public APIs and actual parser/mapper/report services. The NUnit release-validator wrapper was still hardcoded to exactly six PowerShell cases; update its contract to require at least seven successful cases, zero failures and exit zero, emit summary, and remove duplicate explicit CI invocation of the same suite. CLI NoReport preflight uses explicit target list. CI qualification pending. CH-Rm deferred.
