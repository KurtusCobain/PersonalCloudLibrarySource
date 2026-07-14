# Upgrades

The live development version is 0.3.2. A final 1.0 package has not been published from this branch.

## Before upgrading

1. Close active transfers and let Playnite finish library updates.
2. Back up Playnite's configuration and plugin user-data directory.
3. Record the provider, manifest path, library/content root, cache root, and rclone remote name.
4. Keep the existing cache and source content in place during the first post-upgrade check.

## Settings migration

PCLS migrates settings through each schema version in order and records the current schema after migration. Existing configured values are retained where possible. The old default rclone timeout of 30 seconds migrates to the safer 90-second inactivity default; a user-selected custom timeout is preserved.

After upgrading, open settings in Playnite Desktop and run **Verify setup**. Confirm provider, manifest, source/content root, cache root, download permission, uninstall behavior, notification preferences, and timeout before updating the library.

## Qualification status

Automated fixtures cover sequential and idempotent migration, older/default values, custom values, corrupt input fallback, and save/cancel behavior. Installed upgrades from 0.1.1 and 0.2.0 are still release-qualification rows. Do not treat them as manually passed until the release checklist records the Playnite run.

If an upgrade does not behave as expected, restore the backup and attach the public-safe verification report and relevant diagnostics to a private support report. Do not publish credentials or a complete private manifest.
