# Official Playnite Addon Database — PCLS v0.3.2 update handoff

As of November 1, 2026, the official listing at [JosefNemec/PlayniteAddonDatabase](https://github.com/JosefNemec/PlayniteAddonDatabase/blob/master/addons/library/PersonalCloudLibrarySource.yaml) still says **v0.2.0 is the latest**. PCLS's own published [installer manifest](https://raw.githubusercontent.com/KurtusCobain/PersonalCloudLibrarySource/main/playnite-addon/installer.yaml) correctly lists **v0.3.2**, and the official Playnite 10.63 Toolbox [verified the public feed](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/41353669451).

## Proposed upstream change

Copy [PersonalCloudLibrarySource-v032.yaml](PersonalCloudLibrarySource-v032.yaml) over `addons/library/PersonalCloudLibrarySource.yaml` in a **fork of the PlayniteAddonDatabase**, then open a PR to `JosefNemec/PlayniteAddonDatabase:master`.

- Preserve AddonId, Type, author, InstallerManifestUrl and existing working screenshots.
- Update the stale v0.2.0 description with accurate published v0.3.2 features and limitations.
- Do **not** repackage the extension or create a second installer feed. The original remains stable.
- Once screenshots below are approved and uploaded as publicly accessible repository files, submit a separate small upstream screenshot URL update.

## Genuine Windows QA image shortlist

The Windows handoff contains these **unaltered** 2026-11-01 Playnite captures from disposable test games:

| File | Original pixels | Proposed use |
|---|---:|---|
| `01-PCLS-Setup-Source-Selection.png` | 1920×1040 | Guided setup / release gallery |
| `05-PCLS-Game-Library.png` | 1920×1040 | Playnite Desktop game catalog |
| `10-PCLS-Fullscreen.png` | 1920×1080 | Playnite Fullscreen game actions |

The first two are **not** 16:9; existing CI's canonical screenshot contract requires matched 16:9 captures. Do not silently crop, stretch, or claim altered pictures are raw evidence. For public use, either publish the original aspect ratios in new filenames where supported or explicitly approve a derivative with faithful framing and keep the originals as private evidence. All screenshots need an independent privacy review before public upload. No screenshot file was uploaded by this documentation-only PR.

The zip of three selected originals was delivered with the Windows QA handoff. Avoid public screenshots displaying real user paths, accounts, credential hints, private libraries or unrelated desktop/chat content.

## Submission steps

1. Review the proposed YAML and approved image selection.
2. Fork `JosefNemec/PlayniteAddonDatabase` (not PCLS itself), replace its `addons/library/PersonalCloudLibrarySource.yaml` and open a pull request titled **Update PCLS listing for published v0.3.2**.
3. Link the published [GitHub release](https://github.com/KurtusCobain/PersonalCloudLibrarySource/releases/tag/v0.3.2) and successful [official Toolbox validation](https://github.com/KurtusCobain/PersonalCloudLibrarySource/actions/runs/41353669451).
4. Check the actual official listing after merge; do not treat preparing this file as a published upstream change.

The existing public installer manifest remains the sole source for installable versions and already lists v0.3.2 first.
