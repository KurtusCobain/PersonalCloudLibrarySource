# Security Policy

## Reporting

Please open a private security report through GitHub security reporting if you believe you found:

- unsafe uninstall/delete behavior
- path traversal or cache ownership issues
- unsafe command execution around rclone
- exposure of private manifest, cloud, or diagnostics data

## Project Boundaries

This plugin should never:

- provide games or download sources
- delete source/cloud files
- auto-download a full cloud library on startup
- write mutable user data into the extension install folder
