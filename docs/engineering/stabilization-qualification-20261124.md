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

| [301cb214](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45356172933) | 875 pass, 0 fail, 2 skips; 0 warnings | Batched history retention, completion ownership, retry fences and quarantine; archive passed |
| [9d563062](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45356246805) | 876 pass, 0 fail, 2 skips; 0 warnings | Automatic retention fixture over 550 transfers; archive passed |

| [a39b9bb8](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45356605279) | 878 pass, 0 fail, 2 skips; 0 warnings | Bounded production diagnostic pipes; archive and CodeQL passed |
| [bff98885](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45490556214) | 880 pass, 0 fail, 2 skips; 0 warnings | Exclusive Windows staging creation and local publication recheck; archive passed |
| [ea307bbd](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45490662884) | 882 pass, 0 fail, 2 skips; 0 warnings | Quarantine ambiguous new staging entries and rclone publication conflicts; archive passed |
| [8ca5548e](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45490786306) | 883 pass, 0 fail, 2 skips; 0 warnings | Native ZIP file identity and handle-bound rollback; archive passed |
| [b21a84c4](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45490884898) | 885 pass, 0 fail, 2 skips; 0 warnings | ZIP hardlink refusal and manual-attention propagation; archive passed |

| [106c0166](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45491005760) | 887 pass, 0 fail, 2 skips; 0 warnings | Local single-file identity and handle rollback |
| [12b2b455](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45491261571) | 890 pass, 0 fail, 2 skips; 0 warnings | Local exact tree and nonrecursive rollback; archive and CodeQL passed |
| [3c77cef9](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45491672082) | 892 pass, 0 fail, 2 skips; 0 warnings | Local content digest; archive and CodeQL passed |
| [e4144e22](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45491757987) | 894 pass, 0 fail, 2 skips; 0 warnings | Local native directory identity; archive and CodeQL passed |
| [4b58f8fd](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45491961604) | 896 pass, 0 fail, 2 skips; 0 warnings | ZIP directory identity; archive passed |
| [caa08b87](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45492207582) | 898 pass, 0 fail, 2 skips; 0 warnings | Native launch-time job containment and disposable process-tree tests; archive passed |
| [a1bddd0a](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45492342444) | 898 pass, 0 fail, 2 skips; 0 warnings | Contained manifest read; archive and CodeQL passed |
| [3c79cfa7](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45492512888) | 899 pass, 0 fail, 2 skips; 0 warnings | Handle-based file-only uninstall; archive and CodeQL passed |
| [a8156d35](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45492668561) | 900 pass, 0 fail, 2 skips; 0 warnings | Native no-replace source-handle publication; archive and CodeQL passed |
| [aa902ac0](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45492796397) | 900 pass, 0 fail, 2 skips; 0 warnings | Bounded contained settings connection test; archive passed |
| [4a7b644c](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45493002705) | 902 pass, 0 fail, 2 skips; 0 warnings | Originating marker identity and handle completion; archive passed |
| [9e5bcceb](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45493158781) | 904 pass, 0 fail, 2 skips; 0 warnings | rclone root identity and nonempty directory quarantine; archive passed |

| [d1e68d99](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45493288423) | 907 pass, 0 fail, 2 skips; 0 warnings | Post-exit rclone integrity snapshot; archive passed |
| [b0e594f8](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45493407725) | 907 standard pass, 0 fail, 2 standard skips; 0 warnings; **2/2 separate real-rclone cases passed** | Official pinned v1.76.0 isolated local-only gate; archive passed |

| [d1d74d74](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45493710010) | 783 pass, 125 fail, 2 skips | Parent-relative publication experiment failed; withdrawn |
| [3b363641](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45493793192) / [bf51353f](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45493860206) | Windows builds failed | Inherited failing publication candidate; not qualified |
| [dd632146](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45494210494) | 911 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Restore known-good source-handle rename, retain identity guard; no-follow journal lock and concurrency fixtures pass; archive passed |
| [1ca69782](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45494280209) | 911 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Reuse local digest buffers; archive passed |

| [2d4d0f03](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45494395197) | 912 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Partial reservation write quarantine; archive passed |
| [c0ca3dc3](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45494467679) | 913 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Timed local tree correctness fixture; archive passed |

| [72e2e5eb](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45494757069) | 915 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Malformed-record fixes; archive and CodeQL passed |
| [19b1a045](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45650590082) | 916 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Marker-origin diagnostics; archive passed |
| [351cb029](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45650653485) | 916 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Localized recovery dialog; archive passed |

| [33fd1f1e](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45650769653) | 918 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Legacy identity-based directory removal; archive passed |
| [e8b45e1b](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45650873433) | 920 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Bounded durable receipt publication and hardlink refusal; archive passed |

| [e755fe37](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45651045272) | 923 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Inventory hardlink refusal and streamed V2 fingerprints; archive and CodeQL passed |
| [864413e1](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45705832268) | 923 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Recovered documentation commit; archive passed |
| [52559eb8](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45705932730) | 924 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Shared private record I/O and save baselines; archive passed |
| [50fc9553](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45705985565) | 926 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Launcher approval identity and handle revocation; archive passed |
| [cca22024](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45706038463) | 926 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Shared receipt record I/O; archive passed |
| [ca72b7fb](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45706132013) | 930 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Recovery journal and cleanup footprint restore guards; archive passed |

| [bccf7de6](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45706265582) | Build failed: missing test assembly reference | Not qualified; corrected subsequently |
| [dd5396f4](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45706450144) | 932 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Permanent cleanup manifest/intent corrections; archive and CodeQL passed |
| [714e4553](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45728294980) | 933 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Root-identity-bound recovery moves; archive passed |
| [9470e4f6](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45728338257) | 935 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Cancellable lifecycle lock and contention fixtures; archive passed |

| [e8c9637c](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45728648267) | 940 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Orphaned recovery evidence, host approval lifecycle and recovery tracking; archive and CodeQL passed |

| [e811b2fd](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45729138805) | 940 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Bounded release package payload inspection; PowerShell regressions and archive passed |
| [31fd0118](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45729237147) | 942 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Extraction receipt-footprint and post-consent guards; archive passed |

| [21494709](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45729454199) | 945 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Durable ZIP reservation overlap and uncertain staging; archive and CodeQL passed |
| [43729f39](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45729686122) | 947 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Same-handle archive consumption and hardlink compatibility; archive and CodeQL passed |
| [dd666059](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45732050542) | 947 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Private profile/catalog reads and create-only publication; archive and CodeQL passed |
| [a56eedfd](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45732100449) | 950 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Three hardlink regressions; archive passed |
| [cfad7bbd](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45732187988) | 951 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Manifest/report staging and backup identity; archive passed |
| [3290bb85](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45732251315) | 952 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | 1,024-file mixed-size qualification; archive passed |

| [2bc8a11c](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45732388090) | 954 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | Stable local catalog reads and UTF-16 BOM fixture; archive passed |
| [b090230d](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45732477687) | 954 standard pass, 0 fail, 2 standard skips; 0 warnings; 2/2 real-rclone | CLI linked output refusal regression; archive passed; exact CodeQL pending at this entry |

### Review freeze

Code revision `b090230dea6c9d5832dee3e222b579c7e84dd154` is the proposed pre-review freeze; Windows and archive passed. [Exact CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/45732477534) pending at this entry. [Review handoff](review-handoff-20261125.md), [host evidence checklist](../tests/stabilization-host-acceptance-20261125.md). Documentation-only commits may follow, but reviewers must examine the same code revision.
- Later CodeQL conclusions must be read against the exact candidate SHA; no success assumed.

Windows build and CodeQL workflow conclusions are not proof of absence of vulnerabilities or native Playnite acceptance. A passing archive compatibility workflow only qualifies the isolated decoder probe, not shipping 7z/RAR install behavior. Review current run evidence by exact commit.

## Performance evidence

- 5,000 historical jobs, 4 active, 2,000 progress reads: reference 294.432 ms / indexed 0.675 ms at c6270bf2; 225.171 ms / 0.483 ms at 0ed191f0. Exact byte totals matched.
- 2,500 synthetic packages / 7,500 synthetic entries: 55.771 ms at d13d0ed7. Exact item count, identity uniqueness and launch priority checked.
- 5,000 historical jobs, six reserved, 2,000 reservation snapshot reads: 108.283 ms reference / 0.120 ms indexed at 14e7ff8d, identical counts.
- 550 sequential synthetic successful transfers, 518 history entries retained, 18.585 ms at 9d563062. Claimed completion remains valid and live reservations are empty. This is not real download throughput.
- At c0ca3dc3, 256 files/3,145,728 bytes (including 64 empty files), 16 source subdirectories: 1,476.386 ms end-to-end verified copy on hosted Windows. Content and membership assertions passed. One run, no pre/post comparison.
- At 3290bb85, 1,024 files / 73,400,320 bytes, 32 source subdirectories plus one empty directory: 8,275.645 ms end-to-end verified copy on hosted Windows. Independent full-byte and membership assertions passed. Same fixture repeated at 2bc8a11c (10,327.268 ms) and b090230d (8,414.382 ms). These are three hosted runs of one synthetic workload, not a comparative speedup claim.
- These are hosted synthetic measurements, not general application speed, actual disk/NAS throughput, startup latency or memory certifications.

## Remaining gate register

| Gate | State and required evidence |
|---|---|
| Main/tag protection and release authorization | Recommendations documented; owner approval required for repository settings |
| Consolidation | All ten original tips compared against e8c9637c; five ancestral, four diverged active branches have no absent changed paths, historical research/feed intentionally retained on backup. Targeted old-only line review of remaining overlapping paths and preserved test behaviors documented; independent semantic confirmation remains, no branch closure/deletion |
| Semantic source audit | Structural compile inventory and targeted repairs complete; comprehensive member/reference analysis and scripts/configuration review incomplete |
| Security | Windows fixtures qualify local/ZIP file and directory identities, local/ZIP no-replace source-handle publication, rclone whole-job launch containment, originating marker identity, and rclone root ownership. Post-exit rclone integrity and separate 2/2 real binary gate passed. Parent-relative publication experiment failed and was withdrawn; source-handle no-replace rename plus source identity check restored and verified. No-follow journal lock/concurrency, bounded receipt writes and legacy nonrecursive identity-based directory deletion qualified. Shipping directory uninstall remains disabled. Inventory hardlink refusal and streamed fingerprints qualified. Shared bounded private record I/O and recovery finalization barriers qualified on Windows CI. Permanent cleanup manifest and replay safeguards qualified; host cleanup remains disabled. Root-identity-bound reversible moves and cooperating recovery/cleanup lifecycle lock qualified; orphaned evidence and host approval/overlap guards qualified on Windows CI. Extraction lifecycle/receipt changes qualified on Windows CI; durable ZIP/queue reservation and exact archive source handles qualified; private profile/catalog record hardlink refusal and manifest/report staging qualification passed; native host acceptance remains open. Full ancestor pinning, create-to-open race, directory/member publication transaction, receipt-bound uninstall, further malformed journal and power-loss recovery remain open. No automatic permanent ZIP cleanup or recovery release. |
| Transfer history | Soft cap verified in Windows fixtures: 512 ordinary terminal attempts, batched pruning, protected unclaimed results/latest retry/fences/quarantine; 550-transfer fixture passed. Native host acceptance pending; protected history may exceed cap. |
| Runtime settings and retry | Native Playnite database reconciliation and stale controller/action cases require qualification |
| Recovery | Originating marker identity enforced; restarted instances remain read-only for completion. Nonempty rclone directory failures/cancellations preserve staging and quarantine the job. Read-only marker-origin diagnostics and localized dialog qualify on Windows CI. Explicit recovery release remains blocked by persistent process/ownership evidence, ancestor identity, and native host presentation. |
| Multi-source | Experimental activation and isolated identity routing; verify real host import/refresh/migration and source disappearance |
| Archive support | Shipping ZIP path only; 7z/RAR decoder is isolated compatibility evidence; do not claim full support |
| UI | Localized resource audit passed; keyboard/controller, DPI, theme and genuine screenshot review pending |
| Performance | Synthetic progress, preview and retention measurements recorded; filesystem identity checks need local/NAS compatibility and large-tree cost qualification. Local digest buffer reuse qualified; 256-file timed correctness fixture passed; 1,024-file 70 MiB mixed workload passed; comparative memory/throughput measurements and actual host/NAS performance remain pending. Cleanup manifest construction now uses matching detailed inventory rather than redundant scans; correctness qualified, comparative measurement pending. Actual library startup, memory and cancellation under I/O faults pending. |
| Independent review | Grok/Claude review of a frozen internal candidate, resolve findings before acceptance |
| Native acceptance | Disposable Windows Playnite upgrade/install/uninstall/reinstall, digest-pinned real-rclone fixture gate (2/2 passed on hosted Windows), safe provider fixtures, official Toolbox and package provenance |
| CH-Rm | Deferred until internal PCLS remediation and review are complete; selective review only, with independent regression after any eventual port |
| Publication | Separate owner approval after all required gates; no feed or release mutation |

Never install test artifacts into an actual library as a substitute for disposable host qualification. Do not delete preserved staging or ownership evidence to force a successful retry.

For public users, see [release status](../release-status.md) and [published limits](../known-limits.md).
