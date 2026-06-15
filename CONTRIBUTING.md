# Contributing

## Scope

This repository is a Playnite `GameLibrary` plugin.

Keep changes aligned with:

- .NET Framework compatibility used by the project
- Playnite SDK APIs
- stable extension ID and plugin GUID
- safe cache/install/uninstall behavior

## Preferred Change Style

- small, reviewable changes
- safe service extraction over risky rewrites
- user-facing settings improvements over debug-only surfaces
- public-safe docs and examples

## Before Opening a PR

1. Build the solution.
2. Check that `extension.yaml`, `playnite-addon/addon-database.yaml`, and `playnite-addon/installer.yaml` are not contradictory.
3. Do not commit generated manifests, reports, `bin/`, `obj/`, `dist/`, test libraries, ROMs, BIOS files, keys, cracks, or personal library content.
4. Keep examples generic.

## Packaging Notes

Local packaging is documented in [DEVELOPMENT.md](DEVELOPMENT.md).
