# PCLS — disposable Windows host acceptance evidence worksheet

**NOT EXECUTED. Run only after independent review findings are resolved and the owner authorizes disposable host acceptance.** This worksheet is not a release approval. The proposed pre-review code freeze is b090230dea6c9d5832dee3e222b579c7e84dd154; replace it in the evidence header with the exact post-review candidate. See [review handoff](../engineering/review-handoff-20261125.md) and [acceptance sequence](../engineering/v1-code-complete-review-acceptance.md).

## Evidence header (fill before running)

| Field | Recorded value |
|---|---|
| Exact reviewed commit and corrected candidate SHA | |
| Windows version and architecture | |
| Playnite version, Desktop/Fullscreen | |
| Filesystem, local drive / disposable UNC/NAS | |
| Candidate package SHA-256 | |
| Backup/profile location (redact when sharing) | |
| Rclone version and isolated config type | |
| Independent review reports and resolved findings | |
| Test operator and date | |

Use a fresh checkout of the approved SHA, a fresh portable/disposable Playnite profile, separate fixture source/cache/plugin data and **no personal game files, credentials, saves or existing production Playnite installation**. The historical pre-review freeze retained the public version; the post-review internal candidate now uses version `0.3.2.1` but retains the public extension ID for migration testing. It can still upgrade a real installation, so never install it into your everyday profile. Use harmless text/binary fixture files; for actual Play tests choose a known trusted disposable executable that can be closed immediately, not a random archive. Do not execute fake fixture scripts or unknown installers.

## Prerequisites

- [ ] Both independent reviews returned, findings triaged, blockers/high risks resolved or explicitly deferred by owner with disabled functionality.
- [ ] Exact final Windows, archive and CodeQL workflows checked, no unresolved failing gate.
- [ ] Fresh Debug/Release build and NUnitLite results recorded; run the release and standalone generator contracts and package inspection using [DEVELOPMENT.md](../../DEVELOPMENT.md).
- [ ] Official Playnite Toolbox candidate validation (`-Candidate`) run for exact local package and metadata; public feed verification recorded separately. Missing executable or inaccessible prerequisite is BLOCKED.
- [ ] Record package digest with `Get-FileHash -Algorithm SHA256 -LiteralPath <candidate package>`.
- [ ] Separate copies of public v0.2.0 and v0.3.2 disposable profiles with known IDs and hashes for upgrade tests.
- [ ] Record expected fixture hashes and source/cache inventories before each mutation. Never change actual cloud data.

## Test matrix

Record PASS, FAIL, BLOCKED or NOT RUN in the result column. Evidence should name the fixture, observation, screenshot/log or recorded hash. A passing unit test does not fill a host row.

| ID | Scenario | Required observed behavior | Result / evidence |
|---|---|---|---|
| A01 | Clean Desktop setup, cancel then finish | Cancel does not persist partial settings; chosen provider/cache survive restart | |
| A02 | LocalFile valid catalog import and refresh | Exact stable unique game IDs, no duplicates or unexpected removal | |
| A03 | LocalFolder manifest generation and preview | Preview read-only; generated JSON imports; overwrite preserves prior backup; no private data leak | |
| A04 | Local source unavailable, corrupt/oversize or actively written manifest | Safe refusal; no unintended catalog deletion or mutation; recovery after source restored | |
| A05 | File-type install into shared cache | Correct source/cache hash; exact per-file installed size, launch path, working directory | |
| A06 | Larger unrelated sibling and Calculate Install Size | Tiny game size remains correct; record missing/disabled Open installation folder as trade-off, not automatic PASS | |
| A07 | Directory install with empty directories and mixed files | Membership, hashes and metadata correct; source retained | |
| A08 | File-only uninstall/reinstall | Only explicitly authorized disposable cache file removed; sibling and source preserved | |
| A09 | Directory uninstall action | Automatic directory deletion refused/disabled; all saves and files remain | |
| A10 | Local transfer cancel, retry, parallel overlap | Staging isolated; cancelled job stable; conflicting destination refused; fresh retry only after safe completion | |
| A11 | Rclone isolated local fixture install, slow transfer/cancel/retry | Real binary and isolated config verified; no personal remote; no sidecars or unresolved process mistaken for success | |
| A12 | Host exit/restart during controlled disposable transfer | Journal evidence retained; conflicting new work refused; read-only recovery dialog distinguishes unknown origin; no auto release | |
| A13 | Low disk / destination collision / locked output | Refusal without overwrites or source mutation; uncertain staging retained | |
| A14 | Linked cache/source/staging and hardlinked private evidence | Refuse unsafe operations; never affect link target or unrelated fixture | |
| A15 | ZIP keep versus extract, source changes after review | No extract without explicit consent; changed source refused; source and destination hashes verified | |
| A16 | ZIP approval, revoke, refresh, guarded Play | Only explicitly approved verified fixture launcher; revoked or altered executable blocked; no auto-run | |
| A17 | ZIP mutable save and changed content | Allowed new save behavior only within baseline policy; changed package files refuse launch/recovery; saves retained | |
| A18 | ZIP quarantine and restore | Exact verified directory moved without deleting content; ownership/approval evidence retained; no automatic approval | |
| A19 | ZIP recovery with conflicting destination or orphaned finalization | No move or overwrite; preserve both paths and evidence | |
| A20 | Managed extraction and ordinary transfer overlap | Durable reservation refuses conflicts; clean success releases only its own marker | |
| A21 | Permanent cleanup surface | Disabled in installed host; no irreversible cleanup authorization | |
| A22 | Multi-source opt-in and source profile changes | Stable scoped identities; no cross-source cache/receipt authority; stale actions refuse | |
| A23 | One multi-source provider disconnects | No unintended catalog erasure; record exact Playnite import behavior; no unsafe stale install route | |
| A24 | Upgrade v0.2.0 -> candidate | IDs, expected migration, stored settings, fixture installed state and hashes preserved | |
| A25 | Upgrade v0.3.2 -> candidate | Same preservation; inspect legacy inflated size (may remain stale) and document manual trade-off | |
| A26 | Desktop at 100% and 150% DPI, dark/light themes | Controls, text and dialogs accessible; no clipping/overlap, keyboard focus visible | |
| A27 | Fullscreen at 100% and 150% DPI, keyboard/controller | Standard imported actions work; no unsupported Desktop-only wizard/dashboard assumed | |
| A28 | Dashboard progress, refresh, cancellation, close/reopen | No repeated dialogs, stale queue rows, UI freeze or unbounded refresh behavior | |
| A29 | Disposable actual UNC/NAS path, disconnect/reconnect | Supported native identity operations or safe explicit refusal; no compatibility claims on unsupported volumes | |
| A30 | Startup and larger fixture timings | Record catalog count, first and repeat refresh duration, queue activity, CPU/memory and local/NAS conditions; no universal performance claim | |
| A31 | Reports/support screenshots/privacy | Bounded diagnostic details; redact paths, accounts, configuration and library content | |
| A32 | Final package/Toolbox/provenance | Exact commit, digest, allowlist, version/ID, official verification and unchanged stable feed recorded | |

### Additional post-review observations

The following supplement A10–A32. They remain NOT RUN until performed in the disposable Windows host; CI evidence does not replace them.

| ID | Scenario | Required observed behavior | Result / evidence |
|---|---|---|---|
| R01 | Ancestor rename during transfer publication | On supported local filesystem, ordinary guarded ancestor rename refuses; no unexpected destination or overwrite; rename works after handles release | |
| R02 | Limited directory permissions and disposable SMB | Supported publication or explicit safe refusal; record ACL and provider conditions without modifying personal data | |
| R03 | Encrypted disposable rclone config without supplied password | Copy, manifest read and listing cannot prompt or hang indefinitely; bounded failure, no password/config contents in logs | |
| R04 | Configured named local rclone versus rejected inline backend | Named isolated fixture operates; inline backend refused without execution | |
| R05 | Ambiguous source, cache and manifest path spellings | Device namespaces, streams, drive-relative operands and reserved aliases fail closed; ordinary explicitly selected UNC sources tested separately | |
| R06 | ZIP32 format boundaries and long output path | Unsupported ZIP64 or invalid metadata refused without stage or destination; long path failure safe and clear; never claim ZIP64 compatibility | |
| R07 | Actual low-capacity destination (disposable volume only) | Local/ZIP preflight fails safely if measured insufficient; unknown capacity not presented as a guarantee; source retained | |
| R08 | Concurrent private metadata replacement | Locked/changed records and occupied backup names refuse when detected; preserve ambiguous evidence, record remaining pathname race as a limitation | |

| R09 | External LocalFile source in Desktop and Fullscreen | With a disposable external fixture, no content access before consent; denial queues nothing, explicit approval applies only to exact path, stale settings refuse; configured roots retain compatibility | |
| R10 | Unapproved external ZIP and source navigation | Menu eligibility is lexical; denial causes no file access; approved ZIP review is read-only; extraction still requires separate consent | |
| R11 | Missing/changed owned marker during disposable transfer | New reservations blocked, recovery reports unreconciled job ID, no automatic release; preserve all evidence | |
| R12 | Large disposable ZIP review and close/shutdown | Awaited worker keeps UI responsive; no duplicate review, no canceled/stale result after shutdown; no extraction or launch authority | |
| R13 | Private metadata writer contention and late cancellation | Competing save refuses before stage, stale profile edit refuses; verified unpublished canceled scratch may be removed, altered or uncertain stage and unrelated files remain | |
| R14 | Contained launcher environment and concurrent host activity | Configured environment honored; no secrets logged, no unlisted inheritable fixture handle visible; unrelated host operations preserved | |

| R15 | Settings revoked or cancellation requested during reservation creation | No provider execution, no destination mutation; release only verified unused originating reservation, otherwise preserve and quarantine | |
| R16 | Missing or mismatched final destination after publication | Retain journal and block overlap/retry; no receipts or installed-state promotion, no ready-to-play notification; subsequent library update behavior recorded separately | |
| R17 | Retry of external LocalFile attempt | Direct queue retry refuses, hosted retry requires a fresh controller and affirmative per-operation consent | |
| R18 | File ancestor cache destination collision and receipt path binding | Refuse inconsistent catalog before publication regardless of item order; ordinary sibling cache files remain valid; receipt names the actual completed destination rather than re-resolving it | |

## Stop rules

Stop on possible source/user-data mutation, unexpected deletion, incorrect cross-source identity, unauthorized launcher execution, unexplained host freeze or unknown process ownership. Preserve logs, journal and staging. Do not automatically remove evidence, rerun a destructive operation, or bypass a refusal to obtain a PASS. Reviewers and owner adjudicate the failure before continuing.

## Signoff

| Gate | Result |
|---|---|
| Independent review and remediation | NOT RUN |
| Disposable Desktop acceptance | NOT RUN |
| Disposable Fullscreen acceptance | NOT RUN |
| Upgrade and provider qualification | NOT RUN |
| Official Toolbox and final provenance | NOT RUN |
| CH-Rm deferred selective review (separate gate) | NOT RUN |
| Owner release authorization | NOT GRANTED |

Public release remains unchanged. This worksheet never grants permission to merge, tag, publish or enable previously disabled destructive actions.
