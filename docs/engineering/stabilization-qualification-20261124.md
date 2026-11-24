# PCLS stabilization qualification — November 24, 2026

**NO-GO for publication.** This is a development-only qualification index, not the public release notes. Stable v0.3.2 and its installer feed remain unchanged. The owner has authorized isolated remediation, not main merges, repository settings changes, release publication, original branch deletion, or production installation.

## Verified checkpoints

| Commit | Windows result | Scope |
|---|---|---|
| [fb0ee526](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45350200562) | 829 pass, 0 fail, 2 opt-in skips; 0 compiler warnings; package inspection | Framework reference reduction, recovery presentation, ownership guards; archive and CodeQL workflows also succeeded |
| [c6270bf2](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45350511001) | 831 pass, 0 fail, 2 opt-in skips; 0 warnings; package inspection | Live progress index and benchmark; archive and CodeQL workflows also succeeded |
| [0ed191f0](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45350761872) | 834 pass, 0 fail, 2 opt-in skips; 0 warnings; package inspection | Queue shutdown, reservation lifetime; archive compatibility passed |
| [d13d0ed7](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45350964498) | 836 pass, 0 fail, 2 opt-in skips; 0 warnings; package inspection | Manifest ancestor indexing; archive compatibility passed |
| [8e276f7f](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45351094785) | 838 pass, 1 fail, 2 skips | Standalone test reached host serializer before output-path refusal; corrected in e0c23dcf; subsequent correction verified |
| [e0c23dcf](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45351269054) | 839 pass, 0 fail, 2 skips; 0 warnings; package inspection | Backup preservation, output size/collision protection; archive gate passed |
| [67e6ff98](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45351509513) | 842 pass, 0 fail, 2 skips; 0 warnings; package inspection | Manifest ID collision and enumeration safeguards; archive gate passed |
| [20e12dd5](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45351650417) | PowerShell: 7 pass, 0 fail; CI step failed | Expected missing-Toolbox child exit code leaked into runner; explicit final status correction pending CI |

| [b2c70ea0](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45352566934) | 842 pass, 0 fail, 2 skips; 0 warnings | Shared package validation, standalone generator, 333-file audit; package/archive and script tests passed |
| [8e333953](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45352797504) | 845 pass, 0 fail, 2 skips; 0 warnings | Read-only recovery journal review |
| [c41279b3](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45352956891) | 847 pass, 0 fail, 2 skips; 0 warnings | Legacy controller route freshness |
| [e6b13de0](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45353149994) | 854 pass, 0 fail, 2 skips; 0 warnings | Remote operand validation and file-only staging cleanup |
| [ee96a5b9](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45353276912) | 856 pass, 0 fail, 2 skips; 0 warnings | Worker and retry execution guard |
| [b17f6a75](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45353470187) | 858 pass, 0 fail, 2 skips; 0 warnings | Hosted retry intent and fresh controller routing |
| [14e7ff8d](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45353612000) | 859 pass, 0 fail, 2 skips; 0 warnings | Reservation indexing and benchmark |
| [a72d780e](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45354060663) | 862 pass, 0 fail, 2 skips; 0 warnings | Transactional catalog generation |
| [8acd241f](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45354166828) | 863 pass, 0 fail, 2 skips; 0 warnings | Committed dashboard state; archive and CodeQL passed |
| [06543920](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45355357537) | Debug test compilation failed | Internal-method access in fixture; corrected at 38c15dcf; not qualified |
| [38c15dcf](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45355426471) | 866 pass, 0 fail, 2 skips; 0 warnings | Superseded retry refusal; archive and CodeQL passed |
| [b1630ffc](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45355538787) | 867 pass, 0 fail, 2 skips; 0 warnings | Recovery path observations; archive and CodeQL passed |
| [2b5dbd3f](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45355721004) | 868 pass, 0 fail, 2 skips; 0 warnings | Superseded retry-context release; archive passed |
| [33f53ba6](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45355786366) | 869 pass, 0 fail, 2 skips; 0 warnings | Stable attempt order and manager retry guard; archive passed |
| [9f273e80](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45355888084) | 872 pass, 0 fail, 2 skips; 0 warnings | Ambiguous launch and late setup cancellation; archive passed |

### Pending candidates (not qualified at this ledger entry)

- [301cb214](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45356172933): batched in-memory history retention; tests for completion ownership, retry fences and quarantine.
- [9d563062](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45356246805): additional 550-transfer automatic-pruning fixture.
- Later CodeQL conclusions must be read against the exact candidate SHA; no success assumed.

Windows build and CodeQL workflow conclusions are not proof of absence of vulnerabilities or native Playnite acceptance. A passing archive compatibility workflow only qualifies the isolated decoder probe, not shipping 7z/RAR install behavior. Review current run evidence by exact commit.

## Performance evidence

- 5,000 historical jobs, 4 active, 2,000 progress reads: reference 294.432 ms / indexed 0.675 ms at c6270bf2; 225.171 ms / 0.483 ms at 0ed191f0. Exact byte totals matched.
- 2,500 synthetic packages / 7,500 synthetic entries: 55.771 ms at d13d0ed7. Exact item count, identity uniqueness and launch priority checked.
- 5,000 historical jobs, six reserved, 2,000 reservation snapshot reads: 108.283 ms reference / 0.120 ms indexed at 14e7ff8d, identical counts.
- These are hosted synthetic measurements, not general application speed, actual disk/NAS throughput, startup latency or memory certifications.

## Remaining gate register

| Gate | State and required evidence |
|---|---|
| Main/tag protection and release authorization | Recommendations documented; owner approval required for repository settings |
| Consolidation | Original branches retained; PR #22 overlap review recorded in ledger; review remaining unique work and preserve history |
| Semantic source audit | Structural compile inventory and targeted repairs complete; comprehensive member/reference analysis and scripts/configuration review incomplete |
| Security | Ambiguous rclone launch and late cancellation hardened; no automatic permanent ZIP cleanup. Verify filesystem replacement races, hardlinks, process-tree containment, malformed journal and power-loss recovery. |
| Transfer history | Soft-cap candidate implemented: 512 ordinary terminal attempts, batched pruning, protected unclaimed results/latest retry/fences/quarantine; exact candidate CI and native host acceptance pending. Protected history may exceed cap. |
| Runtime settings and retry | Native Playnite database reconciliation and stale controller/action cases require qualification |
| Recovery | Persistent reservation refusal and read-only path observations implemented and regression-tested; explicit manual resolution remains blocked by process/ownership/handle identity evidence and native host presentation. |
| Multi-source | Experimental activation and isolated identity routing; verify real host import/refresh/migration and source disappearance |
| Archive support | Shipping ZIP path only; 7z/RAR decoder is isolated compatibility evidence; do not claim full support |
| UI | Localized resource audit passed; keyboard/controller, DPI, theme and genuine screenshot review pending |
| Performance | Synthetic progress and preview measurements recorded; filesystem, actual large libraries, startup, memory and cancellation under I/O faults pending |
| Independent review | Grok/Claude review of a frozen internal candidate, resolve findings before acceptance |
| Native acceptance | Disposable Windows Playnite upgrade/install/uninstall/reinstall, real-rclone opt-in, safe provider fixtures, official Toolbox and package provenance |
| CH-Rm | Deferred until internal PCLS remediation and review are complete; selective review only, with independent regression after any eventual port |
| Publication | Separate owner approval after all required gates; no feed or release mutation |

Never install test artifacts into an actual library as a substitute for disposable host qualification. Do not delete preserved staging or ownership evidence to force a successful retry.

For public users, see [release status](../release-status.md) and [published limits](../known-limits.md).
