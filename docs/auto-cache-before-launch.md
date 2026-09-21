# Auto Cache Before Launch

This is a 1.0 release-candidate boundary note, not a promise of automatic caching.

## Current Behavior

The plugin does not auto-download before launch.

Users manually trigger cache/download actions through install actions when the provider can resolve the source path.

## Possible future direction

Potential future work:

- optional cache-before-launch flow
- tighter install-state verification
- clearer queued download messaging
- safer retry and timeout handling

Any future auto-cache behavior must keep the current product boundaries:

- no bulk auto-download on startup
- no source/cloud deletion
- safe cache ownership checks
