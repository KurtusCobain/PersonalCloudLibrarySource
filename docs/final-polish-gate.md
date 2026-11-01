# PCLS public release follow-up and v1.0 checklist

The original PR #10 draft checklist was completed or superseded during the November 1, 2026 public [v0.3.2 release](releases/v0.3.2.md). Its unmodified historical wording is archived [here](history/final-polish-gate-pre-v032.md).

## v0.3.2 evidence

- [x] Four Windows QA fixes merged via PR #10.
- [x] GitHub Windows Debug/Release build and package inspection green; 433 passing hosted tests, 2 opt-in real-rclone cases skipped in CI.
- [x] Separate native Windows QA: 435/435 with rclone installed, disposable source/cache and cancellation/retry, upgrade from v0.2.0, observed Desktop and Fullscreen game actions.
- [x] Public GitHub release package published with SHA-256 recorded in [release status](release-status.md).
- [x] Official Playnite 10.63 Toolbox verified the published v0.3.2 add-on and installer manifests.
- [x] Live installer feed lists v0.3.2 and retains v0.2.0.
- [ ] Correct the **upstream** official Playnite Addon Database's stale v0.2.0 description (this repository cannot silently rewrite that separate repository).
- [ ] Review and publish privacy-clean original Windows screenshots; existing CI's 16:9 gallery contract and 1920×1040 source captures must not be conflated.
- [ ] Fix misleading shared-cache per-game installed-size display.
- [ ] Expand real-provider and device tests; see [known limits](known-limits.md).

## v1.0 acceptance gates

1. **Data safety:** unchanged source content under failed/cancelled/retried transfer, path/reparse races, no unrecognized file deletion, accurate cache ownership, safe directory uninstallation decision.
2. **Real environments:** disposable local/USB/NAS and at least one configured rclone provider with disconnect/reconnect, credential failure, throttling and no destructive test on a real collection.
3. **Usability:** clean first-run setup, helpful errors, correct game-size reporting, keyboard/150% DPI test, Desktop and Fullscreen core actions.
4. **Compatibility:** published 0.2.0 and 0.3.2 upgrade paths and legacy manifest/settings changes validated with recorded identities/hashes.
5. **Distribution:** final version metadata/notes/screenshots/README/listing consistent; official Playnite Toolbox check; exact package checksums and zero critical failures.

No automatic recursive deletion is authorized by this checklist. Keep it disabled unless a separately reviewed ownership/delete procedure meets the explicit safety criteria. The [1.0 roadmap](roadmap-v1.0.md) prioritizes and scopes the work.
