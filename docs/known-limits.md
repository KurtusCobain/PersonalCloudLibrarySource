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
- The candidate rejects local manifests exceeding 32 MiB, parsed manifests exceeding 32 million characters, and catalogs exceeding 100,000 items.
- The wizard's Review source step confirms configuration only; full detected/skipped game preview remains a usability improvement under development.
- Installed provider, upgrade, package, high-DPI, and Fullscreen rows remain part of release qualification.
