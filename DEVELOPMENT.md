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

## Official Playnite release validation

Playnite distributes `Toolbox.exe` with Playnite. The validator searches an explicit
`-ToolboxPath`, `PLAYNITE_TOOLBOX`, and the documented installed/portable Playnite
locations, in that order. It does not download or redistribute Toolbox.

The known installed locations are `%LOCALAPPDATA%\Playnite\Toolbox.exe`,
`%LOCALAPPDATA%\Programs\Playnite\Toolbox.exe`, `%ProgramFiles%\Playnite\Toolbox.exe`,
and `%ProgramFiles(x86)%\Playnite\Toolbox.exe`. For a portable installation in any
other directory, use `-ToolboxPath` or `PLAYNITE_TOOLBOX`.

Set the executable explicitly when Playnite is installed elsewhere, then run the
non-mutating release gate:

```powershell
$env:PLAYNITE_TOOLBOX = 'C:\path\to\Playnite\Toolbox.exe'
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File .\tools\validate-release.ps1 `
  -PackagePath .\dist\PersonalCloudLibrarySource-0.3.2.pext
```

The script uses the official Playnite Toolbox syntax:
`pack <extensionfolder> <targetfolder>`, `verify addon <manifest_path>`, and
`verify installer <manifest_path>`. Packing uses a temporary staging/output folder;
the source manifests, workflow, and extension tree are not edited. The script also
parses all three YAML documents structurally and inspects the package's exact files,
identity, and manifest-derived version.

If Toolbox cannot be found, the release gate prints `PREREQUISITE_MISSING` and exits
with exit code 2. This is a blocked official qualification, not a pass. Hosted CI can
run the non-mutating contracts below, but a release still requires a recorded local
run with the official executable:

```powershell
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass `
  -File .\tools\test-release-validation.ps1
```
