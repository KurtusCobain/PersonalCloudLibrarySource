# Independent Playnite / Plugin Ecosystem / License Review — October 30, 2026

**Scope:** PCLS unmerged [draft PR #10](https://github.com/KurtusCobain/PersonalCloudLibrarySource/pull/10), Playnite 10 SDK and install-state flow, comparable open-source plugins and licensing. Public PCLS v0.2.0 and `main` are unchanged. **Do not merge or publish from this report.**

## Verified license status

PCLS's root [LICENSE](../../LICENSE) is **MIT**, with `Copyright (c) 2026 The Bearclaw & ztwslayer`. Playnite itself is also MIT. The PCLS project targets .NET Framework 4.6.2, currently references PlayniteSDK **6.16.0**; newer SDK 6.18.0 exists but requires compatibility testing before adoption. Do not modify contributor attribution without authorization.

A release audit discovered that `tools/package-extension.ps1` had omitted the LICENSE from the packed `.pext`. Draft commit `6d5c2384` packages the MIT notice and strengthens `tools/test-release-baseline.ps1` to enforce the notice and copyright; the related synthetic package fixture was corrected in `c6800411`. Validate CI at the *latest exact head* before relying on this.

| Plugin | License | Review depth | Takeaway |
|---|---|---|---|
| [Cloud Storage](https://github.com/GreenFuze/PlayniteCloudSource) | Apache-2.0 | README, license and install/uninstall controller code | Read-only cloud folder choice, truthful reconciliation, Playnite progress |
| [SMB Game Library](https://github.com/YavuzAkbay/SMBGameLibrary) | MIT | README, license and install/retry code | NAS-to-SSD positioning, bounded transient retry |
| [EmuLibrary](https://github.com/psychonic/Playnite-EmuLibrary) | GPL-3.0 | License, prior audit | ROM grouping and contained-path architecture only |
| [RomM](https://github.com/rommapp/playnite-plugin) | GPL-3.0 | README, license, install/uninstall code | Verify shared install-folder ownership before deletion |
| [LocalLibrary](https://github.com/azuravian/playnite-LocalLibrary) | CC0-1.0 | License metadata | Local metadata simplicity |
| [GameVault](https://github.com/Phalcode/gamevault-playnite-integration) | GPL-3.0 | License metadata | Server import; separate scope |
| [Gameyfin](https://github.com/mark-monteiro/gameyfin-playnite-library-plugin) | MIT | License metadata | Remote catalog |
| [LANCommander](https://github.com/LANCommander/LANCommander.PlaynitePlugin) | MIT | License metadata | LAN integration |
| [Playnite Link](https://github.com/metanmai/playnite-link) | AGPL-3.0 | README and license | Clear stale/offline/conflict states; defer save sync |
| [Auto Game Importer](https://github.com/Sam-GG/PlayniteAutoImport) | MIT | README and license | Review-before-import, ignore lists |
| [AutoImport](https://github.com/Neppale/auto-import) | MIT | README and `LICENSE.md` | Selection + persistent block list; previous 'no license' claim was incorrect |
| [Simple Sync](https://github.com/Yalgrin/playnite-simple-sync-plugin) | LGPL-2.1 | License metadata | Sync not installation |
| [Playnite Bridge](https://github.com/rollacode/playnite-bridge) | MIT | License metadata | Avoid scope expansion into external execution |
| [HydraTorrent](https://github.com/BCDezgun/Playnite-HydraTorrent) | MIT | License metadata | Transfer UX only, no torrent integration |
| [LibraryManagement](https://github.com/Lacro59/playnite-librarymanagement-plugin) | MIT | License metadata | Localization lessons |

**Copyright boundary:** architecture/UX ideas are fine to study; never paste GPL/AGPL implementation into MIT PCLS without compatible relicensing/legal review. Verify attribution and NOTICE for any Apache code reused. MIT doesn't convey game content or third-party artwork rights.

## Primary Playnite 10 source findings

1. [SDK Actions.cs](https://github.com/JosefNemec/Playnite/blob/master/source/PlayniteSDK/Plugins/Actions.cs) saves `SynchronizationContext.Current` in `ControllerBase`; installed/cancelled callbacks use `execContext.Send`. Controllers created without a valid context may not signal correctly.
2. [GamesEditor.cs](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/GamesEditor.cs) sets `IsInstalling=true` on action start. Installed and cancelled events clear it, update the Playnite database and synchronously remove/dispose the controller.
3. [GameControllerFactory](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Controllers/GameControllerFactory.cs) attaches/detaches event handlers. Desktop/Fullscreen both expose standard install action selectors; neither has been installed-tested against this PCLS candidate.
4. PCLS draft fixes missing early-rejection termination and async transfer completion. A further queue race was found: a late cancellation could mark a job cancelled but return a success result to the controller. Commit `0101c41e` normalizes the completion result to terminal state.
5. **Mandatory installed-host gate:** verify exactly-one terminal callback, UI responsiveness, host disposal/cancel during slow or failed transfer, Fullscreen, shutdown and upgrade. Do not infer this from CI alone.

## PCLS source fixes already in draft PR #10

- Reject unsafe cache path traversal, root and overlapping destinations and source junction/symlink traversal.
- Rclone partial-target protection, valid zero-byte files and empty folders, Windows command-line escaping, bounded manifest read/log buffers.
- Bound manifest generation to importer-supported entry count and preview scans to finite work.
- Remove seven nonfunctional settings controls; repair live dashboard cached counts.
- Async install observer and cancellable, background local/NAS setup generation.
- Prequeue rejection terminal signaling; cancellation/success race reconciliation.
- MIT license added to release package and package contract; migration preserves serialized settings.

## Still unresolved before public release

- **Actual installed Playnite 10:** portable clean setup, real v0.2.0 upgrade, install/queue/cancel/retry/shutdown, Desktop + Fullscreen, 100/125/150/200% DPI.
- **Provider and filesystem testing:** fake files plus configured disposable Google Drive/rclone remote, disconnected NAS, file placeholders/junctions, low free space and destination collisions. No real user's library deletion tests.
- **Polish:** final distinctive screenshots replacing temporary alias images, icon/hero asset ownership, clear warnings, accessible keyboard flow and accurate docs.
- **Packaging:** official Playnite Toolbox `verify addon` and `verify installer`, recorded SHA-256, stable addon ID; decide release version **only after** acceptance. Public installer feed intentionally remains v0.2.0.
- **Performance:** consider cancellation-aware enumeration of staged directory verification (currently uses all-files materialization); test under large disposable game directories before changing semantics.
- **Privacy:** review historical commits separately; do not force-push history casually.

## Prioritized enhancements after stability

**Next minor updates:** cache size per game and total with no automatic eviction; explicit online/offline/reconnect; import diff with corrections and persistent ignore list; helpful rclone connection tester and redacted report; bounded idempotent NAS retry.

**Not for this final 1.0 candidate:** automatic deletion/eviction, two-way save/mod sync, archive extraction expansion, emulator-manager features, torrents, REST/eval, game delivery, or Playnite 11 guarantees.

## Independent reviewer request (read-only)

Review the *exact current draft PR head* against main and public v0.2.0. Inspect Playnite 10 SDK event contracts, Playnite `GamesEditor`, PCLS controllers/queue/path safety/partial cleanup, import-source disappearance, settings migration, WPF UI, package, dependency notices, icon and screenshots. Report **severity, file + line, reproduction, smallest safe repair, and test**. Separate code-proven findings from host-only hypotheses. Distinguish fixed old-main bugs from genuine current-head defects. Do not merge, tag, publish or touch personal Google Drive files. Report RELEASE/HOLD with a short mandatory test matrix. Peer reviewers may be Muse (UX), Claude (C#/WPF lifecycle), Grok (competitors/rclone), Codex (adversarial patch/test) or an independent GPT. **This document prepares that request; it does not claim any other model completed a review.**
