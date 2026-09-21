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
2. Run the full NUnitLite suite, including repository documentation and distribution contracts.
3. Check that `extension.yaml`, `playnite-addon/addon-database.yaml`, and `playnite-addon/installer.yaml` preserve the stable ID and are not contradictory.
4. Do not commit generated manifests, reports, `bin/`, `obj/`, `dist/`, restored packages, ROMs, BIOS files, keys, cracks, or personal library content.
5. Keep examples generic and reports free of private provider paths or manifest inventories.
6. Do not report installed Desktop, Fullscreen, provider, or upgrade rows as passing unless they were actually run in Playnite.

## Packaging Notes

Local packaging is documented in [DEVELOPMENT.md](DEVELOPMENT.md).
