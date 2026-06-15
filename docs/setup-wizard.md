# Setup Wizard Flow

This document describes the intended v0.2 guided setup flow inside Playnite.

## Goal

A normal Playnite user should be able to:

1. Open **Extension settings -> Libraries -> Personal Cloud Library Source**
2. Choose a source mode
3. Generate or select a manifest
4. Verify the setup
5. Run **Update Game Library**

## Local Folder or NAS Flow

1. Choose `LocalFolder`.
2. Browse to the local library root.
3. Click **Generate manifest from folder**.
4. Review the detected item count, skipped count, and warnings.
5. Save settings.
6. Run **Update Game Library**.

The generated manifest is stored under the plugin user data path, not under the extension install folder.

## Local JSON Manifest Flow

1. Choose `LocalFile`.
2. Browse to a manifest JSON file.
3. Choose a local cache folder.
4. Click **Verify setup**.
5. Save settings.
6. Run **Update Game Library**.

## Rclone Flow

1. Choose `RcloneRemote`.
2. Set the rclone executable path if `rclone` is not on `PATH`.
3. Enter the remote name.
4. Enter the manifest path inside the remote.
5. Enter `RcloneContentRoot` only if item `sourcePath` values should resolve beneath a remote subfolder.
6. Click **Test rclone connection**.
7. Click **Verify setup**.
8. Save settings.
9. Run **Update Game Library**.

## Current Limitation

The v0.2 pass focuses on in-Playnite manifest generation for local and NAS-style sources first.

Advanced rclone-based manifest generation can still use:

- `tools/generate-manifest.ps1`
- `tools/generate-rclone-manifest.ps1`
