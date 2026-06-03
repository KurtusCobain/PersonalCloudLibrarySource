# Release Checklist

Manifest generation update completed 6/2/2026

## Build

- Build Release Any CPU.
- Confirm `PersonalCloudLibrarySource.dll`, `extension.yaml`, and `icon.png` exist in the Release output.
- Create the `.pext` package with `tools/package-extension.ps1`.

## Manual Playnite Tests

- Run LocalFile test.
- Run LocalFolder test.
- Run RcloneRemote test.
- Run manifest generator test for filesystem mode.
- Run manifest generator test for rclone mode.
- Confirm directory-package items copy full folders for both local and rclone providers.
- Test missing-file `Download to local cache`.
- Confirm cached/downloaded fake launchers launch from Playnite.

## Public Safety

- Confirm no private strings are present.
- Confirm no build outputs are committed.
- Confirm the package does not include `bin`, `obj`, `.git`, `diagnostics`, or `samples/dev-tests`.

## Clean Profile

- Install the package into a clean Playnite portable install or clean profile if possible.
- Confirm the plugin appears as `Personal Cloud Library Source`.
- Confirm the plugin loads into Playnite's normal library view.
