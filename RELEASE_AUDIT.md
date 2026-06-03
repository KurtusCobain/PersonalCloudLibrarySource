# Release Audit

Manifest generation update completed 6/2/2026

## Executive Summary

Personal Cloud Library Source is close to a usable public prerelease. The blockers below were identified during the audit and should be rechecked after cleanup.

Core plugin behavior appears aligned with the release goal: normal Playnite GameLibrary import, cloud-only entries as uninstalled, cached entries with one named Play action, manual download/cache install action, and cache-only uninstall support. The main release risks are repository hygiene, prerelease version metadata, and public asset safety.

Audit was read-only except for creating this report. No source fixes were made in this pass.

## Release Blockers

1. `PersonalCloudLibrarySource/extension.yaml` used a prerelease version string.
   - Playnite extension versions should be valid .NET-style versions.
   - Recommended release value: `0.1.1` or `0.1.1.0`.
   - GitHub tag/release can still use prerelease naming if desired.

2. Build outputs are tracked in git and currently modified.
   - Tracked release-unsafe files include `PersonalCloudLibrarySource/bin/Debug/*.dll`, `*.pdb`, `extension.yaml`, `icon.png`, and many `PersonalCloudLibrarySource/obj/Debug/*` files.
   - These should be removed from git tracking before public release.

3. The original banner image is not public-safe.
   - It visibly includes recognizable commercial game titles/art-like cards in the background.
   - It is referenced by `README.md` and `docs/playnite-addon-listing.md`.
   - Replace with a fake/sample-only banner or remove from public docs before release.

4. Root testing docs include private/dev-specific naming.
   - Dev notes should use neutral remote names such as `my_remote`.
   - Root MVP test docs also contain local development paths.
   - Remove from public release, move under a clearly ignored/private dev folder, or rewrite with neutral fake names.

5. `diagnostics/last-import-diagnostics.txt` existed locally and contained a project-specific remote name.
   - It is ignored by `.gitignore`, but it should be deleted locally before packaging/release checks to avoid accidental inclusion.

## Must-Fix Before Release

- Change extension manifest version to a release-safe value.
- Remove tracked `bin/` and `obj/` files from git history/index for the release commit.
- Replace or remove the unsafe banner image from README/listing.
- Remove or neutralize `MVP2B_TESTING.md` private/dev references.
- Decide dependency strategy for `PersonalCloudLibrarySource/packages/`.
  - The repo currently tracks `PlayniteSDK.6.15.0` and `PlayniteSDK.6.16.0` DLLs.
  - The project references only `PlayniteSDK.6.16.0`.
  - Remove unused `PlayniteSDK.6.15.0`, or switch to a restore-based workflow and update packaging docs/script accordingly.
- Run a clean clone/package test after cleanup.

## Should-Fix Soon

- Confirm `CHANGELOG.md` uses the release version when cutting the release.
- Update `RELEASE_CHECKLIST.md` because it still says "Create the zip package"; the script now outputs `.pext`.
- Add a dedicated troubleshooting section titled "Difference between Remove and Uninstall".
  - The behavior is explained, but the requested exact topic heading is missing.
- Consider reducing duplicate disclaimer text in README/listing while keeping the legal language clear.
- Consider changing settings text boxes for enum-style fields into ComboBoxes later:
  - `SourceProviderType`
  - `UninstallBehavior`

## Nice-To-Have Later

- Add automated tests for path resolution and uninstall safety helpers.
- Add a CI build that restores dependencies and builds Release Any CPU.
- Add a package validation script that opens the `.pext` and verifies only runtime files are inside.
- Add a fake, public-safe Playnite library screenshot showing only:
  - Example Adventure
  - Example Puzzle Pack
  - Example Homebrew Demo
- Add hash verification/cache validation in a later phase.

## Files/Folders To Remove Or Rework

- Remove from git tracking:
  - `PersonalCloudLibrarySource/bin/`
  - `PersonalCloudLibrarySource/obj/`
- Remove local generated/ignored folder before release checks:
  - `diagnostics/`
- Review/remove or neutralize before public release:
  - `MVP0_TESTING.md`
  - `MVP2A_TESTING.md`
  - `MVP2B_TESTING.md`
- Remove unused package dependency folder if keeping packages checked in:
  - `PersonalCloudLibrarySource/packages/PlayniteSDK.6.15.0/`
- Keep unsafe banner out of public references:
  - `docs/images/dev-screenshots/pcls-banner-private-commercial-placeholder.png`

## Code Risk Notes

- `GetGames` imports every valid manifest item with `id` and `title`.
- Missing cached launch files import as uninstalled when `TreatMissingFilesAsUninstalled` is true.
- Cached/installed files get exactly one named Play action: `Play`.
- Cloud-only/missing files do not get a Play action.
- `GetInstallActions` checks plugin ownership, enabled state, downloads enabled, source path presence, missing cached launch file, and provider source resolvability.
- `RcloneInstallController` copies via rclone/local copy, verifies expected launch file exists, then calls `InvokeOnInstalled` with `GameInstallationData`.
- `GetUninstallActions` only exposes uninstall for cached entries with an existing launch file or install directory and a safe target path.
- `PersonalCloudLibraryUninstallController` deletes only the resolved cached target and calls `InvokeOnUninstalled`.
- Uninstall safety checks refuse empty paths, drive roots, `LocalCacheFolder` itself, and outside-cache targets unless explicitly allowed.
- rclone commands capture stdout/stderr, enforce timeout, and quote paths for spaces.
- JSON parsing trims UTF-8 BOM.
- Diagnostics use Playnite plugin user data with LocalAppData fallback, not the old hardcoded repo path.
- No obvious secrets/environment variables are logged.

Risk to review manually:
- `LocalFileCopier` allows overwrite of the destination cache file. This matches "copy to cache" behavior, but should be documented as default overwrite behavior.
- `OpenCacheFolder` creates the configured local folder if a non-empty path is provided. This is safe-local behavior, but it is still a settings-button side effect.
- `RcloneContentRoot` path-doubling is warning-only, not blocking.

## Docs/Readme Risk Notes

- README clearly distinguishes user install from developer build. User installation points to packaged `.pext`; `bin\Debug` is in developer instructions.
- rclone setup covers Google Drive, OneDrive, Dropbox, `rclone config`, `listremotes`, and `cat`.
- LocalFolder setup covers external drive, mapped network drive, NAS, and synced folder examples.
- Troubleshooting covers rclone not found, wrong remote path, JSON BOM/encoding, uninstalled item visibility, Playnite filters, missing download action, uninstall outside cache, and game remains after uninstall.
- Missing exact requested troubleshooting heading: "Difference between Remove and Uninstall".
- Metadata-before-download behavior is documented.
- Add-on listing avoids gameplay streaming claims and includes the "not a gameplay streaming service" disclaimer.

## Asset Safety Notes

- `docs/images/pcls-settings-provider.png` appears public-safe. It shows Playnite settings and neutral plugin settings. It does include common storefront names in Playnite's extension list, but not private content.
- `docs/images/pcls-settings-cache-uninstall.png` appears public-safe. It shows settings only.
- `docs/images/pcls-workflow.png` is generally public-safe, but wording like "Discover available packages from your personal cloud" could be interpreted as provider discovery. Consider changing to "Import entries from your manifest" in a future image.
- `docs/images/dev-screenshots/pcls-banner-private-commercial-placeholder.png` is not public-safe for README/listing because it shows recognizable commercial game titles/art-like cards.
- Keep private/dev screenshots out of public README and add-on listing references.

## Public Safety Scan Results

Targeted scan over source/docs/samples/tools found only legal disclaimer hits for ROMs/BIOS/cracks/keys/download sources.

Broader repo scan found:
- Dev testing notes should use neutral remote names such as `my_remote`.
- Root MVP docs include local development paths such as `D:\PersonalCloudLibrarySource` and `D:\PersonalCloudLibraryCache`.
- `diagnostics/last-import-diagnostics.txt` is ignored but should be absent locally before packaging.

No private branding names, credential material, Playnite database files, or private Drive IDs were found in the release source/docs scan.

## Release Test Matrix

| Test | Status | Notes |
|---|---:|---|
| LocalFile import | Pending final RC test | Use public sample manifest with 3 fake entries. |
| LocalFolder import/copy | Pending final RC test | Use a fake local source root and cache folder. |
| RcloneRemote import/download | Pending final RC test | Use a neutral fake rclone remote name, not private remote names. |
| Metadata before download | Pending final RC test | Confirm cloud-only entry can receive Playnite metadata before cache download. |
| Download/cache | Pending final RC test | Confirm `Download to local cache` copies the expected fake file. |
| Play cached file | Pending final RC test | Confirm cached fake `.bat` launches locally through Playnite. |
| Uninstall cache | Pending final RC test | Confirm Playnite Uninstall removes only cache target. |
| Entry remains cloud-only | Pending final RC test | After uninstall + library update, entry remains visible as uninstalled. |
| Package `.pext` | Pending final RC test | Run package script after version/build-output cleanup. |
| Clean Playnite install test | Pending final RC test | Install `.pext` into a clean Playnite portable/profile. |

## Recommended Next Commit Plan

1. Cleanup commit:
   - Remove tracked `bin/` and `obj/` files.
   - Delete ignored local `diagnostics/`.
   - Remove unused `PlayniteSDK.6.15.0` package folder or document dependency restore.

2. Release metadata commit:
   - Change `extension.yaml` to `Version: 0.1.1`.
   - Update package script output name to `PersonalCloudLibrarySource-0.1.1.pext`.
   - Update `CHANGELOG.md` and `RELEASE_CHECKLIST.md`.

3. Public asset/docs commit:
   - Replace/remove unsafe banner.
   - Remove or neutralize root MVP testing docs.
   - Add explicit "Difference between Remove and Uninstall" troubleshooting section.

4. Verification:
   - Build Release Any CPU.
   - Run package script.
   - Verify `.pext` contents.
   - Run clean Playnite install test.
