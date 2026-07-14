param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$modulePath = Join-Path $PSScriptRoot 'ReleaseValidation.psm1'
Import-Module $modulePath -Force

$script:passed = 0
$script:failed = 0

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][string]$Path)
    $stream = [IO.File]::OpenRead((Resolve-Path -LiteralPath $Path).Path)
    try {
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            return ([BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '')
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Assert-ThrowsLike {
    param([scriptblock]$Action, [string]$Pattern)
    $caught = $null
    try {
        & $Action
    }
    catch {
        $caught = $_
    }
    if ($null -eq $caught) { throw "Expected an exception matching '$Pattern', but no exception was thrown." }
    if ($caught.Exception.Message -notmatch $Pattern) { throw $caught }
}

function Test-Case {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        $script:passed++
        Write-Host "PASS $Name"
    }
    catch {
        $script:failed++
        Write-Host "FAIL $Name`: $($_.Exception.Message)"
    }
}

function New-TestRepository {
    param([string]$Root)
    New-Item -ItemType Directory -Path (Join-Path $Root 'PersonalCloudLibrarySource') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $Root 'playnite-addon') -Force | Out-Null
    @'
Id: PersonalCloudLibrarySource_61993828-67a8-4468-93a2-293442e36328
Name: Personal Cloud Library Source
Author: Test
Version: 0.3.2
Module: PersonalCloudLibrarySource.dll
Type: GameLibrary
Icon: icon.png
'@ | Set-Content -LiteralPath (Join-Path $Root 'PersonalCloudLibrarySource\extension.yaml') -Encoding UTF8
    @'
AddonId: PersonalCloudLibrarySource_61993828-67a8-4468-93a2-293442e36328
Type: GameLibrary
Name: Personal Cloud Library Source
Author: Test
ShortDescription: Test
InstallerManifestUrl: https://example.invalid/installer.yaml
SourceUrl: https://example.invalid
Tags:
  - library
'@ | Set-Content -LiteralPath (Join-Path $Root 'playnite-addon\addon-database.yaml') -Encoding UTF8
    @'
AddonId: PersonalCloudLibrarySource_61993828-67a8-4468-93a2-293442e36328
Packages:
  - Version: 0.2.0
    RequiredApiVersion: 6.16.0
    ReleaseDate: 2026-06-15
    PackageUrl: https://example.invalid/PersonalCloudLibrarySource-0.2.0.pext
    Changelog:
      - Historical package
'@ | Set-Content -LiteralPath (Join-Path $Root 'playnite-addon\installer.yaml') -Encoding UTF8
}

function New-TestPackage {
    param([string]$Root, [string]$PackagePath, [switch]$MissingIcon, [switch]$ExtraSource)
    $stage = Join-Path $Root ('stage-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path (Join-Path $stage 'Localization') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $stage 'Assets') -Force | Out-Null
    Copy-Item (Join-Path $Root 'PersonalCloudLibrarySource\extension.yaml') (Join-Path $stage 'extension.yaml')
    Set-Content (Join-Path $stage 'PersonalCloudLibrarySource.dll') 'test'
    if (-not $MissingIcon) { Set-Content (Join-Path $stage 'icon.png') 'test' }
    Set-Content (Join-Path $stage 'Localization\en_US.xaml') 'test'
    Set-Content (Join-Path $stage 'Assets\pcls-logo-wide.png') 'test'
    if ($ExtraSource) { Set-Content (Join-Path $stage 'Leaked.cs') 'test' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $PackagePath)
    Remove-Item -LiteralPath $stage -Recurse -Force
}

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('pcls-release-validator-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

try {
    Test-Case 'Toolbox discovery honors explicit, environment, and known-location precedence' {
        $explicit = Join-Path $testRoot 'explicit\Toolbox.exe'
        $environment = Join-Path $testRoot 'environment\Toolbox.exe'
        $known = Join-Path $testRoot 'known\Toolbox.exe'
        foreach ($path in @($explicit, $environment, $known)) {
            New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null
            Set-Content -LiteralPath $path -Value 'fixture'
        }
        Assert-True ((Find-PlayniteToolbox -ExplicitPath $explicit -EnvironmentPath $environment -KnownLocations @($known)) -eq $explicit) 'Explicit Toolbox path did not win.'
        Assert-True ((Find-PlayniteToolbox -EnvironmentPath $environment -KnownLocations @($known)) -eq $environment) 'Environment Toolbox path did not win.'
        Assert-True ((Find-PlayniteToolbox -KnownLocations @($known)) -eq $known) 'Known Toolbox location was not found.'
        Assert-True ((Find-PlayniteToolbox -KnownLocations @((Join-Path $testRoot 'missing.exe'))) -eq $null) 'Missing Toolbox must not be reported as found.'
    }

    Test-Case 'Structural YAML parser rejects malformed mappings and sequences' {
        $badIndent = Join-Path $testRoot 'bad-indent.yaml'
        $badSequence = Join-Path $testRoot 'bad-sequence.yaml'
        $outdentedProperty = Join-Path $testRoot 'outdented-property.yaml'
        $nestedDuplicate = Join-Path $testRoot 'nested-duplicate.yaml'
        $badBlock = Join-Path $testRoot 'bad-block.yaml'
        $scalarWithChild = Join-Path $testRoot 'scalar-child.yaml'
        Set-Content $badIndent "Root:`n   Child: value"
        Set-Content $badSequence "Items:`n  value"
        Set-Content $outdentedProperty "Screenshots:`n  - Thumbnail: one.png`n  Links:`n    GitHub: https://example.invalid"
        Set-Content $nestedDuplicate "Links:`n  GitHub: one`n  GitHub: two"
        Set-Content $badBlock "Description: |`nnot-indented"
        Set-Content $scalarWithChild "Name: value`n  Child: forbidden"
        Assert-ThrowsLike { Read-DistributionYaml -Path $badIndent } 'indent|structure|mapping'
        Assert-ThrowsLike { Read-DistributionYaml -Path $badSequence } 'sequence|mapping|colon'
        Assert-ThrowsLike { Read-DistributionYaml -Path $outdentedProperty } 'sequence|indent|container|mapping'
        Assert-ThrowsLike { Read-DistributionYaml -Path $nestedDuplicate } 'Duplicate.*GitHub'
        Assert-ThrowsLike { Read-DistributionYaml -Path $badBlock } 'block.*indent|block.*content'
        Assert-ThrowsLike { Read-DistributionYaml -Path $scalarWithChild } 'scalar.*nested|indent'
    }

    Test-Case 'Release surfaces reject identity version and name mismatches' {
        $root = Join-Path $testRoot 'mismatch'
        New-TestRepository $root
        (Get-Content (Join-Path $root 'playnite-addon\addon-database.yaml') -Raw).Replace('Name: Personal Cloud Library Source', 'Name: Wrong Name') |
            Set-Content (Join-Path $root 'playnite-addon\addon-database.yaml')
        Assert-ThrowsLike {
            Assert-ReleaseSurfaces -ExtensionPath (Join-Path $root 'PersonalCloudLibrarySource\extension.yaml') -AddonPath (Join-Path $root 'playnite-addon\addon-database.yaml') -InstallerPath (Join-Path $root 'playnite-addon\installer.yaml')
        } 'Name.*match'

        New-TestRepository $root
        (Get-Content (Join-Path $root 'playnite-addon\installer.yaml') -Raw).Replace('0.2.0', '0.3.2') |
            Set-Content (Join-Path $root 'playnite-addon\installer.yaml')
        (Get-Content (Join-Path $root 'playnite-addon\installer.yaml') -Raw).Replace('PersonalCloudLibrarySource-0.3.2.pext', 'PersonalCloudLibrarySource-9.9.9.pext') |
            Set-Content (Join-Path $root 'playnite-addon\installer.yaml')
        Assert-ThrowsLike {
            Assert-ReleaseSurfaces -ExtensionPath (Join-Path $root 'PersonalCloudLibrarySource\extension.yaml') -AddonPath (Join-Path $root 'playnite-addon\addon-database.yaml') -InstallerPath (Join-Path $root 'playnite-addon\installer.yaml')
        } 'package.*version|version.*package'

        New-TestRepository $root
        (Get-Content (Join-Path $root 'playnite-addon\installer.yaml') -Raw).Replace('PersonalCloudLibrarySource-0.2.0.pext', 'PersonalCloudLibrarySource-9.9.9.pext') |
            Set-Content (Join-Path $root 'playnite-addon\installer.yaml')
        Assert-ThrowsLike {
            Assert-ReleaseSurfaces -ExtensionPath (Join-Path $root 'PersonalCloudLibrarySource\extension.yaml') -AddonPath (Join-Path $root 'playnite-addon\addon-database.yaml') -InstallerPath (Join-Path $root 'playnite-addon\installer.yaml')
        } '0.2.0|package.*version|version.*package'
    }

    Test-Case 'Package inspection derives name and rejects missing or extra files' {
        $root = Join-Path $testRoot 'package'
        New-TestRepository $root
        $valid = Join-Path $root 'PersonalCloudLibrarySource-0.3.2.pext'
        New-TestPackage $root $valid
        $result = Test-ReleasePackage -PackagePath $valid -ExtensionPath (Join-Path $root 'PersonalCloudLibrarySource\extension.yaml')
        Assert-True ($result.Version -eq '0.3.2') 'Package version was not derived from extension.yaml.'
        Assert-True ($result.PackageName -eq 'PersonalCloudLibrarySource-0.3.2.pext') 'Package name was not derived from extension.yaml.'

        $missing = Join-Path $root 'missing\PersonalCloudLibrarySource-0.3.2.pext'
        New-Item (Split-Path $missing) -ItemType Directory -Force | Out-Null
        New-TestPackage $root $missing -MissingIcon
        Assert-ThrowsLike { Test-ReleasePackage -PackagePath $missing -ExtensionPath (Join-Path $root 'PersonalCloudLibrarySource\extension.yaml') } 'missing.*icon.png'

        $extra = Join-Path $root 'extra\PersonalCloudLibrarySource-0.3.2.pext'
        New-Item (Split-Path $extra) -ItemType Directory -Force | Out-Null
        New-TestPackage $root $extra -ExtraSource
        Assert-ThrowsLike { Test-ReleasePackage -PackagePath $extra -ExtensionPath (Join-Path $root 'PersonalCloudLibrarySource\extension.yaml') } 'unexpected.*Leaked.cs'
    }

    Test-Case 'Official Toolbox output must already use the manifest-derived filename' {
        $root = Join-Path $testRoot 'official-name'
        New-TestRepository $root
        $output = Join-Path $root 'official'
        New-Item $output -ItemType Directory -Force | Out-Null
        New-TestPackage $root (Join-Path $output 'WrongName.pext')
        Assert-ThrowsLike {
            Test-OfficialToolboxOutput -OutputDirectory $output -ExtensionPath (Join-Path $root 'PersonalCloudLibrarySource\extension.yaml')
        } 'filename|PersonalCloudLibrarySource-0.3.2.pext'
    }

    Test-Case 'Missing Toolbox is an explicit non-success prerequisite and does not mutate release inputs' {
        $root = Join-Path $testRoot 'missing-toolbox'
        New-TestRepository $root
        $paths = @('PersonalCloudLibrarySource\extension.yaml', 'playnite-addon\addon-database.yaml', 'playnite-addon\installer.yaml')
        $before = @{}; foreach ($relative in $paths) { $before[$relative] = Get-Sha256Hex (Join-Path $root $relative) }
        $validator = Join-Path $PSScriptRoot 'validate-release.ps1'
        $oldPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        $output = & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $validator -RepositoryRoot $root -ToolboxPath (Join-Path $root 'absent\Toolbox.exe') 2>&1
        $exit = $LASTEXITCODE
        $ErrorActionPreference = $oldPreference
        Assert-True ($exit -ne 0) 'Missing Toolbox must fail the release gate.'
        Assert-True (($output -join "`n") -match 'PREREQUISITE_MISSING.*Toolbox.exe') 'Missing Toolbox result was not explicit.'
        foreach ($relative in $paths) {
            Assert-True ((Get-Sha256Hex (Join-Path $root $relative)) -eq $before[$relative]) "Validator mutated $relative."
        }
    }
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Release validator tests: $script:passed passed, $script:failed failed."
if ($script:failed -ne 0) { exit 1 }
