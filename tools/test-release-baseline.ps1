param(
    [string]$PackagePath
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repoRoot "PersonalCloudLibrarySource\extension.yaml"
$workflowPath = Join-Path $repoRoot ".github\workflows\build.yml"

function Get-ManifestVersion {
    param([string]$Path)

    $version = Select-String -Path $Path -Pattern '^Version:\s*(.+)$' |
        Select-Object -First 1 |
        ForEach-Object { $_.Matches[0].Groups[1].Value.Trim() }

    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Unable to determine extension version from $Path"
    }

    return $version
}

function Assert-Contains {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Message
    )

    if (-not $Text.Contains($Expected)) {
        throw "$Message Expected to find: $Expected"
    }
}

$version = Get-ManifestVersion -Path $manifestPath
$workflow = Get-Content -LiteralPath $workflowPath -Raw

Assert-Contains $workflow 'PCLS_VERSION=$extensionVersion' 'The workflow must export the manifest version to GITHUB_ENV.'
Assert-Contains $workflow '$env:GITHUB_ENV' 'The workflow must persist the manifest version for later steps.'
Assert-Contains $workflow '$env:PCLS_VERSION.pext' 'Package inspection must use the manifest-derived version.'
Assert-Contains $workflow 'Version:\s*$([regex]::Escape($env:PCLS_VERSION))\s*$' 'Package manifest validation must use the manifest-derived version.'
Assert-Contains $workflow 'PersonalCloudLibrarySource-${{ env.PCLS_VERSION }}-test' 'The artifact name must use the manifest-derived version.'
Assert-Contains $workflow 'dist/PersonalCloudLibrarySource-${{ env.PCLS_VERSION }}.pext' 'The artifact path must use the manifest-derived version.'

$hardCodedPackageIdentity = "PersonalCloudLibrarySource-$version"
if ($workflow.Contains($hardCodedPackageIdentity)) {
    throw "The workflow hard-codes the current package identity: $hardCodedPackageIdentity"
}

if ($PackagePath) {
    $resolvedPackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
    $expectedName = "PersonalCloudLibrarySource-$version.pext"
    if ([System.IO.Path]::GetFileName($resolvedPackagePath) -ne $expectedName) {
        throw "Package filename does not match extension.yaml. Expected: $expectedName"
    }

    $inspectRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("pcls-package-contract-" + [guid]::NewGuid().ToString('N'))
    $zipPath = Join-Path $inspectRoot "$expectedName.zip"
    $expandedPath = Join-Path $inspectRoot "expanded"
    New-Item -ItemType Directory -Path $inspectRoot -Force | Out-Null

    try {
        Copy-Item -LiteralPath $resolvedPackagePath -Destination $zipPath -Force
        Expand-Archive -LiteralPath $zipPath -DestinationPath $expandedPath -Force

        $requiredPaths = @(
            'PersonalCloudLibrarySource.dll',
            'extension.yaml',
            'icon.png',
            'Localization\en_US.xaml',
            'Assets\pcls-logo-wide.png'
        )

        foreach ($relativePath in $requiredPaths) {
            if (-not (Test-Path -LiteralPath (Join-Path $expandedPath $relativePath))) {
                throw "Packaged extension is missing: $relativePath"
            }
        }

        $packagedVersion = Get-ManifestVersion -Path (Join-Path $expandedPath 'extension.yaml')
        if ($packagedVersion -ne $version) {
            throw "Packaged extension version '$packagedVersion' does not match extension.yaml version '$version'."
        }
    }
    finally {
        if (Test-Path -LiteralPath $inspectRoot) {
            Remove-Item -LiteralPath $inspectRoot -Recurse -Force
        }
    }
}

Write-Host "Release baseline contract passed for version $version."
