param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$ToolboxPath,
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ReleaseValidation.psm1') -Force

try {
    $root = (Resolve-Path -LiteralPath $RepositoryRoot).Path
    $extensionPath = Join-Path $root 'PersonalCloudLibrarySource\extension.yaml'
    $addonPath = Join-Path $root 'playnite-addon\addon-database.yaml'
    $installerPath = Join-Path $root 'playnite-addon\installer.yaml'
    $surface = Assert-ReleaseSurfaces -ExtensionPath $extensionPath -AddonPath $addonPath -InstallerPath $installerPath

    $toolbox = Find-PlayniteToolbox -ExplicitPath $ToolboxPath
    if (-not $toolbox) {
        [Console]::Error.WriteLine('PREREQUISITE_MISSING: Toolbox.exe was not found. Pass -ToolboxPath, set PLAYNITE_TOOLBOX, or install Playnite in a documented location.')
        exit 2
    }

    $releaseOutput = Join-Path $root 'PersonalCloudLibrarySource\bin\Release'
    if (-not (Test-Path -LiteralPath (Join-Path $releaseOutput 'PersonalCloudLibrarySource.dll'))) {
        throw "Release build output is missing: $releaseOutput"
    }

    $workspace = Join-Path ([IO.Path]::GetTempPath()) ('pcls-toolbox-validation-' + [guid]::NewGuid().ToString('N'))
    $stage = Join-Path $workspace 'extension'
    $officialOutput = Join-Path $workspace 'official'
    New-Item -ItemType Directory -Path (Join-Path $stage 'Localization') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $stage 'Assets') -Force | Out-Null
    New-Item -ItemType Directory -Path $officialOutput -Force | Out-Null
    try {
        Copy-Item (Join-Path $releaseOutput 'PersonalCloudLibrarySource.dll') $stage
        Copy-Item $extensionPath $stage
        Copy-Item (Join-Path $root 'PersonalCloudLibrarySource\icon.png') $stage
        Copy-Item (Join-Path $root 'PersonalCloudLibrarySource\Localization\en_US.xaml') (Join-Path $stage 'Localization')
        Copy-Item (Join-Path $root 'PersonalCloudLibrarySource\Assets\pcls-logo-wide.png') (Join-Path $stage 'Assets')

        & $toolbox pack $stage $officialOutput
        if ($LASTEXITCODE -ne 0) { throw "Toolbox pack failed with exit code $LASTEXITCODE." }
        & $toolbox verify addon $addonPath
        if ($LASTEXITCODE -ne 0) { throw "Toolbox verify addon failed with exit code $LASTEXITCODE." }
        & $toolbox verify installer $installerPath
        if ($LASTEXITCODE -ne 0) { throw "Toolbox verify installer failed with exit code $LASTEXITCODE." }

        Test-OfficialToolboxOutput -OutputDirectory $officialOutput -ExtensionPath $extensionPath | Out-Null
        if ($PackagePath) { Test-ReleasePackage -PackagePath $PackagePath -ExtensionPath $extensionPath | Out-Null }
        Write-Host "OFFICIAL_VALIDATION_PASSED: Toolbox pack, verify addon, verify installer, YAML, and package inspection passed for $($surface.Version)."
    }
    finally { Remove-Item -LiteralPath $workspace -Recurse -Force -ErrorAction SilentlyContinue }
}
catch {
    [Console]::Error.WriteLine("RELEASE_VALIDATION_FAILED: $($_.Exception.Message)")
    exit 1
}
