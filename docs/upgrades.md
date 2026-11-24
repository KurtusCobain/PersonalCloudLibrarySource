# Upgrades

The current **public** release is [v0.3.2](https://github.com/KurtusCobain/PersonalCloudLibrarySource/releases/tag/v0.3.2), published November 1, 2026. The 1.0 milestone remains future work; version 1.0 has not been published.

## Before upgrading

1. Close active transfers and let Playnite finish library updates.
2. Back up Playnite's configuration and plugin user-data directory.
3. Record the provider, manifest path, library/content root, cache root, and rclone remote name.
4. Keep the existing cache and source content in place during the first post-upgrade check.

## Settings migration

PCLS migrates settings through each schema version in order and records the current schema after migration. Existing configured values are retained where possible. The old default rclone timeout of 30 seconds migrates to the safer 90-second inactivity default; a user-selected custom timeout is preserved.

After upgrading, open settings in Playnite Desktop and run **Verify setup**. Confirm provider, manifest, source/content root, cache root, download permission, uninstall behavior, and timeout before updating the library.

### Changes to review before enabling downloads

- **All new cached installs must stay inside the configured `LocalCacheFolder`.** Older absolute `cachePath` / `installDirectory` values can still identify existing files, but an Install action targeting a different location is now refused. To download again, change those paths to point under your chosen cache. The separate outside-cache *uninstall* option does not authorize outside-cache downloads.
- Incomplete transfer cleanup and staged destination verification are always enforced. The two old checkboxes had no runtime effect and are no longer displayed.
- Five notification preferences were also removed from the UI because they did not control runtime behavior. Their serialized settings fields are retained for migration compatibility, but should not be treated as functional notification filters.
- Extremely large catalogs now receive a clear refusal instead of consuming unbounded memory (local manifest file limit: 32 MiB; parsed input: 32 million characters; maximum 100,000 entries).

If any catalog uses an absolute path outside your cache, **do not move or delete its existing content during upgrade**. First inspect that item's Play/Install availability and choose a safe migration path using a disposable test library.

## Qualification status

Automated fixtures cover sequential and idempotent migration, older/default values, custom values, corrupt input fallback and save/cancel behavior. **An isolated installed v0.2.0 → v0.3.2 upgrade passed Windows acceptance** on November 1, 2026: both disposable games, IDs, installed states and eight source/cache/manifest hashes were preserved; 24 legacy settings were unchanged and the default timeout migrated 30 → 90 seconds. An installed v0.1.1 → v0.3.2 upgrade and upgrades with real cloud/NAS configurations have **not** been separately verified. Do not generalize the two-game fixture result to every personal library.

If an upgrade does not behave as expected, restore the backup and attach the public-safe verification report and relevant diagnostics to a private support report. Do not publish credentials or a complete private manifest.
