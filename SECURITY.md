# Security Policy

## Reporting

Please open a private security report through GitHub security reporting if you believe you found:

- unsafe uninstall/delete behavior
- path traversal or cache ownership issues
- unsafe command execution around rclone
- exposure of private manifest, cloud, or diagnostics data
- unsafe rclone argument or provider-path handling

## Project Boundaries

This plugin should never:

- provide games or download sources
- delete source/cloud files
- auto-download a full cloud library on startup
- write mutable user data into the extension install folder

Cache removal is expected to stay within the configured cache by default and to refuse roots, the cache root, and unsafe reparse-point paths. Reports should remain summary-oriented and must not publish credentials or full private manifest inventories.
