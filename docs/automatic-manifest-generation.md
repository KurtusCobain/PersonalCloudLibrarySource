# Automatic Manifest Generation

The v0.2 pass adds local manifest generation directly inside Playnite settings.

## What It Scans

The local generator scans a chosen filesystem root and looks for:

- supported single-file launch targets
- directory packages with a detectable launch file
- Wii U-style `code/content/meta` packages

## What It Skips

It skips obvious non-launchable content such as:

- metadata folders
- artwork caches
- save data
- screenshots
- `.bin` sidecar files as standalone entries

Some legacy folder names remain excluded for compatibility with older private library layouts.

## Generated Output

The generator writes:

- a v3 manifest JSON
- a plain-text report

Both are written to the plugin user data path.

## Settings Updated After Generation

When generation succeeds, the plugin updates settings to support a LocalFolder workflow:

- `SourceProviderType = LocalFolder`
- `LocalLibraryRoot = chosen root`
- `LocalManifestPath = generated manifest path`
- `LocalCacheFolder = default plugin cache path if empty`

## User Follow-Up

After generation:

1. Save settings.
2. Run **Update Game Library** in Playnite.
3. Review imported entries.

Use **Verify setup**, **Open generated manifest**, and **Open generated report** if you need to troubleshoot the result.
