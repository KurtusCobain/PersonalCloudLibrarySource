param(
    [switch]$DebugSymbols
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$msbuildCommand = Get-Command "MSBuild.exe" -ErrorAction SilentlyContinue
$msbuildCandidates = @(
    "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
)
$msbuild = if ($msbuildCommand) {
    $msbuildCommand.Source
}
else {
    $msbuildCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
$solution = Join-Path $repoRoot "PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln"
$configuration = "Release"
$projectOutput = Join-Path $repoRoot "PersonalCloudLibrarySource\bin\$configuration"
$distRoot = Join-Path $repoRoot "dist"
$extensionManifestPath = Join-Path $repoRoot "PersonalCloudLibrarySource\extension.yaml"

if (-not (Test-Path -LiteralPath $extensionManifestPath)) {
    throw "Extension manifest not found: $extensionManifestPath"
}

$extensionVersion = Select-String -Path $extensionManifestPath -Pattern '^Version:\s*(.+)$' |
    Select-Object -First 1 |
    ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() }

if ([string]::IsNullOrWhiteSpace($extensionVersion)) {
    throw "Unable to determine extension version from $extensionManifestPath"
}

$parsedVersion = $null
if (-not [version]::TryParse($extensionVersion, [ref]$parsedVersion) -or $extensionVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?
$debugPackagePath = Join-Path $distRoot "PersonalCloudLibrarySource-$extensionVersion-debug-symbols.zip"

if ([string]::IsNullOrWhiteSpace($msbuild)) {
    throw "MSBuild was not found on PATH or in a supported Visual Studio installation."
}

& $msbuild $solution /t:Build /p:Configuration=$configuration /p:Platform="Any CPU"
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed with exit code $LASTEXITCODE"
}

Import-Module (Join-Path $PSScriptRoot 'ReleaseValidation.psm1') -Force
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Assert-OrdinaryFile {
    param([string]$Path)
    $attributes = [IO.File]::GetAttributes($Path)
    if ($attributes -band ([IO.FileAttributes]::Directory -bor [IO.FileAttributes]::ReparsePoint)) {
        throw "Package input or output is not an ordinary file: $Path"
    }
}

function Write-PackageArchive {
    param([string]$Path, [System.Collections.IDictionary]$Files)
    $stream = [IO.FileStream]::new($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($entryName in $Files.Keys) {
                $source = $Files[$entryName]
                Assert-OrdinaryFile $source
                [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive, $source, $entryName, [IO.Compression.CompressionLevel]::Optimal)
            }
        }
        finally { $archive.Dispose() }
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }
}

function Publish-ValidatedPackage {
    param([string]$Staged, [string]$Destination)
    if ([IO.File]::Exists($Destination)) {
        Assert-OrdinaryFile $Destination
        [IO.File]::Replace($Staged, $Destination, $null)
    }
    else { [IO.File]::Move($Staged, $Destination) }
}

# Explicit allowlist matches the strict official validator. No recursive copy
# of build outputs, localization directories or arbitrary assets.
$inputs = [ordered]@{
    'PersonalCloudLibrarySource.dll' = (Join-Path $projectOutput 'PersonalCloudLibrarySource.dll')
    'extension.yaml' = (Join-Path $projectOutput 'extension.yaml')
    'icon.png' = (Join-Path $projectOutput 'icon.png')
    'Localization/en_US.xaml' = (Join-Path $projectOutput 'Localization\en_US.xaml')
    'Assets/pcls-logo-wide.png' = (Join-Path $repoRoot 'PersonalCloudLibrarySource\Assets\pcls-logo-wide.png')
    'LICENSE' = (Join-Path $repoRoot 'LICENSE')
}
foreach ($source in $inputs.Values) { Assert-OrdinaryFile $source }

[void][IO.Directory]::CreateDirectory($distRoot)
$distAttributes = [IO.File]::GetAttributes($distRoot)
if ($distAttributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Distribution directory must not be linked.' }
$stage = Join-Path $distRoot ('.pcls-package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
$stagedPackage = Join-Path $stage ([IO.Path]::GetFileName($packagePath))
try {
    Write-PackageArchive -Path $stagedPackage -Files $inputs
    Test-ReleasePackage -PackagePath $stagedPackage -ExtensionPath $extensionManifestPath | Out-Null

    if ($DebugSymbols) {
        $stagedDebug = Join-Path $stage ([IO.Path]::GetFileName($debugPackagePath))
        $debugInputs = [ordered]@{
            'PersonalCloudLibrarySource.pdb' = (Join-Path $projectOutput 'PersonalCloudLibrarySource.pdb')
        }
        Write-PackageArchive -Path $stagedDebug -Files $debugInputs
        Publish-ValidatedPackage -Staged $stagedDebug -Destination $debugPackagePath
    }

    Publish-ValidatedPackage -Staged $stagedPackage -Destination $packagePath
    # Only remove an empty invocation-owned directory. Preserve any unexpected
    # content and never recursively delete a fixed dist or staging tree.
    [IO.Directory]::Delete($stage, $false)
}
catch {
    Write-Warning "Package creation failed; staging evidence retained at $stage."
    throw
}

Write-Host "Created package: $packagePath"
