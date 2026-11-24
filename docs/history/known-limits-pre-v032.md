# Historical development record — October 2026

This snapshot preserves the pre-v0.3.2 candidate notes. It is **not** the current public release status. Several statements below were superseded by the November 1 Windows QA and publication. For current limitations, see [Known limits](../known-limits.md) and [Release status](../release-status.md).

---

# Known limits

These limits apply to the 1.0 release candidate while the installed package remains version 0.3.2.

- PCLS catalogs and caches content; it does not stream gameplay.
- It does not provide content, storefront access, scraping, credentials, or cloud accounts.
- Setup, settings, manifest generation, verification reports, dashboard, and custom details are Desktop-only management surfaces. There is no dedicated Fullscreen setup wizard or dashboard.
- Core GameLibrary metadata and standard play/install/uninstall controllers have automated Fullscreen-boundary coverage, but installed Fullscreen qualification is still pending.
- External, mapped, and NAS paths depend on Windows drive mapping, credentials, latency, and device/network availability. PCLS cannot reconnect unavailable media or repair provider credentials.
- Rclone must be installed and configured by the user. Provider throttling, authentication, and remote-specific behavior remain outside PCLS.
- Automatic startup actions are bounded and report failures; PCLS does not auto-download an entire library.
- Recent dashboard activity is an in-memory, bounded list and is not a persistent audit log.
- Verification reports and diagnostics are support aids, not backups of source content.
- Existing absolute paths may be catalogued, but new installs must remain **inside** the configured local cache and cannot target the cache root. Enabling outside-cache uninstall does not authorize outside-cache installs or override root/reparse-point safety checks.
- Outside-cache uninstall is an advanced opt-in that may delete external files. Known local source items and the LocalFolder source root are explicitly protected by the candidate, but unrelated external paths are not automatically recognizable as source files. Keep the option off for normal use and verify any external deletion target before enabling.
- The candidate rejects local manifests exceeding 32 MiB, parsed manifests exceeding 32 million characters, and catalogs exceeding 100,000 items.
- Local directory caching refuses source folders containing filesystem links/junctions, rather than recursively following them to unrelated locations. It also refuses destinations that overlap the source directory. Real synced folders using Windows reparse-point placeholders may require a direct rclone setup or a separate ordinary local source.
- The wizard now offers a read-only preview for local folders, NAS locations, and local manifest files. Completing it generates a local-folder manifest on a background worker with cancellation checked before settings persistence; a disconnected/hung filesystem operation may not stop instantaneously. Filesystem previews stop at 10,000 entries, can be cancelled, and do not replace actual import/verification. Cloud/rclone requires a separately configured remote and later verification.
- The install controller now observes completion asynchronously to keep the action responsive; **real Playnite Desktop/Fullscreen, cancellation and disposal acceptance remains mandatory** before release.
- Installed provider, upgrade, package, high-DPI, and Fullscreen rows remain part of release qualification.


### Draft ownership hardening (October 30, 2026; not yet released)

File-type manifest entries authorize only their own cached file, not the containing directory. If a file item is configured for folder removal (including legacy Ask each time migration), draft builds refuse the uninstall action rather than recursively deleting possible unrelated files. Choose the file-only uninstall mode for those entries. Directory-type entries still require a separately verified ownership strategy before unattended recursive deletion can be considered safe. A classification-only CacheOwnershipPolicy exists but is not a persisted receipt ledger and does **not** authorize deletion; no legacy cache has been automatically adopted. The draft remains blocked pending full ownership persistence/migration implementation and native Playnite Windows acceptance.


### Unverified directory caches (draft safety gate)

Draft builds no longer offer or perform automatic **recursive directory removal** for directory-type items without verified PCLS-created installation provenance. Existing folders remain detectable as cached and are not migrated, adopted, or deleted by this restriction. The current provenance classifier is **not** yet a durable verified receipt system, so recursive directory uninstall remains disabled even for new transfers. File-only cache removal remains separately governed by its existing path safety checks. To remove a legacy directory cache before provenance support is completed, review its contents and manage it manually outside PCLS. This is a deliberate temporary compatibility limitation, not a completed ownership-ledger rollout.


### Receipt store foundation (draft, not enforced)

The candidate now includes a create-only JSON receipt store keyed by cache root and item ID. It rejects duplicate records and invalid/missing destinations, and reading fails closed on malformed data. **This is not yet wired to transfers, is not cryptographically authenticated, and cannot be used as deletion authorization.** The host must provide a stable receipt directory outside the cache and source roots; the transfer lifecycle must issue receipts only after a newly created destination commits; readers must validate destination and identity; and an actual deletion gate and migration/compatibility tests are still required. No existing cache is automatically adopted. Do not consider recursive removal safe or release-ready on the basis of this store.


### Receipt issuance on successful transfers (draft)

The draft install controller now attempts to write a create-only receipt to the plugin user-data `ownership-receipts` directory after a successful local or rclone transfer. Failed and cancelled transfers do not issue a receipt. Storage refuses to overlap the cache root. Receipt-write errors are logged without making the successfully copied game unusable. **Receipts are still informational**: they do not authenticate filesystem contents, prove an entire tree remains unchanged, or grant automatic deletion. Recursive directory uninstall remains blocked until a verified deletion contract and migration tests are implemented. The host integration needs installed-Windows testing.


### Read-only cache inventory (draft)

A bounded SHA-256 directory inventory scanner is available for testing provenance. It hashes relative paths and file contents, rejects linked entries, and limits enumeration to 150,000 entries and individual file sizes to 4 GiB. It does not yet attach fingerprints to issued receipts, has no cancellation support, and does not eliminate time-of-check/time-of-use races. **It is not a deletion authorization mechanism.** Recursive directory uninstall remains disabled; a future revision needs safe snapshot issuance, verified ownership and host acceptance before release.


### Versioned directory inventory receipts (draft)

After a successful directory transfer, PCLS now attempts a version 2 receipt containing the SHA-256 tree inventory. This scan runs off the Playnite UI thread; errors leave the copied game usable but do not establish inventory provenance. Existing version 1 receipts remain readable but cannot pass the new inventory comparison. A read-only comparison detects ordinary added, removed or changed files and returns no authorization to delete. Scans are currently bounded but may require substantial disk I/O, and TOCTOU, Windows links and identity/permission enforcement still require further review and installed-host testing. **All recursive directory deletion remains disabled.**


### Cancelable inventory scanner foundation (draft)

The SHA-256 scanner now accepts an optional cancellation token and checks it between entries and 1 MiB file read chunks. The existing two-argument scanner call remains compatible. This does not yet wire host shutdown/controller disposal to the inventory cancellation token, and synchronous filesystem reads can still take time to return. The operation remains read-only and never authorizes recursive deletion.


### Controller disposal cancellation (draft)

Disposing the install controller now signals cancellation to directory-inventory hashing and checks cancellation before publishing a new V2 receipt. This prevents a pre-cancelled or cooperatively cancelled scan from creating ownership claims. It does **not** roll back an already committed transfer, interrupt a blocking filesystem read immediately, or guarantee that cancellation wins a race after receipt publication. No directory-delete authorization has been enabled. Installed Windows Playnite lifecycle acceptance is still required.


### Existing-cache ownership non-adoption regression (draft)

A controller-level regression checks that a local install targeting a pre-existing cached file fails without overwriting its bytes or issuing an ownership receipt. The transfer adapters' no-overwrite staging behavior remains the foundation; simply finding content on disk never claims ownership. A passing synthetic regression does not establish that Windows host races, rclone destinations or filesystems are fully safe. Recursive directory uninstall remains disabled.


### Directory V2 lifecycle and receipt validation tests (draft)

The draft Windows test fixtures now exercise a successful local directory transfer through V2 receipt issuance, a matching initial inventory, a user-added file causing mismatch, and the continued refusal to offer recursive uninstall. A separate paused directory-transfer fixture confirms disposal before terminal success cannot issue an ownership receipt. Receipt reads reject destinations outside the configured cache, malformed V2 inventory metadata, and receipt stores that overlap the cache tree. These are safeguards against accidental adoption and tampered metadata, **not cryptographic authenticity or filesystem race elimination**. No installed-host Playnite Desktop/Fullscreen acceptance is claimed and no recursive directory deletion is enabled.


### Disposable rclone lifecycle and staging-tree hardening (draft)

The test suite now exercises the rclone transfer queue and installation controller using an in-process fake `IRcloneProcessRunner`. The fixtures create disposable local staging files only: they check successful V2 directory receipt issuance, modified contents, simulated remote failure, cancellation, and refusal to adopt a pre-existing destination. **These are simulated rclone responses, not a real rclone binary or cloud provider.** A Windows junction fixture checks that a nested link under the partial directory is rejected and that unrelated linked-to data is left alone. The rclone adapter now refuses nested reparse points and overlarge staging trees before recursive verification or cleanup; the transfer executor also screens final destinations. These are best-effort checks, not an elimination of race windows between inspection and filesystem operations. More tests are required for a real disposable local rclone remote, subprocess timeouts, permissions and installed Playnite lifecycle. Recursive directory uninstall remains disabled.


### Explicit opt-in real rclone local-backend check (draft)

A separate `RcloneLocalBinaryIntegrationTests` case can exercise the production `RcloneProcessRunner` and `RcloneTransferAdapter` with a **real installed rclone executable**, using the configless `:local:` backend, a randomly generated temporary source/cache, V2 receipt creation and a post-transfer user-file mutation check. It requires `PCLS_TEST_RCLONE_EXE` to be set to the full path of the local `rclone.exe` before running the repository's Windows NUnitLite test suite. For example, in a Windows PowerShell test session:

```powershell
$env:PCLS_TEST_RCLONE_EXE = (Get-Command rclone.exe).Source
# Run the repository's documented full Windows NUnitLite suite here.
Remove-Item Env:PCLS_TEST_RCLONE_EXE
```

The test is skipped without this explicit opt-in. It uses no saved cloud remote or cloud credentials, but has **not yet been executed against a real rclone binary** in this development session. It does not replace installed Playnite Desktop/Fullscreen or actual-provider acceptance tests. Recursive directory deletion remains disabled.
