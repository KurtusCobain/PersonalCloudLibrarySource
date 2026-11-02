# Known limits — PCLS v0.3.2 (public release)

Updated November 1, 2026. This page describes the **published** [v0.3.2 package](https://github.com/KurtusCobain/PersonalCloudLibrarySource/releases/tag/v0.3.2), not an unmerged candidate. Historical implementation notes are preserved [here](history/known-limits-pre-v032.md).

## Product boundaries

- PCLS catalogs games supplied by the user and copies/downloads selected content into a local cache. It is **not** a game store, source of ROMs, emulator manager or gameplay-streaming service.
- No credentials, remote accounts, games, BIOS files or download sources are bundled. A remote must be configured independently using rclone; local folders, external drives and NAS paths do not require rclone.
- PCLS does not automatically cache an entire library on startup and does not synchronize saves or mods.

## Source availability and providers

- `LocalFile`, `LocalFolder` and `RcloneRemote` are implemented. Network shares, external disks and synced folders depend on Windows permissions, connection stability and placeholder behavior; the plugin does not repair provider credentials or unavailable mounts.
- The November 2026 Windows QA ran local-only fixtures, including a real rclone `:local:` process and a disconnected-source test. **No live third-party cloud account or real remote NAS was used for the release acceptance.** Do not interpret that test as certification of Google Drive, OneDrive, Dropbox or every remote provider.
- Local folder copy rejects filesystem links/junctions and overlapping source/cache trees. Some cloud-sync placeholders are implemented with reparse points and can therefore require an ordinary local copy or explicit rclone configuration.
- Manifest bounds: local manifest files ≤32 MiB, parsed content ≤32 million characters and catalogs ≤100,000 entries; preview scans are separately bounded and cancellable. A blocked filesystem call may take time to return even after cancellation.

## Cache safety and storage reporting

- New installs are restricted to destinations **inside the configured local cache**. Existing external/absolute-path entries may remain visible, but new outside-cache installations are refused. No source files or remote content are removed by ordinary cache uninstall.
- File-only cache uninstall is guarded by explicit path policies; **automatic recursive directory-cache uninstall remains disabled**. This is intentional. A game folder may contain later-added saves, settings or mods that the plugin must not silently destroy.
- PCLS has V1/V2 ownership receipts and read-only SHA-256 tree inventories, with limited lifecycle tests. **Receipts alone are not authorization for recursive deletion**, and changes, reparse-point races and legacy preexisting caches remain separate safety challenges. No legacy cache is silently adopted.
- The advanced outside-cache **uninstall** option is hazardous: it can affect unrelated external files and should remain disabled for normal use. It does not grant permission for outside-cache installs.
- **Known display issue:** a game's shown installed size can include unrelated content where multiple game entries share a cache/install directory. It is not a reliable per-game storage figure yet.
- The dashboard's recent activity is bounded in memory, not a persistent audit log. Diagnostics are support aids, not backups of the original collection.

## Playnite UI, accessibility and upgrade qualification

- Guided setup, the PCLS dashboard, source verification and custom Details management are **Desktop-only**. Fullscreen supports imported games and standard Playnite game actions; there is no separate PCLS Fullscreen setup wizard or dashboard.
- Windows QA observed Desktop core workflows, Fullscreen launch actions, dark-theme readability, file-only uninstall/reinstall and cancellation/retry. Some keyboard/controller-navigation paths and alternate-DPI layouts have **not** been fully signed off.
- An isolated installed **v0.2.0 → v0.3.2** upgrade preserved both test game IDs, installed states and recorded hashes, with the documented default rclone timeout change from 30 to 90 seconds. That does not establish the same result for every prior version, real collection or provider.
- Users should back up their Playnite and PCLS configuration before upgrading. See [Upgrade guidance](upgrades.md).

## Before v1.0

Finish independent real-provider and offline recovery coverage; repair cache size accounting; complete accessibility/DPI testing; confirm cancellation/disposal behavior under faults; and establish a separate, rigorously reviewed directory-cleanup policy or explicitly retain the safe disabled state. See the [1.0 roadmap](roadmap-v1.0.md).

No published Playnite version should be assumed to have features not listed in its actual release notes.
