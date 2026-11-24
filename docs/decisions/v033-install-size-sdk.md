# PCLS install-size correctness: Playnite SDK compatibility decision (draft)

Date: 2026-11-01. Status: **review required before PR #15 merge**.

## Confirmed host behavior

In [Playnite's GamesEditor](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/GamesEditor.cs):

- After a game-installed controller event, Playnite copies `InstalledInfo.InstallDirectory` and optional `InstalledInfo.Roms` to the database and immediately calculates installed size.
- `CalculateGameSize` measures **all bytes beneath `InstallDirectory`** if there are no ROM entries, regardless of which particular game they belong to.
- If there are ROM entries, Playnite instead sums those files' sizes. The ROM path branch has special playlist treatment for `.cue`, `.m3u` and `.gdi`.
- A blank `InstallDirectory` without ROMs leads `CalculateGameSize` to return null, preserving any size already assigned by the plugin.
- Native `OpenGameLocation` immediately returns if `InstallDirectory` is blank.
- During normal `GameDatabase.ImportGames`, an **already installed game's** `InstallSize` is not overwritten by a later plugin metadata value; it only updates existing install size when the game is *not* marked installed.

## Existing shipped v0.3.2 problem

PCLS correctly resolves a file-type item with a `cachePath` such as `tiny.cmd`, but when no `installDirectory` is explicit, `CachePathResolver` exposes the cached file's *parent directory* as an installation directory. If other game files share this folder, Playnite's own size scanner reports their combined size for the tiny file game. An isolated Windows QA reproduction showed a ~176-byte game displayed as ~512 MiB.

## Candidate A — blank Playnite installation directory and exact file size

**Implemented in draft PR #15, not published.** PCLS preserves the actual cached file, game ID, launch path, launch working directory, transfer path and uninstall authorization, but reports a blank Playnite `InstallDirectory` for an implicit single-file game. It reports the exact cached file's byte count through `GameMetadata.InstallSize` and game state. Explicit install directory and directory-type games are unchanged.

**Pros:** smallest change; no extra ROM metadata or cache moves; avoids shared directory scans; no impact on actual Play action.

**Cons and qualification:**
- Native **Open installation folder** becomes unavailable for these entries.
- Existing already-installed games with incorrect size may not be repaired by an ordinary library refresh. Updating such values requires a separate, deliberate migration or user action that must respect manually edited metadata.
- An implicit file-type game is assumed to own the cached **file only**, not siblings. If a package requires a collection of files, it should be declared as a directory-type item rather than reporting only the launcher.
- Additional Windows Playnite acceptance is required on exact draft head, see [host test](../tests/v033-install-size-host-acceptance.md).

## Candidate B — preserve install folder, use Playnite ROMs size scan

The Playnite SDK accepts `GameInstallationData.Roms` and `GameMetadata.Roms`. Recording one cached file as a `GameRom` may allow a file-specific size calculation **without** removing `InstallDirectory`, preserving native Open installation folder.

**Potential drawbacks:** PCLS file items can be arbitrary local game executable files, not necessarily ROMs. Populating `Roms` changes visible Playnite game metadata and might overwrite existing user-selected ROMs in a subsequent install event. Files ending `.cue`, `.m3u`, or `.gdi` trigger playlist expansion, not a strict one-file size, making the result provider-/format-sensitive. A full migration must not silently adopt or discard edited ROM metadata.

**Status:** analysis only; **not** implemented or approved. Do not call this a safe drop-in replacement without isolated host testing and a clear metadata policy.

## Candidate C — physically isolate each game in its own cache directory

Could preserve native install directory and full folder-size scanning, but would require changing cache destinations, receipts, old manifest paths and launch/uninstall semantics. It risks breaking existing collections and should **not** be used as a cosmetic bug fix.

## Required signoff

Choose explicitly between the accepted UX limitation of A and a separately tested metadata model such as B, and decide how already-installed inflated sizes should be handled. No automatic mutation of legacy caches or user-editable ROM metadata. Reject any method that misattributes another game's bytes or changes source and deletion boundaries.

Published v0.3.2 is unaffected. This ADR is an engineering record for [issue #12](https://github.com/KurtusCobain/PersonalCloudLibrarySource/issues/12).
