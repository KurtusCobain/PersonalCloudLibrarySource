# Manifest Format

Personal Cloud Library Source reads a JSON manifest with a top-level `version` and an `items` array.

## Version 3 Recommended Format

Version 3 manifests should prefer `sourcePath`, `sourceType`, and `cachePath`.

```json
{
  "version": 3,
  "generatedBy": "Personal Cloud Library Source manifest generator",
  "generatedAt": "2026-06-02T00:00:00Z",
  "sourceMode": "filesystem",
  "itemCount": 1,
  "items": [
    {
      "id": "example-adventure",
      "title": "Example Adventure",
      "platform": "Example Platform",
      "sourcePath": "ExampleAdventure/ExampleAdventure.bat",
      "sourceType": "file",
      "cachePath": "ExampleAdventure\\ExampleAdventure.bat",
      "installDirectory": "ExampleAdventure",
      "launchFile": "ExampleAdventure.bat",
      "notes": "Fake local sample entry for testing."
    }
  ]
}
```

## Version 1 Compatibility

Version 1 manifests remain supported. Existing fields such as `localPath`, `installDirectory`, `launchFile`, and legacy `remotePath` can still be used.

Version 2 manifests remain supported.

New manifests should use `sourcePath` instead of `remotePath`.

## Fields

- `version`: Manifest schema version.
- `items`: Array of library entries to import.
- `generatedBy`: Optional generator description.
- `generatedAt`: Optional UTC ISO timestamp.
- `sourceMode`: Optional source mode summary such as `filesystem` or `rclone`.
- `itemCount`: Optional generated item count.
- `id`: Stable item identifier. Keep this value stable between imports so Playnite can recognize the same entry.
- `title`: Display name shown in Playnite.
- `platform`: Optional platform label for the entry.
- `sourcePath`: Provider source path used for install/download actions.
- `sourceType`: Optional source kind. Use `file` or `directory`. Missing or blank values default to `file`.
- `packageRole`: Optional package label such as `game`, `dlc`, or `update`.
- `cachePath`: Preferred local cached launch file path. It can be absolute or relative to `LocalCacheFolder`.
- `localPath`: Legacy cached launch file path. It can be absolute or relative to `LocalCacheFolder`.
- `installDirectory`: Legacy cached install directory. It can be absolute or relative to `LocalCacheFolder`.
- `launchFile`: Launch file name used with `installDirectory`, and useful as a clear launch-file hint with `cachePath`.
- `remotePath`: Legacy fallback for `sourcePath`.
- `notes`: Optional text imported as the Playnite description.

## Path Resolution

`cachePath` is preferred for the local cached launch file. If `cachePath` is not present, the plugin falls back to `localPath`, then `installDirectory + launchFile`.

`sourcePath` is preferred for provider source files. If `sourcePath` is not present, the plugin falls back to legacy `remotePath`.

`sourcePath` points to the source provider path. In LocalFolder mode it is relative to `LocalLibraryRoot`. In RcloneRemote mode it is relative to `RcloneContentRoot` when that setting is provided.

`cachePath` points to the local cache destination. It can be absolute, but relative paths are usually better because they resolve inside `LocalCacheFolder`.

For `sourceType = directory`, `sourcePath` points to the package folder. `installDirectory` should point to the copied folder, and `cachePath` may point to the preferred launch file inside that copied folder. Wii U-style packages may leave `launchFile` blank.

Cloud-only items are normal. If the cached launch file is missing, the item should still import and appear as uninstalled when `TreatMissingFilesAsUninstalled` is enabled.

After import, Playnite metadata tools can enrich entries with covers, descriptions, genres, screenshots, and other metadata before the item is downloaded or copied into the local cache.

## Provider Behavior

`LocalFile` reads `LocalManifestPath`. If downloads are used, `sourcePath` can be absolute or relative to the manifest folder.

`LocalFolder` reads `LocalLibraryRoot + ManifestRelativePath` and copies files or directories from `LocalLibraryRoot + sourcePath`.

`RcloneRemote` reads the manifest with `rclone cat RcloneRemoteName:RcloneManifestPath`. Downloads use `rclone copyto` for `sourceType = file` and `rclone copy` for `sourceType = directory`. If `RcloneContentRoot` is empty, `sourcePath` is used directly.

When `RcloneContentRoot` is set, keep `sourcePath` relative to that root. Do not repeat the content root inside each item.

Correct:

```text
RcloneContentRoot = PersonalLibrary/files
sourcePath = Game/Game.exe
```

Incorrect:

```text
RcloneContentRoot = PersonalLibrary/files
sourcePath = PersonalLibrary/files/Game/Game.exe
```

The incorrect form becomes `PersonalLibrary/files/PersonalLibrary/files/Game/Game.exe`.

When `TreatMissingFilesAsUninstalled` is true, entries with missing cached launch files are imported as uninstalled. The plugin does not auto-download before launch.
