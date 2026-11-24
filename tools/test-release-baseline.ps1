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

    # Inspect the ZIP directory and small metadata streams directly. This works
    # in Windows PowerShell 5.1 and PowerShell 7 and never extracts untrusted paths.
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackagePath)
    try {
        $files = [System.Collections.Generic.Dictionary[string,System.IO.Compression.ZipArchiveEntry]]::new([System.StringComparer]::OrdinalIgnoreCase)
        $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
        if ($archive.Entries.Count -gt 4096) { throw 'Package entry count exceeds inspection budget.' }

        foreach ($entry in $archive.Entries) {
            $name = $entry.FullName.Replace([char]0x5c, [char]0x2f)
            $directory = $name.EndsWith('/')
            $name = $name.TrimEnd([char]0x2f)
            $segments = $name.Split('/')
            if ([string]::IsNullOrWhiteSpace($name) -or $segments.Count -gt 64 -or
                ($segments | Where-Object {
                    [string]::IsNullOrWhiteSpace($_) -or $_ -eq '.' -or $_ -eq '..' -or
                    $_ -match '[<>:"|?*\x00-\x1f]' -or $_.EndsWith('.') -or $_.EndsWith(' ') -or
                    $_ -match '^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)'
                })) {
                throw "Unsafe package entry path: $name"
            }
            if (-not $seen.Add($name)) { throw "Duplicate package entry: $name" }
            if (-not $directory) { $files.Add($name, $entry) }
        }

        $requiredPaths = @(
            'PersonalCloudLibrarySource.dll',
            'extension.yaml',
            'LICENSE',
            'icon.png',
            'Localization/en_US.xaml',
            'Assets/pcls-logo-wide.png'
        )
        foreach ($relativePath in $requiredPaths) {
            if (-not $files.ContainsKey($relativePath)) {
                throw "Packaged extension is missing: $relativePath"
            }
        }

        $forbiddenSegmentPattern = '^(?:\.superpowers|bin|obj|packages|dist|tests?|scratch|temp|tmp|TestResults)$'
        $forbiddenLeafPattern = '(?i)(?:\.cs|\.csproj|\.sln|\.ps1|\.tmp|\.bak|\.pdb|\.g\.cs|\.g\.i\.cs|\.generated\.cs)$'
        $forbiddenFiles = $files.Keys | Where-Object {
            $segments = $_.Split('/')
            $leaf = $segments[-1]
            ($segments | Where-Object { $_ -match $forbiddenSegmentPattern } | Select-Object -First 1) -or
                $leaf -match $forbiddenLeafPattern -or
                $leaf -match '^TemporaryGeneratedFile_'
        }
        if ($forbiddenFiles) {
            throw "Packaged extension contains forbidden file(s): $($forbiddenFiles -join ', ')"
        }

        function Read-PackageMetadata {
            param([System.IO.Compression.ZipArchiveEntry]$Entry)
            $limit = 65536
            if ($Entry.Length -gt $limit) { throw 'Package metadata exceeds inspection budget.' }
            $stream = $Entry.Open()
            try {
                $buffer = New-Object byte[] 4096
                $data = New-Object System.IO.MemoryStream
                try {
                    while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                        if ($data.Length + $read -gt $limit) { throw 'Package metadata exceeds inspection budget.' }
                        $data.Write($buffer, 0, $read)
                    }
                    if ($data.Length -ne $Entry.Length) { throw 'Package metadata size mismatch.' }
                    return [System.Text.Encoding]::UTF8.GetString($data.ToArray()).TrimStart([char]0xFEFF)
                }
                finally { $data.Dispose() }
            }
            finally { $stream.Dispose() }
        }

        $licenseNotice = Read-PackageMetadata $files['LICENSE']
        Assert-Contains $licenseNotice 'MIT License' 'Package must retain the MIT license.'
        Assert-Contains $licenseNotice 'The Bearclaw & ztwslayer' 'Package must retain the contributor copyright attribution.'

        $manifestText = Read-PackageMetadata $files['extension.yaml']
        $match = [regex]::Match($manifestText, '(?m)^Version:\s*(.+)$')
        $packagedVersion = $match.Groups[1].Value.Trim()
        if (-not $match.Success -or $packagedVersion -ne $version) {
            throw "Packaged extension version '$packagedVersion' does not match extension.yaml version '$version'."
        }
    }
    finally { $archive.Dispose() }
}

Write-Host "Release baseline contract passed for version $version."
