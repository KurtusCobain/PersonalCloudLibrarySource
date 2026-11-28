# Persistent transaction and restart-recovery protocol design

Status: engineering proposal; implementation and qualification required. Candidate 0.3.2.1. No change to production storage, public feed, main, CH-Rm, or release authority.

## Threat and compatibility boundary

Existing guards pin ancestors, reject reparse points/hardlinks where ownership is required, bound reads, serialize cooperating private-store writers, verify payloads and identities, and publish new objects with no-replace handle rename. Existing File.Replace calls still operate on names after closing leaf handles. Neither content hashes nor post-replacement identity checks can undo an unintended overwrite. Arbitrary same-user code that can rewrite the plugin or its data is outside the enforceable trust boundary; do not claim sandbox security. A stronger protocol should avoid overwriting an unexpected object even when a namespace entry changes. Network shares and power-loss durability need independent qualification.

## G1/G2 — recommended immutable-generation storage

Prefer a versioned append-only private-store protocol over a two-rename replacement transaction. Do not silently convert existing source-profile, catalog identity/display, receipt, ZIP baseline or user-selected manifest/report formats. First inventory every reader, writer, backup and migration contract; distinguish plugin-owned metadata from exported user-facing files with fixed filenames.

For plugin-owned records, use an exclusive verified store lock and a dedicated private namespace. New records are bounded, strictly decoded, versioned and include logical key, sequence, predecessor digest and payload digest; define canonical digest encoding and reject malformed/duplicate sequences, wrong keys and forks. No digest is authentication against an arbitrary local editor. Stage into a unique sibling, flush and verify bytes and file identity, publish by the existing no-replace handle primitive, verify the published object. No pathname overwrite or deletion on uncertain completion. Return success only after publication verification. Readers fail closed on ambiguous or incomplete chain evidence; do not silently select an older generation. Define explicit size/count bounds and an operator-visible capacity refusal, not automatic removal of historical records.

Migration is explicit: freeze writers and review a valid legacy record; retain it intact, bind its exact digest and identity in the migration evidence, verify the initial new generation, and enable new readers/writers only after the durable migration decision. Old binaries must not silently write stale legacy data after activation. Define downgrade behavior and recovery from interruption before implementing activation. Existing exported fixed-path files require a separate policy: no-replace versioned export or an independently reviewed recoverable transaction preserving user-facing compatibility. Do not claim G1 closure by implementing only private-store generations while other affected writers remain unchanged.

Test boundaries: concurrent readers and cooperating writers; target/stage/backup substitution including same-byte replacement; reparse and hardlink refusal; interrupted stage write, flush, rename, verification and migration; fork/duplicate/gap/corruption; exhaustion; retained legacy bytes and rollback restrictions; local NTFS and real SMB semantics. Process termination fixtures are not evidence of physical power-loss durability.

## G3/G4/G7 — conservative operator recovery

Current V1 pending marker records game ID, job ID and canonical destination. It does not prove originating execution stopped, and a missing stage does not distinguish safe interruption from completed publication. Legacy markers must remain fail-closed. Never add a general delete-marker command based on a PID, age, missing path or checkbox.

Proposed V2 protocol needs a durable session/job identity, exact marker identity and payload digest, and an execution-lifetime fence acquired before provider I/O and held until known worker termination. A non-inherited lease must cover local workers and the native rclone job lifetime; review whether host death guarantees descendant termination before a new owner can acquire the fence, including handle duplication, remote/provider activity and failed job containment. A free lock alone is not proof of quiescence. Unsupported legacy, share or uncertain process cases remain blocked. Do not use a named process identity alone or kill a process by stale PID.

Operator review must distinguish live/unknown, verified stopped, changed evidence, stage present, destination present, and committed-but-unreconciled outcomes. Show exact job/destination/stage, state and limitations; no automatic retry or cleanup. Require independently verified quiescence, stable marker identity, revalidated destination/stage and an explicit disposition tied to the reviewed evidence. Acknowledge does not mean delete data. Preserve unknown files, user saves and original source. A durable append-only disposition must precede any release; make replay idempotent, bind it to the exact reservation and never allow it to authorize another attempt. A changed marker or stage invalidates review. Keep launch/catalog/removal gates fail-closed while review or disposition is incomplete. Design marker-release semantics and crash restart before introducing a UI command; no direct manual removal instructions for unqualified evidence.

Test boundaries: two instances, live workers, descendant/job failure, process death during each stage, reused identity, changed marker or stage, duplicate/replayed approval, destination present without stage, legacy marker, corrupt record, inaccessible path, simultaneous enqueue/launch/uninstall, local and SMB behavior. Test genuine native Windows Playnite host and disposable providers; qualify independent review before enabling release.

## Engineering decision sequence

1. Inventory exact storage and runtime contracts and document supported threat model.
2. Implement isolated primitives and migration/recovery state machines with fault injection, without enabling destructive authority.
3. Wire private-store readers/writers and recovery review under explicit version/feature boundaries; verify compatibility and downgrade.
4. Qualify same-commit Windows, archive and CodeQL plus disposable native host and provider tests. Obtain independent adjudication of residual constraints before marking G1/G2 or G3/G4/G7 closed.

No implementation or approval is implied by this design record.


## Prototype implementation checkpoint

At `90f4caaf66a9ea260899366a97745c95ea52f8e8` the isolated internal ImmutableRecordGenerationStore was added to the .NET Framework project and its tests registered. At `0ad3c888577da36227bb2e76fc80ecfb2221dd78` additional interference and capacity regressions were added. Total 13 new test cases (not yet a passing count at inspection). [Windows](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46123916841), [archive](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46123916864), [CodeQL](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/46123916855) in progress. Prior exact checkpoint `eee9a224` passed all three workflows, with 1089 standard + 2 real-rclone tests passing, zero failures/warnings and official Toolbox candidate validation.

Implemented: one existing dedicated directory per logical GUID; exclusive guarded cooperative lock (readers also acquire it and may create the persistent lock file); 64 canonical sequential filenames; strict UTF-8, canonical base64 and SHA-256 payload/predecessor chain; explicit expected-head compare-and-append; private pending file, flushed stage, verified no-replace native publication and post-publication identity/content check; full chain revalidation; defensive payload copies. A pending or unrecognized object, malformed/gapped chain, wrong logical key, or exhausted capacity refuses operations without deleting evidence. Regression fixtures exercise interruption after staging and after publication, same-byte stage replacement, occupied target, invalid input, stale head, competing reader/writer, and corrupt/foreign evidence. These are deterministic exception fixtures, not physical power-loss tests.

Limitations: no trusted external head anchor, so deletion of an entire valid suffix or malicious rewriting of a whole valid chain cannot be detected solely from this directory. The cooperative lock does not serialize nonparticipants; scans are not a filesystem snapshot. A complete published record can be visible after an interrupted caller; callers must reload and reconcile before retry. A pending stage requires future recovery protocol, not automatic removal. Current limits are 1 MiB payload, 2 MiB envelope, 64 generations; no rotation or garbage collection. This prototype is not used by live settings, imports, controllers, receipts, or transfer recovery. No migration, downgrade protection, restart-release authority or production activation has been implemented.

### Initial live-storage contract inventory

| Component | Current contract and migration disposition |
|---|---|
| SourceProfilesFileStore | One source-profiles.v1.json; 128 Ki-character codec, up to 512 KiB encoded; canonical baseline comparison and unique backup/restore. Candidate for first isolated migration design, preserving backup preview and legacy ID rules. |
| SourceCatalogSnapshotStore | Per-profile identity records, 2 MiB max and 10,000 games. Prototype payload limit insufficient. Preserve profile binding and cross-record consistency. |
| SourceCatalogDisplaySnapshotStore | Per-profile display-only records, 4 MiB max. Prototype payload limit insufficient. Preserve exact identity matching, never confer launch authority. |
| CacheOwnershipReceiptStore | Hash-derived key, no-replace WritePrivateRecord; receipt binds cache root, item and destination, and inventory. Not an ordinary mutable replacement caller. Do not introduce automatic new-generation ownership grants. |
| ZipMutableDataBaselineStore | No-replace private record, 2 MiB max, up to 12,000 original entries and linked receipt checks. Do not change deletion or save-data semantics. |
| SafeFileWriteService / ManifestGenerationService | Fixed user-selected manifest and optional report filenames; backup-enabled replacement. Cannot silently substitute private versioned filenames. |
| SafeFileWriteService / PersonalCloudLibrarySource | last-import-diagnostics.txt and verification report exports; latter uses backups. Explicit fixed-path compatibility policy required. |
| TransferRecoveryJournal | Separate pending record and release protocol; no migration to ordinary generation head without dedicated execution and disposition rules. |
| SetupStatePersistenceService / DashboardStateStore | Setup changes settings; dashboard is observable in-memory state. Neither is a direct replacement caller in the inspected code. |

This is a focused contract inventory of the inspected components, not a proof that every persistence call site has been inventoried. Next: qualify the primitive, implement explicit legacy migration/activation and downgrade refusal for the first eligible store, and resolve separate restart-recovery and fixed-path export protocols. Do not replace live stores before those gates.
