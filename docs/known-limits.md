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
- Absolute cache paths outside the configured cache are refused by default. Enabling outside-cache uninstall does not override root or reparse-point safety checks.
- Installed provider, upgrade, package, high-DPI, and Fullscreen rows remain part of release qualification.
