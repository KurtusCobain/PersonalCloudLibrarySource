# Local Folder, External Drive, and NAS Setup

![Provider settings](images/pcls-settings-provider.png)

Use `LocalFolder` when your manifest and source files are available through a normal filesystem path.

Good fits include external drives, mapped network drives, NAS shares, and synced cloud folders that already exist on disk.

Local folders, external drives, mapped network drives, NAS shares, and synced local cloud folders do not require rclone.

## Folder Layout Example

```text
E:\PersonalLibrary
  personal-cloud-library.sample.json
  ExampleAdventure
    ExampleAdventure.bat
```

## Generated-Manifest Workflow

1. Run `tools/generate-manifest.ps1` against the local filesystem root you want to catalog.
2. Place the generated JSON inside the library root, or set `ManifestRelativePath` to point at it.
3. Configure `LocalLibraryRoot` and `LocalCacheFolder` in the plugin settings.
4. Run **Update Game Library** in Playnite.

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
  -SourceRoot "D:\PersonalLibrary" `
  -OutputPath ".\personal-cloud-library.generated.json" `
  -Overwrite
```

External drive:

```powershell
.\tools\generate-manifest.ps1 `
  -SourceRoot "E:\PersonalLibrary" `
  -OutputPath ".\personal-cloud-library.generated.json" `
  -Overwrite
```

Mapped drive:

```powershell
.\tools\generate-manifest.ps1 `
  -SourceRoot "Z:\PersonalLibrary" `
  -OutputPath ".\personal-cloud-library.generated.json" `
  -Overwrite
```

NAS:

```powershell
.\tools\generate-manifest.ps1 `
  -SourceRoot "\\NAS\PersonalLibrary" `
  -OutputPath ".\personal-cloud-library.generated.json" `
  -Overwrite
```

Dry run:

```powershell
.\tools\generate-manifest.ps1 `
  -SourceRoot "D:\PersonalLibrary" `
  -DryRun
```

![Cache and safe uninstall settings](images/pcls-settings-cache-safety.png)

Use `LocalCacheFolder` for files copied from the local folder, external drive, mapped drive, NAS, or synced cloud folder before Playnite launches them.
