# Local Folder, External Drive, and NAS Setup

![Provider settings](images/pcls-settings-provider.png)

Use `LocalFolder` when your manifest and source files are available through a normal filesystem path.

Good fits include external drives, mapped network drives, NAS shares, and synced cloud folders that already exist on disk.

These setups do not require rclone.

## Folder Layout Example

```text
E:\PersonalLibrary
  personal-cloud-library.sample.json
  ExampleAdventure
    ExampleAdventure.bat
```

## Plugin Settings

```text
SourceProviderType = LocalFolder
LocalLibraryRoot = E:\PersonalLibrary
ManifestRelativePath = personal-cloud-library.sample.json
LocalCacheFolder = D:\PersonalCloudLibraryCache
AllowDownloads = true
TreatMissingFilesAsUninstalled = true
```

## NAS Example

```text
LocalLibraryRoot = \\NAS\PersonalLibrary
ManifestRelativePath = personal-cloud-library.sample.json
```

## More Local Root Examples

External drive:

```text
LocalLibraryRoot = E:\PersonalLibrary
```

Mapped network drive:

```text
LocalLibraryRoot = Z:\PersonalLibrary
```

NAS UNC path:

```text
LocalLibraryRoot = \\NAS\PersonalLibrary
```

Synced cloud folder:

```text
LocalLibraryRoot = C:\Users\You\CloudDrive\PersonalLibrary
```

`sourcePath` values in the manifest are resolved relative to `LocalLibraryRoot`.

## Generate a Manifest from a Filesystem Root

Filesystem:

```powershell
.\tools\generate-manifest.ps1 `
  -SourceRoot "D:\Games" `
  -OutputPath ".\personal-cloud-library.generated.json" `
  -Overwrite
```

External drive:

```powershell
.\tools\generate-manifest.ps1 `
  -SourceRoot "E:\ROMs" `
  -OutputPath ".\personal-cloud-library.generated.json" `
  -Overwrite
```

Mapped drive:

```powershell
.\tools\generate-manifest.ps1 `
  -SourceRoot "Z:\ROMCade" `
  -OutputPath ".\personal-cloud-library.generated.json" `
  -Overwrite
```

NAS:

```powershell
.\tools\generate-manifest.ps1 `
  -SourceRoot "\\NAS\Games" `
  -OutputPath ".\personal-cloud-library.generated.json" `
  -Overwrite
```

Dry run:

```powershell
.\tools\generate-manifest.ps1 `
  -SourceRoot "D:\Games" `
  -DryRun
```

![Cache and uninstall settings](images/pcls-settings-cache-uninstall.png)

Use `LocalCacheFolder` for files copied from the local folder, external drive, mapped drive, NAS, or synced cloud folder before Playnite launches them.
