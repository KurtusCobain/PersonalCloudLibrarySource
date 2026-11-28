# PCLS internal engineering exit gate

**Status: OPEN — NO-GO for CH-Rm, owner acceptance or publication.** Isolated branch `audit/pcls-stabilization-20261124`. Internal candidate version 0.3.2.1. Do not install into an everyday profile. This is a qualification record, not a risk waiver or release authorization.

## Ordered assignments

| Assignment | Evidence available | Exit status |
|---|---|---|
| 01 Security | Original findings reconciled below; structural ZIP32 and terminal-reservation corrections qualified at 666a3af | OPEN: residual metadata/recovery and trust-boundary decisions; host qualification |
| 02 Modernization | All tracked files enumerated/hashed and syntax/compile contracts; new security-sensitive site index and reused stateless catalog resolvers | OPEN: exhaustive semantic reachability and compatibility review not established |
| 03 Performance | Existing deterministic preview, progress/reservation indexing, retention fixtures; new repeated 1k/10k catalog and 1024-entry ZIP workloads | OPEN: obtain exact latest measurements; real host/provider benchmarks and memory/latency limits |
| 04 Consolidation | Branch/PR/release inventory below, retired candidate publish workflow, pinned official Toolbox validation attempt, updated docs | OPEN: final CI, divergent-branch disposition, independent review prerequisites |

A green build and zero reported CodeQL findings are necessary, not sufficient. The owner has authorized these four assignments, not acceptance of a remaining high-risk finding.

## Security dispositions

Use the original [remediation register](independent-review-remediation-20261126.md) as the detailed evidence history. At exact 666a3af, Windows 45938798890 passed 1077 standard + 2 real-rclone tests, zero compiler warnings/failures; archive 45938798842 and CodeQL 45938798891 passed. Structural ZIP32 changes check local and central flags/method/raw name/CRC/length and signed/unsigned descriptors. Queue validates terminal outcome before reservation release.

| Original IDs | Engineering status | Remaining condition |
|---|---|---|
| P2/P4/P6/P7/P8/P9/P14/H3/H4/H6 | Concrete corrections or explicit compatibility contracts implemented | Independent verification and applicable host cases |
| P1 | Distinct candidate identity and unchanged public feed | Official Toolbox candidate gate plus migration |
| P3 | Destination capacity for local/ZIP; rclone unknown size remains best effort | Low-capacity and real UNC/provider fixture |
| P5 | Verified unpublished-stage cleanup only | Preserve old or uncertain evidence |
| P10 | Executor exception containment, authorization after reserve, outcome consistency before release | Exhaustive bookkeeping fault containment not proven |
| P11/G5/G9 | Legacy public process and directory APIs retained; hosted destructive directory behavior disabled | Semantic reachability/deprecation decision, not deletion based on textual search |
| P12 | Retire one-time publisher on candidate; original public main unchanged | Future release governance requires owner approval |
| P13 | Scoped external-source consent and lexical menu/report behavior | Real host/share and complete trust-boundary audit |
| H1/G6 | Native job containment, bounded pipes and unlisted inherited-handle test | Concurrent external process inheritance, not a sandbox |
| H2/H5 | Ambiguous paths refused and no-prompt rclone flags | Actual long-path and encrypted-config qualification |
| G1/G2 | Guarded ancestor/leaf verification, private writer locks, verified backups, no-replace native publication | Final closed-handle File.Replace pathname window and share/root semantics remain |
| G3/G4/G7 | Cooperative journal reservations and hosted deletion serialization; read-only restart evidence, fail-closed import/launch | Nonparticipants, power-loss and operator restart-release protocol unresolved |
| G8/G10 | Synthetic performance gates and pinned inputs | Host/provider performance; independent dependency/workflow assessment |

### Architecture decision required (not waived)

The current metadata replacement implementation preserves atomic File.Replace semantics and verified backups. It cannot express a native compare-and-replace of an exact target handle. Replacing it with two unjournaled renames would introduce a primary-record absence/crash window. Evaluate an immutable-generation or recoverable transaction protocol with migration and fault injection before claiming G1 closure, or obtain explicit independent acceptance of the constrained threat model. Do not silently switch the format, delete an uncertain file or overwrite an unknown record. No PCLS protection is a security boundary against arbitrary same-user code that can edit plugin binaries and user data.

Restart transfer markers intentionally cannot be cleared by a different journal instance. Any operator-release redesign needs exclusive verified identity, evidence that the originating transfer/process is stopped, destination and stage review, explicit action and durable disposition, plus crash/replay/SMB tests. A read-only review or absence of a stage is not release authority. Until qualified, retain the marker and refuse unsafe operations.

## Branch disposition inventory

Compared exact candidate 666a3af24b5f0acd3a2ee4b1ea8085041106490f against all 11 repository branch heads. Counts below are commits unique to the historical branch / commits behind that candidate, not numbers of missing features. No CH-Rm/fork accessed.

| Branch | Head | Unique / behind | Disposition |
|---|---|---:|---|
| main | d414e2f19375d3ba4a2142dd70a10aad323843d0 | 0 / 435 | Protected by operational policy; preserve public baseline |
| audit/pcls-stabilization-20261124 | 666a3af24b5f0acd3a2ee4b1ea8085041106490f | 0 / 0 | Working candidate at comparison time |
| audit/release-hardening-20261030 | 95db6786719314b188669554d6f9b6fdc67f8d2d | 0 / 452 | Ancestor; preserve |
| backup/pre-kubuntu-20260818 | db8f14d48c4a1bb820a699b61aee8c9232dcb8a1 | 1 / 942 | Backup: 2 historical research docs absent, 2 old feed files differ; retain |
| docs/v032-public-polish-20261101 | c051f3dfe63a1afb5581621ec4d08377df186024 | 13 / 435 | 11 changed paths, 9 exact matches, 2 evolved; retain draft #11 |
| feature/user-friendly-dashboard-cleanup | 1c59e0f4e344fc419c5fc7bb913ec09989d0632c | 0 / 583 | Ancestor; preserve |
| feature/v1-combined-integration-20261103 | e82a27862cb2cce10b1b635735c5de0467d978d7 | 0 / 171 | Ancestor and recovery point; draft #23 |
| feature/v1-multisource-identity-foundation-20261101 | 56664f96cb21625258b52aa4056240253b04c296 | 108 / 435 | All 40 changed paths present; 19 exact matches, others evolved; retain draft #22 |
| feature/v1-package-discovery-foundation-20261101 | 0c129df3cc60905cdf5b3c0438181efe1fed78e7 | 0 / 278 | Ancestor; draft #21 |
| fix/v033-implicit-file-size-20261101 | ce338ff9125740b5dd0083fae6b4118df431a7bc | 9 / 435 | All 8 changed paths present; 2 exact, others evolved; draft #15 |
| safety/v1-readonly-cleanup-review-20261101 | 552d9a61f3f2df3cbb240b53db5c1141a6e9aeef | 14 / 435 | All 9 changed paths present; 3 exact, others evolved; draft #16 |

All six open PRs (#11, #15, #16, #21, #22, #23) are draft at inspection. Branch list reported protected=false for all heads; no settings change is authorized. Exact blob matches establish byte identity, not semantic equivalence of evolved files. No merges, PR closures, branch/tag deletions or force updates. Five diverged branches remain preserved pending semantic reconciliation; the backup's research documents are not imported by default.

Published releases at inspection: v0.3.2 stable, v0.2.0 stable, v0.1.1 prerelease. Public v0.3.2 package digest `1781fe630fad9398b8e810e3cf843309acc68a8a3dcfba720873042da41b2ed7`. Tag-list endpoint was not available through the connector; release tags verified via releases endpoint only. No change to public assets, feed, add-on metadata, release request markers or main.

## Reproducible evidence

- [Windows 666a3af](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45938798890) and [archive](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45938798842), [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45938798891).
- The Windows audit report inventories 337 tracked files at 666a3af, 119 production C# files, zero syntax/inventory failures, zero private-member lexical candidates. This does not prove dead-code absence. Review the expanded source-reference index generated by subsequent CI.
- Candidate digest at 666a3af: `0F58A6423CB9973408039D22E4AC69A49C458F616E65C3BE7C2C8E81C0B76F34`. Digest applies only to that exact build artifact.
- Existing performance workload at 666a3af: synthetic 2,500-package/7,500-entry preview 81.052 ms; 2,000 progress queries with 5,000 history jobs 228.727 ms scan / 0.456 ms indexed; reservation queries 168.858 / 0.154 ms; 550 observed transfers, 518 retained, 9.579 ms. These are hosted synthetic measurements, not real host throughput.
- [Expanded audit and resolver modernization](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45939120636): 337 tracked files, 119 production C# files, 25,735 physical source lines, 69 indexed reference sites, zero lexical candidates or inventory failures.
- [New catalog and ZIP benchmark run](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45939161676) at e3557ac: 1080 standard + 2 real-rclone passing, zero compiler warnings/failures, archive compatibility passed. Median 1k parse 2.740 ms, map 39.597 ms; median 10k parse 33.304 ms, map 412.581 ms. Median 1024-entry / 154388-byte ZIP review 15.356 ms; no extraction. Preview 108.650 ms; 2k progress queries 149.946 / 0.348 ms scan/indexed, reservations 109.900 / 0.124 ms; 550-transfer retention 8.903 ms. Candidate digest 20D6D1F89F7F35FF16AD8DF439705AD34CDD3765E3D744B3BD605397568BDD15. First retained-managed-memory samples were roughly 0.40 MB (1k) and 4.11 MB (10k); later samples were contaminated by prior object lifetime, so do not report their deltas as comparable. Correct benchmark stack lifetime in follow-up and requalify.
- Playnite Toolbox 10.63 asset pinned from [official release](https://github.com/JosefNemec/Playnite/releases/tag/10.63), size 133044541 bytes, SHA-256 9AF9A0DB330C97879697BEAC9F819C16F9F544E712261A83FA4E62A8F9A2C787. Candidate pack/unchanged feed check only. CI outcome pending.
- CodeQL local SARIF gate covers all reported results; permission/runtime warnings must be recorded. Zero reported findings does not mean no vulnerabilities.
- [Disposable host acceptance worksheet](../tests/stabilization-host-acceptance-20261125.md) remains NOT RUN.

## Continuation evidence — 2026-11-27

Exact prior checkpoint `4a91826bb40d2ad30bba917513306f4dc2e60908` is preserved and all three push workflows completed successfully: [Windows](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45939431077), [archive compatibility](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45939431097), [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45939431113). Windows log: 1081 standard + 2 real-rclone passing, zero compiler warnings/failures. Official Playnite Toolbox 10.63 candidate qualification passed (unchanged public feed only). Corrected retained-memory samples were 399276/399256/399256 bytes for 1k entries and 4111552/4111576/4111576 bytes for 10k entries; median parse/map 3.741/53.940 ms and 46.876/550.488 ms respectively. These are synthetic managed-memory deltas, not peak memory or host throughput.

### Assignment 01 additional corrections

- `0cbaa394`: require post-replacement target identity to match the verified stage and backup identity to match the inspected original, in addition to content verification.
- `0656a483`: retain and enforce the verified stage identity in SafeFileWriteService.
- `be17840c8264c8fd0e125d1ae786180fecbeabe7`: Windows regression qualifies replacement and backup identity. [Microsoft ReplaceFile contract](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilea). [Windows](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46104965786) completed successfully: 1082 standard + 2 real-rclone, zero failures/compiler warnings, candidate Toolbox PASS; [archive](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46104965782) PASS; [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46104965795) pending at inspection.
- `1b6e68842a455d6cb2b07feb482f95bdbf593c20`: independently set manager quarantine now overrides a conflicting ordinary executor result before reservation completion; the journal independently refuses release for quarantined jobs. Two targeted tests cover the queue and direct journal boundary. [Windows](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46105142018), [archive](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46105141998), [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46105141996) started; outcomes not yet qualified at inspection.

Identity verification detects substitution after replacement; it does NOT close the closed-handle pathname race. Retain G1/G2 as open. The quarantine correction prevents an ordinary result from releasing known uncertain evidence; it does NOT authorize release of an orphaned reservation by a new journal instance. Retain G3/G4/G7 as open. No storage-format change or restart-release path implemented. Require migration/rollback and fault-injection qualification before adopting a new persistent protocol. In particular a missing stage, process ID alone, or successful read-only review is insufficient evidence of safe release. Legacy markers must continue to fail closed.

### Assignment 04 historical branch reconciliation

At exact `be17840c`, all five historical divergent heads were re-compared; their unique counts remain 1/13/108/9/14 respectively, with every head retained. The candidate was 949 commits ahead of the backup merge base, 442 ahead of the common public baseline for the other four branches. Reviewed the following branch changes against candidate source rather than merging old code:

| Historical branch | Disposition of its changes |
|---|---|
| docs/v032-public-polish-20261101 (#11) | 9/11 exact blob matches; the two evolved paths (README and external-link-checks) retain the UI tour, Fullscreen caveat, roadmap link and issue references. No additional import indicated. |
| fix/v033-implicit-file-size-20261101 (#15) | InstallDirectory/working-directory separation, exact owned file bytes, mapper/controller integration and corresponding shared-cache/explicit-directory regressions present. The current ResolveGame additionally preserves the real play action working directory. ADR and host worksheet preserved; native Playnite size/migration and UX trade-off remain open. No old code import indicated. |
| safety/v1-readonly-cleanup-review-20261101 (#16) | 3/9 exact blob matches; both project registrations and the added regression lines retained. Evolved diff service requires HasDetailedEntries and matching counts; scanner retains detailed read-only observations while adding bounded handle-based hashing and added-save rules. Automatic deletion remains forbidden. No additional import indicated by this focused review. |
| feature/v1-multisource-identity-foundation-20261101 (#22) | 19/40 exact blobs, all 21 evolved paths present; semantic review of evolved integration, stores, settings, tests and localization remains open. |
| backup/pre-kubuntu-20260818 | Preserve as historical backup; no blind import of research or historical public feed files. |

These are targeted branch-change dispositions, not proof of whole-codebase semantic completeness. No merge, PR closure, branch deletion, CH-Rm inspection, release or owner acceptance has occurred. No native Windows test host available in this session; hosted CI is the available build/test authority.

## Continuation evidence — 2026-11-28

Exact checkpoint `8d749bc794f743e4740e9e445141514b9809adfb` passed all three push workflows: [Windows](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46105180061), [archive](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46105180024), [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46105180052). The Windows raw log confirms 1084 standard + 2 opt-in real-rclone tests passed, zero failures and compiler warnings, and official Toolbox candidate qualification passed. The prior recovery-quarantine commit `1b6e688` independently passed all three workflows. Synthetic retained memory remains approximately 399256 bytes for 1k and 4111576 bytes for 10k entries; timing varies between hosted runners and is not a regression threshold.

### Additional hardening

At `9f4f64d6a1d00d5882ff0e44e6b52d95f471d591`, transfer recovery rejects an empty game GUID before reservation creation and treats a persisted empty game GUID as malformed, preserving evidence and refusing unrelated launch/reservation. Private record replacement validates all raw paths, normalizes them once, and rejects any pair of case-insensitive canonical sibling aliases before validation or staging. Five new regression cases; [Windows](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46116705021), [archive](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46116705014), [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46116705069) in progress at inspection. These are defensive improvements, not closure of the residual metadata transaction or restart release architecture.

### Historical branch disposition follow-up

Compared the 40 changed paths in multisource branch `56664f9` against `8d749bc`. 19 exact blob matches; all 21 evolved paths present. All added nonblank lines of the old project registrations, localization, settings migration, settings data, settings view model, workflow test and three persistence test files are present. Reviewed the changed controller factory (current source/plan guards, scoped receipt identity, ZIP protection and explicit external-source confirmation), profile management service (baseline-locked save and explicit backup preview/restore), and profile/display/catalog persistence deltas (bounded handle reads, exclusive writer locks, verified stage publication replacing older pathname cleanup). The identity match now copies display entries once rather than twice. The remaining old plugin route and controller changes are integrated in evolved code; the older documentation link references appear in newer dated verification sections. No additional wholesale import indicated by this focused review; not a complete semantic equivalence proof. Preserve all branch heads and drafts.

The sole unique backup commit at `db8f14d` adds two historical research reports (playnite-library-addon-research.md and romcade-audit-report.md) and changes two outdated public feed manifests; it contains no additional production source. Preserve it in its historical branch, do not import obsolete feed or unreviewed research. Internal branch-reconciliation disposition is now recorded for all five divergent heads; destructive retirement remains separate.

See [persistent transaction and restart-recovery protocol design](persistent-transaction-recovery-design.md) for the remaining engineering constraints. It is a design, not implemented closure or release approval.

## Exit authority

Freeze an exact candidate only after all four assignments have documented dispositions and the complete CI matrix passes on the same commit. Remaining risks must be corrected or explicitly adjudicated, not silently accepted by the implementation agent. Preserve source and artifacts, then obtain separate approval for CH-Rm, final independent reviews, disposable host acceptance and release.
