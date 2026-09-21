# PCLS Current Release Status

Last updated: 2026-09-21

## Public package

- Current published release: **v0.2.0**
- Current Playnite installer-feed package: **0.2.0**
- Existing v0.2.0 GitHub Release and `.pext` remain unchanged.
- No 0.3.2 or 1.0 package is currently advertised to installed users.

## Source state

- Default branch: `main`
- Consolidated source merge: `12705ed2b672ae745580071a61cdf1b6cd2ecd92`
- Merged RC source head: `1c59e0f4e344fc419c5fc7bb913ec09989d0632c`
- Source manifest version: **0.3.2**
- Intended release-candidate scope: eventual **1.0**, subject to explicit version-promotion approval
- PR #9: merged for source consolidation
- Post-merge GitHub Actions build run #92: **passed**

The source merge and public package release are intentionally separate. A developer reading `main` sees the current release-candidate implementation; an installed Playnite user remains on the published v0.2.0 package until the release gate below is completed.

## Automated qualification already recorded

The reviewed RC head recorded:

- 86/86 tests passing in GitHub Actions run #91;
- successful Debug build;
- successful Release/package build;
- package-content inspection;
- validated branding dimensions/assets;
- a retained 0.3.2 test artifact and recorded SHA-256 evidence.

After merging that exact source into `main`, build run #92 also completed successfully.

## Remaining public-release gate

Do not add a 0.3.2/1.0 entry to `playnite-addon/installer.yaml`, create a new GitHub Release, move/replace the v0.2.0 release, or publish a new package until real Windows/Playnite acceptance covers:

- clean installed-provider behavior;
- Desktop visual smoke test;
- Fullscreen play/install/uninstall workflow;
- upgrade from v0.2.0 with settings/data preserved;
- final packaged `.pext` install and smoke test;
- high-DPI presentation;
- any remaining provider/environment rows required by the release checklist.

See:

- [1.0 release checklist](superpowers/verification/pcls-1.0-release-checklist.md)
- [Known limits](known-limits.md)
- [Release-candidate notes](playnite-release-notes.md)

## Version decision at release time

The repository currently keeps the implementation identity at **0.3.2** while the broader 1.0 qualification is incomplete.

When manual acceptance is complete, make an explicit release decision:

1. publish the qualified build as **0.3.2**, preserving 1.0 for a later milestone; or
2. complete the remaining 1.0 synchronization gates and promote the same qualified scope to **1.0.0**.

Do not change this version identity merely as repository cleanup.
