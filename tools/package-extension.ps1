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
$packageFolder = Join-Path $distRoot "PersonalCloudLibrarySource"
$extensionManifestPath = Join-Path $repoRoot "PersonalCloudLibrarySource\extension.yaml"
$brandDecoder = Join-Path $repoRoot "tools\decode-brand-assets.ps1"

if (-not (Test-Path -LiteralPath $extensionManifestPath)) {
    throw "Extension manifest not found: $extensionManifestPath"
}

$extensionVersion = Select-String -Path $extensionManifestPath -Pattern '^Version:\s*(.+)$' |
    Select-Object -First 1 |
    ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() }

if ([string]::IsNullOrWhiteSpace($extensionVersion)) {
    throw "Unable to determine extension version from $extensionManifestPath"
}

$packagePath = Join-Path $distRoot "PersonalCloudLibrarySource-$extensionVersion.pext"
$debugPackagePath = Join-Path $distRoot "PersonalCloudLibrarySource-$extensionVersion-debug-symbols.zip"

if ([string]::IsNullOrWhiteSpace($msbuild)) {
    throw "MSBuild was not found on PATH or in a supported Visual Studio installation."
}

& $msbuild $solution /t:Build /p:Configuration=$configuration /p:Platform="Any CPU"
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath $brandDecoder)) {
    throw "Brand asset decoder not found: $brandDecoder"
}

& $brandDecoder -OutputDirectory $projectOutput

if (Test-Path -LiteralPath $packageFolder) {
    Remove-Item -LiteralPath $packageFolder -Recurse -Force
}

New-Item -ItemType Directory -Path $packageFolder -Force | Out-Null

$requiredFiles = @(
    "PersonalCloudLibrarySource.dll",
    "extension.yaml",
    "icon.png"
)

foreach ($fileName in $requiredFiles) {
    $sourcePath = Join-Path $projectOutput $fileName
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Required extension file missing: $sourcePath"
    }

    Copy-Item -LiteralPath $sourcePath -Destination $packageFolder -Force
}

$localizationPath = Join-Path $projectOutput "Localization"
if (Test-Path -LiteralPath $localizationPath) {
    Copy-Item -LiteralPath $localizationPath -Destination $packageFolder -Recurse -Force
}

$assetsPath = Join-Path $projectOutput "Assets"
if (-not (Test-Path -LiteralPath $assetsPath)) {
    throw "Generated branding assets are missing: $assetsPath"
}

Copy-Item -LiteralPath $assetsPath -Destination $packageFolder -Recurse -Force

if (Test-Path -LiteralPath $packagePath) {
    Remove-Item -LiteralPath $packagePath -Force
}

$packageZipPath = "$packagePath.zip"
if (Test-Path -LiteralPath $packageZipPath) {
    Remove-Item -LiteralPath $packageZipPath -Force
}

Compress-Archive -Path (Join-Path $packageFolder "*") -DestinationPath $packageZipPath -Force
Move-Item -LiteralPath $packageZipPath -Destination $packagePath -Force

if ($DebugSymbols) {
    $debugFolder = Join-Path $distRoot "PersonalCloudLibrarySource-debug-symbols"
    if (Test-Path -LiteralPath $debugFolder) {
        Remove-Item -LiteralPath $debugFolder -Recurse -Force
    }

    New-Item -ItemType Directory -Path $debugFolder -Force | Out-Null
    $pdbPath = Join-Path $projectOutput "PersonalCloudLibrarySource.pdb"
    if (Test-Path -LiteralPath $pdbPath) {
        Copy-Item -LiteralPath $pdbPath -Destination $debugFolder -Force
    }

    if (Test-Path -LiteralPath $debugPackagePath) {
        Remove-Item -LiteralPath $debugPackagePath -Force
    }

    Compress-Archive -Path (Join-Path $debugFolder "*") -DestinationPath $debugPackagePath -Force
}

Write-Host "Created package: $packagePath"
