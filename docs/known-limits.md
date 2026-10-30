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
