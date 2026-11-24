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

Windows build and CodeQL workflow conclusions are not proof of absence of vulnerabilities or native Playnite acceptance. A passing archive compatibility workflow only qualifies the isolated decoder probe, not shipping 7z/RAR install behavior. Review current run evidence by exact commit.

## Performance evidence

- 5,000 historical jobs, 4 active, 2,000 progress reads: reference 294.432 ms / indexed 0.675 ms at c6270bf2; 225.171 ms / 0.483 ms at 0ed191f0. Exact byte totals matched.
- 2,500 synthetic packages / 7,500 synthetic entries: 55.771 ms at d13d0ed7. Exact item count, identity uniqueness and launch priority checked.
- These are hosted synthetic measurements, not general application speed, actual disk/NAS throughput, startup latency or memory certifications.

## Remaining gate register

| Gate | State and required evidence |
|---|---|
| Main/tag protection and release authorization | Recommendations documented; owner approval required for repository settings |
| Consolidation | Original branches retained; PR #22 overlap review recorded in ledger; review remaining unique work and preserve history |
| Semantic source audit | Structural compile inventory and targeted repairs complete; comprehensive member/reference analysis and scripts/configuration review incomplete |
| Security | No automatic permanent ZIP cleanup; verify filesystem replacement races, hardlinks, process-tree containment, malformed journal and power-loss recovery |
| Transfer history | Session history still grows; design bounded retention without dropping unresolved evidence or latest retries |
| Runtime settings and retry | Native Playnite database reconciliation and stale controller/action cases require qualification |
| Recovery | Persistent reservation refusal implemented; read-only restart presentation and explicit manual review still required |
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
