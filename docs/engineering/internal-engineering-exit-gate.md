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
| feature/v1-multisource-identity-foundation-20261101 | 56664f96cb21625258b52aa4056240253b04c296 | 108 / 435 | All 40 changed paths present; 20 exact matches, others evolved; retain draft #22 |
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
- New catalog and archive measurements: see subsequent exact build logs; do not infer results before successful execution.
- Playnite Toolbox 10.63 asset pinned from [official release](https://github.com/JosefNemec/Playnite/releases/tag/10.63), size 133044541 bytes, SHA-256 9AF9A0DB330C97879697BEAC9F819C16F9F544E712261A83FA4E62A8F9A2C787. Candidate pack/unchanged feed check only. CI outcome pending.
- CodeQL local SARIF gate covers all reported results; permission/runtime warnings must be recorded. Zero reported findings does not mean no vulnerabilities.
- [Disposable host acceptance worksheet](../tests/stabilization-host-acceptance-20261125.md) remains NOT RUN.

## Exit authority

Freeze an exact candidate only after all four assignments have documented dispositions and the complete CI matrix passes on the same commit. Remaining risks must be corrected or explicitly adjudicated, not silently accepted by the implementation agent. Preserve source and artifacts, then obtain separate approval for CH-Rm, final independent reviews, disposable host acceptance and release.
