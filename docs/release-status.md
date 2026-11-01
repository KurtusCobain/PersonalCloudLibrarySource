# PCLS Current Release Status

Updated 2026-11-01.

## Public release

**Personal Cloud Library Source v0.3.2 is publicly released.**

- Release: https://github.com/KurtusCobain/PersonalCloudLibrarySource/releases/tag/v0.3.2
- Package: https://github.com/KurtusCobain/PersonalCloudLibrarySource/releases/download/v0.3.2/PersonalCloudLibrarySource-0.3.2.pext
- Published package SHA-256: `1781fe630fad9398b8e810e3cf843309acc68a8a3dcfba720873042da41b2ed7`
- Extension ID: `PersonalCloudLibrarySource_61993828-67a8-4468-93a2-293442e36328`
- Release notes: [v0.3.2](releases/v0.3.2.md)
- Public installer manifest: [playnite-addon/installer.yaml](../playnite-addon/installer.yaml), which retains both v0.3.2 and prior v0.2.0.

PR #10 was merged to main on 2026-11-01. The public package was built, tested and published through the separately gated Windows release workflow, not copied from a draft CI artifact.

The official Playnite add-on database already has a PCLS listing pointing to this repository's public installer manifest. It may take time for the listing or installed application to refresh its cached installer data.

## Verified acceptance evidence

- Windows QA handoff: **435/435** tests passed with opt-in local-only real-rclone tests, including a cancellation/sidecar regression and safe retry.
- Installed disposable Playnite profiles exercised Desktop workflows, Fullscreen game actions, file-only uninstall/reinstall, settings migration, and an in-place v0.2.0 → v0.3.2 upgrade with recorded IDs and hashes preserved.
- Hosted GitHub Actions build on merged main succeeded, as did the one-time Windows release workflow.
- Official Toolbox addon and installer verification **passed against the previous public manifest** in an interactive Windows session. Reverification after the public v0.3.2 installer entry is a separate postpublication check and must not be represented as already completed.

## Known boundaries

- Recursive directory cache uninstall is intentionally disabled.
- Actual third-party cloud accounts and NAS environments were not used in the isolated Windows acceptance; real-world availability and permissions may differ.
- Playnite Fullscreen has game actions, but no separate PCLS setup wizard or dashboard.
- Some keyboard and alternate-DPI visual acceptance cases remain unverified.
- Cached-size display can overcount a single game when multiple games share the same cache directory.
- Three genuine privacy-safe Windows QA screenshots were selected for the public gallery, but the independent screenshot asset update requires a separate publication check; existing listing screenshot URLs remain unchanged.

Do not confuse this v0.3.2 public release with the future 1.0 milestone.
