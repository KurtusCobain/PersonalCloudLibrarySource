# Development

## Build

This project targets the Playnite-supported .NET Framework version already configured in the project file.

On Windows with Visual Studio MSBuild available:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' `
  .\PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln `
  /t:Build `
  /p:Configuration=Debug
```

## Developer Extension Loading

Add the build output folder to Playnite as a developer extension:

```text
.\PersonalCloudLibrarySource\bin\Debug
```

## Packaging

The repo includes `tools/package-extension.ps1` for local packaging.

Expected output:

- `dist/PersonalCloudLibrarySource-<version>.pext`

## Manual Verification

Before publishing a release:

1. Build Release.
2. Package the extension.
3. Verify `extension.yaml`.
4. Verify `playnite-addon/addon-database.yaml`.
5. Verify `playnite-addon/installer.yaml`.
6. Confirm `AddonId` matches everywhere.

If Playnite Toolbox is available locally, use its pack/verify commands as part of release validation.
