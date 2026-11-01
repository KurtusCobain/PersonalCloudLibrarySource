# PR #15 — one-session Windows Playnite size acceptance

**Purpose:** qualify [issue #12](https://github.com/KurtusCobain/PersonalCloudLibrarySource/issues/12) without repeating the full release acceptance matrix. **Run only in a disposable portable Playnite profile.** Do not use everyday ROMs, saves, private manifests, cloud remotes or an existing Playnite installation.

## Preflight

- Candidate: exact artifact from the latest successful CI for [draft PR #15](https://github.com/KurtusCobain/PersonalCloudLibrarySource/pull/15), not published v0.3.2.
- Confirm the extension's manifest identifies a test build; **do not upgrade or overwrite the public installation**.
- Back up the disposable profile, and retain the separate v0.3.2 profile for comparison.
- Create a separate temporary source folder and cache folder under the test workspace. Keep original source files untouched. No recursive directory uninstall test.

## Minimal test files

- `SOURCE\tiny.cmd`: 176 bytes of harmless inert test content.
- `SOURCE\stress.bin`: 512 MiB of disposable random data.
- `CACHE\tiny.cmd` and `CACHE\stress.bin`: only created by testing PCLS's normal file cache install in the isolated profile; don't prepopulate before the test.
- Manifest: two distinct **file-type** items, unique IDs, distinct `sourcePath` / `cachePath` destinations, **no explicit** `installDirectory`, sharing only the `CACHE` parent.

Do not execute the fake test file. Launch-path verification means inspect the action's path and working directory in Playnite's game properties; a test file need not be executed.

## Actions and expected evidence

| Step | Action | PASS criterion |
|---|---|---|
| 1 | Import both entries in disposable Playnite Desktop | Two stable distinct game IDs, both initially uninstalled |
| 2 | Install `tiny.cmd` | Cached file hash unchanged from source, installed state and action present; size reflects **176 bytes** |
| 3 | Install `stress.bin` | The tiny game's size remains **176 bytes**, not 512 MiB; neither file overwritten |
| 4 | Use **Update Game Library** | Both game IDs and install states preserved; tiny game's size remains correct |
| 5 | Run Playnite's **Calculate Install Size** for tiny game | Tiny game's value is not replaced with the shared directory's 512 MiB total |
| 6 | Inspect game launch action and working folder | Correct `CACHE\tiny.cmd` launch path and `CACHE` working directory |
| 7 | Inspect Playnite's **Open installation folder** option | Record whether missing, disabled or functional; this is a known compatibility trade-off and **not** an automatic PASS |
| 8 | Install a file-type entry with **explicit** `installDirectory` | Existing explicit-directory behavior and normal Playnite size scan are unchanged |
| 9 | Inspect a directory-type game | No regression to its directory path, IsInstalled or launch action |
| 10 | File-only uninstall tiny game, **only with explicit approval for that disposable file** | Deletes tiny file only; 512 MiB sibling, source files and game metadata untouched; reimport/redo should work |

## Already-installed v0.3.2 upgrade case

Create a **separate disposable profile** where the tiny game already has an intentionally incorrect stored size (simulating the 512 MiB overcount). Upgrade only that profile to the PR #15 candidate and run **Update Game Library**. Playnite's upstream `GameDatabase.ImportGames` generally does **not** overwrite `InstallSize` for already-installed games, so this is expected to remain stale. Record the observed result. **Do not describe an unchanged legacy value as a pass or silently reset user-edited metadata.**

## Evidence and restrictions

- Record Playnite version, candidate commit SHA, whether installed-size is shown in bytes/KiB, and exact saved game IDs before/after.
- Save **at most** three genuine privacy-reviewed screenshots: tiny game's size after stress cache, action/working directory, and the Open installation folder menu state.
- Report `PASS / FAIL / BLOCKED` for each row. If a meaningful failure appears, stop; don't automatically rerun destructive operations or use personal game data.
- This acceptance is a prerequisite to merging the **draft** code. No tag, GitHub release, or Playnite installer-feed changes are authorized by this document.
