# Development

## Build

This project targets the Playnite-supported .NET Framework version already configured in the project file.

On Windows with Visual Studio 2022 Build Tools MSBuild available:

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' `
  .\PersonalCloudLibrarySource.Tests\PersonalCloudLibrarySource.Tests.csproj `
  /t:Rebuild `
  /p:Configuration=Debug
```

Run the NUnitLite suite after a successful build:

```powershell
.\PersonalCloudLibrarySource.Tests\bin\Debug\PersonalCloudLibrarySource.Tests.exe --labels=Off
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
7. Run the documentation/link/version/YAML/image contracts.
8. Complete the installed Desktop, Fullscreen, provider, and upgrade matrix rather than inferring it from unit tests.

The live pre-1.0 version is `0.3.2`. Do not add a 1.0 installer entry or final package URL/date/checksum until release qualification is complete.

If Playnite Toolbox is available locally, use its pack/verify commands as part of release validation.
