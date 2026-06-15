# Auto Cache Before Launch

This is a v0.3 planning note, not current behavior.

## Current Behavior

The plugin does not auto-download before launch.

Users manually trigger cache/download actions through install actions when the provider can resolve the source path.

## v0.3 Direction

Potential future work:

- optional cache-before-launch flow
- tighter install-state verification
- clearer queued download messaging
- safer retry and timeout handling

Any future auto-cache behavior must keep the current product boundaries:

- no bulk auto-download on startup
- no source/cloud deletion
- safe cache ownership checks
