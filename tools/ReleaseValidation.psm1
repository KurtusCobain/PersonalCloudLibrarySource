$ErrorActionPreference = 'Stop'

function Remove-YamlQuotes {
    param([string]$Value)
    $value = $Value.Trim()
    if ($value.Length -ge 2 -and (($value[0] -eq "'" -and $value[$value.Length - 1] -eq "'") -or ($value[0] -eq '"' -and $value[$value.Length - 1] -eq '"'))) {
        return $value.Substring(1, $value.Length - 2)
    }
    return $value
}

function Read-DistributionYaml {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    if ((Get-Item -LiteralPath $resolved).Length -gt 65536) { throw 'YAML document exceeds inspection budget.' }
    $text = Get-Content -LiteralPath $resolved -Raw
    Read-DistributionYamlText -Text $text -Path $resolved
}

function Read-DistributionYamlText {
    param([string]$Text, [string]$Path)
    $resolved = $Path
    if ($text.Length -gt 65536) { throw 'YAML document exceeds inspection budget.' }
    if ([string]::IsNullOrWhiteSpace($text)) { throw "YAML document is empty: $resolved" }

    $tokens = New-Object System.Collections.ArrayList
    $lineNumber = 0
    foreach ($rawLine in ($text -split "`r?`n")) {
        $lineNumber++
        if ($rawLine -match "`t") { throw "YAML indentation at line $lineNumber contains a tab." }
        if ($rawLine -match '^\s*$' -or $rawLine -match '^\s*#') { continue }
        $indent = $rawLine.Length - $rawLine.TrimStart(' ').Length
        if (($indent % 2) -ne 0) { throw "YAML indentation at line $lineNumber must use two-space levels." }
        if ($indent -gt 128) { throw "YAML nesting exceeds inspection budget at line $lineNumber." }
        [void]$tokens.Add([pscustomobject]@{ Line = $lineNumber; Indent = $indent; Text = $rawLine.TrimStart(' ') })
    }
    if ($tokens.Count -eq 0) { throw "YAML document is empty: $resolved" }
    if ($tokens[0].Indent -ne 0) { throw "YAML document must begin at indentation zero (line $($tokens[0].Line))." }

    $index = 0
    $rootNode = Parse-YamlNode -Tokens $tokens -Index ([ref]$index) -Indent 0
    if ($index -ne $tokens.Count) {
        $trailing = $tokens[$index]
        throw "Unexpected YAML container or indentation at line $($trailing.Line)."
    }
    if ($rootNode.Type -ne 'Map') { throw 'YAML document root must be a mapping.' }

    $topLevel = @{}
    $sequenceItems = @{}
    foreach ($key in $rootNode.Data.Keys) {
        $node = $rootNode.Data[$key]
        if ($node.Type -eq 'Scalar') { $topLevel[$key] = [string]$node.Data }
        elseif ($node.Type -eq 'Sequence') { $topLevel[$key] = $null; $sequenceItems[$key] = @(Convert-YamlNodeValue $node) }
        else { $topLevel[$key] = $null }
    }
    [pscustomobject]@{ Path = $resolved; Scalars = $topLevel; Sequences = $sequenceItems; Root = $rootNode; Text = $text }
}

function Parse-YamlNode {
    param([System.Collections.ArrayList]$Tokens, [ref]$Index, [int]$Indent)
    if ($Index.Value -ge $Tokens.Count -or $Tokens[$Index.Value].Indent -ne $Indent) {
        throw "Expected YAML content at indentation $Indent."
    }
    if ($Tokens[$Index.Value].Text.StartsWith('- ')) {
        return Parse-YamlSequence -Tokens $Tokens -Index $Index -Indent $Indent
    }
    return Parse-YamlMapping -Tokens $Tokens -Index $Index -Indent $Indent
}

function Parse-YamlMapping {
    param([System.Collections.ArrayList]$Tokens, [ref]$Index, [int]$Indent)
    $map = @{}
    while ($Index.Value -lt $Tokens.Count) {
        $token = $Tokens[$Index.Value]
        if ($token.Indent -lt $Indent) { break }
        if ($token.Indent -gt $Indent) { throw "Unexpected YAML indentation at line $($token.Line)." }
        if ($token.Text.StartsWith('- ')) { throw "YAML sequence item is invalid in a mapping container at line $($token.Line)." }
        Add-YamlMappingEntry -Tokens $Tokens -Index $Index -Indent $Indent -Map $map -Text $token.Text -Line $token.Line
    }
    return @{ Type = 'Map'; Data = $map }
}

function Add-YamlMappingEntry {
    param([System.Collections.ArrayList]$Tokens, [ref]$Index, [int]$Indent, [hashtable]$Map, [string]$Text, [int]$Line, [switch]$AllowContinuation)
    $colon = $Text.IndexOf(':')
    if ($colon -le 0) { throw "YAML mapping at line $Line must contain a key and colon." }
    $key = $Text.Substring(0, $colon).Trim()
    if (-not $key) { throw "YAML mapping key is empty at line $Line." }
    if ($Map.ContainsKey($key)) { throw "Duplicate YAML key '$key' at line $Line." }
    $remainder = $Text.Substring($colon + 1).Trim()
    $Index.Value++

    if ($remainder -eq '|') {
        $start = $Index.Value
        if ($start -ge $Tokens.Count -or $Tokens[$start].Indent -ne ($Indent + 2)) {
            throw "YAML block scalar '$key' must contain content indented by two spaces (line $Line)."
        }
        while ($Index.Value -lt $Tokens.Count -and $Tokens[$Index.Value].Indent -gt $Indent) { $Index.Value++ }
        $Map[$key] = @{ Type = 'Scalar'; Data = '<block>' }
        return
    }
    if ($remainder) {
        if (-not $AllowContinuation -and $Index.Value -lt $Tokens.Count -and $Tokens[$Index.Value].Indent -gt $Indent) {
            throw "YAML scalar '$key' cannot own nested content at line $($Tokens[$Index.Value].Line)."
        }
        $Map[$key] = @{ Type = 'Scalar'; Data = (Remove-YamlQuotes $remainder) }
        return
    }
    if ($Index.Value -ge $Tokens.Count -or $Tokens[$Index.Value].Indent -ne ($Indent + 2)) {
        throw "YAML mapping '$key' requires nested content indented by two spaces (line $Line)."
    }
    $Map[$key] = Parse-YamlNode -Tokens $Tokens -Index $Index -Indent ($Indent + 2)
}

function Parse-YamlSequence {
    param([System.Collections.ArrayList]$Tokens, [ref]$Index, [int]$Indent)
    $items = New-Object System.Collections.ArrayList
    while ($Index.Value -lt $Tokens.Count) {
        $token = $Tokens[$Index.Value]
        if ($token.Indent -lt $Indent) { break }
        if ($token.Indent -gt $Indent) { throw "Unexpected YAML indentation in sequence at line $($token.Line)." }
        if (-not $token.Text.StartsWith('- ')) { throw "YAML mapping is invalid in a sequence container at line $($token.Line)." }
        $content = $token.Text.Substring(2).Trim()
        if (-not $content) { throw "YAML sequence item has no value at line $($token.Line)." }
        $colon = $content.IndexOf(':')
        if ($colon -gt 0) {
            $map = @{}
            Add-YamlMappingEntry -Tokens $Tokens -Index $Index -Indent $Indent -Map $map -Text $content -Line $token.Line -AllowContinuation
            if ($Index.Value -lt $Tokens.Count -and $Tokens[$Index.Value].Indent -eq ($Indent + 2)) {
                $continuation = Parse-YamlMapping -Tokens $Tokens -Index $Index -Indent ($Indent + 2)
                foreach ($key in $continuation.Data.Keys) {
                    if ($map.ContainsKey($key)) { throw "Duplicate YAML key '$key' in sequence mapping at line $($token.Line)." }
                    $map[$key] = $continuation.Data[$key]
                }
            }
            [void]$items.Add(@{ Type = 'Map'; Data = $map })
        }
        else {
            $Index.Value++
            if ($Index.Value -lt $Tokens.Count -and $Tokens[$Index.Value].Indent -gt $Indent) {
                throw "YAML scalar sequence item cannot own nested content at line $($Tokens[$Index.Value].Line)."
            }
            [void]$items.Add(@{ Type = 'Scalar'; Data = (Remove-YamlQuotes $content) })
        }
    }
    return @{ Type = 'Sequence'; Data = $items }
}

function Convert-YamlNodeValue {
    param([hashtable]$Node)
    if ($Node.Type -eq 'Scalar') { return $Node.Data }
    if ($Node.Type -eq 'Map') {
        $value = @{}
        foreach ($key in $Node.Data.Keys) { $value[$key] = Convert-YamlNodeValue $Node.Data[$key] }
        return $value
    }
    $values = @()
    foreach ($item in $Node.Data) { $values += ,(Convert-YamlNodeValue $item) }
    return $values
}

function Get-RequiredYamlScalar {
    param($Document, [string]$Key)
    if (-not $Document.Scalars.ContainsKey($Key) -or [string]::IsNullOrWhiteSpace([string]$Document.Scalars[$Key])) {
        throw "$($Document.Path) is missing required scalar '$Key'."
    }
    [string]$Document.Scalars[$Key]
}

function Find-PlayniteToolbox {
    [CmdletBinding()]
    param(
        [string]$ExplicitPath,
        [string]$EnvironmentPath = $env:PLAYNITE_TOOLBOX,
        [string[]]$KnownLocations
    )
    if ($null -eq $KnownLocations) {
        $KnownLocations = @(
            (Join-Path $env:LOCALAPPDATA 'Playnite\Toolbox.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Playnite\Toolbox.exe'),
            (Join-Path $env:ProgramFiles 'Playnite\Toolbox.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Playnite\Toolbox.exe')
        )
    }
    foreach ($candidate in @($ExplicitPath, $EnvironmentPath) + $KnownLocations) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    return $null
}

function Assert-ReleaseSurfaces {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ExtensionPath,
        [Parameter(Mandatory)][string]$AddonPath,
        [Parameter(Mandatory)][string]$InstallerPath
    )
    $extension = Read-DistributionYaml $ExtensionPath
    $addon = Read-DistributionYaml $AddonPath
    $installer = Read-DistributionYaml $InstallerPath
    $id = Get-RequiredYamlScalar $extension 'Id'
    $name = Get-RequiredYamlScalar $extension 'Name'
    $version = Get-RequiredYamlScalar $extension 'Version'
    if ((Get-RequiredYamlScalar $addon 'AddonId') -ne $id -or (Get-RequiredYamlScalar $installer 'AddonId') -ne $id) {
        throw 'AddonId values do not match extension.yaml Id.'
    }
    if ((Get-RequiredYamlScalar $addon 'Name') -ne $name) { throw 'Add-on Name does not match extension.yaml Name.' }
    $parsedVersion = $null
    if (-not [version]::TryParse($version, [ref]$parsedVersion)) { throw "Extension Version '$version' is not a valid .NET version." }
    if (-not $installer.Sequences.ContainsKey('Packages') -or $installer.Sequences['Packages'].Count -eq 0) { throw 'Installer Packages sequence is empty.' }
    foreach ($package in $installer.Sequences['Packages']) {
        if ($package -isnot [hashtable]) { throw 'Installer Packages must contain mappings.' }
        $packageVersion = [string]$package['Version']
        $packageUrl = [string]$package['PackageUrl']
        $parsedPackageVersion = $null
        if ([string]::IsNullOrWhiteSpace($packageVersion) -or -not [version]::TryParse($packageVersion, [ref]$parsedPackageVersion)) {
            throw "Installer package Version '$packageVersion' is not a valid .NET version."
        }
        $uri = $null
        if ([string]::IsNullOrWhiteSpace($packageUrl) -or -not [uri]::TryCreate($packageUrl, [UriKind]::Absolute, [ref]$uri)) {
            throw "Installer package $packageVersion has an invalid PackageUrl."
        }
        $expectedLeaf = "PersonalCloudLibrarySource-$packageVersion.pext"
        $actualLeaf = [IO.Path]::GetFileName($uri.AbsolutePath)
        if ($actualLeaf -cne $expectedLeaf) {
            throw "Installer package Version $packageVersion requires PackageUrl leaf '$expectedLeaf', found '$actualLeaf'."
        }
    }
    [pscustomobject]@{ Id = $id; Name = $name; Version = $version; PackageName = "PersonalCloudLibrarySource-$version.pext" }
}

# An internal candidate must exceed every published installer version.
# Treat a missing fourth component as zero for comparison.
function Assert-UnpublishedCandidate {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ExtensionPath,
        [Parameter(Mandatory)][string]$AddonPath,
        [Parameter(Mandatory)][string]$InstallerPath,
        [string]$PackagePath
    )
    $surface = Assert-ReleaseSurfaces -ExtensionPath $ExtensionPath -AddonPath $AddonPath -InstallerPath $InstallerPath
    $installer = Read-DistributionYaml $InstallerPath
    $version = [version]$surface.Version
    if ($version.Revision -lt 1) {
        throw 'Internal candidate must use a positive fourth version component.'
    }
    foreach ($entry in $installer.Sequences['Packages']) {
        $public = [version]([string]$entry['Version'])
        $normalized = [version]::new($public.Major, $public.Minor,
            [Math]::Max(0, $public.Build), [Math]::Max(0, $public.Revision))
        if ($version -le $normalized) {
            throw "Internal candidate version $version must exceed published installer version $normalized."
        }
    }
    if ($PackagePath) {
        Test-ReleasePackage -PackagePath $PackagePath -ExtensionPath $ExtensionPath | Out-Null
        # Use the same framework hashing primitive as the Windows
        # PowerShell regression fixtures; cmdlet/module availability varies.
        $stream = [IO.File]::OpenRead((Resolve-Path -LiteralPath $PackagePath).Path)
        try {
            $sha256 = [Security.Cryptography.SHA256]::Create()
            try { $digest = ([BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '') }
            finally { $sha256.Dispose() }
        }
        finally { $stream.Dispose() }
        if ($digest -eq '1781fe630fad9398b8e810e3cf843309acc68a8a3dcfba720873042da41b2ed7') {
            throw 'Candidate digest matches the published v0.3.2 package.'
        }
        Write-Host "Internal candidate SHA-256: $digest"
    }
    Write-Host "Internal candidate $version is distinct from published installer versions."
}

# Inspect a bounded ZIP directory without expanding untrusted paths. Keep the
# baseline and official Toolbox validators on the same archive safety contract.
function Open-PclsPackageArchive {
    param([Parameter(Mandatory)][string]$Path)
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $item = Get-Item -LiteralPath $resolved
    if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Package must be an ordinary file.'
    }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stream = [IO.FileStream]::new($resolved, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($stream.Length -gt 67108864) { throw 'Package exceeds inspection byte budget.' }
        # Archive owns the same length-checked stream; no second path open.
        return [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read, $false)
    }
    catch { $stream.Dispose(); throw }
}

function Get-PclsPackageIndex {
    param([Parameter(Mandatory)]$Archive)
    $files = [System.Collections.Generic.Dictionary[string,System.IO.Compression.ZipArchiveEntry]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $totalBytes = [long]0
    if ($Archive.Entries.Count -gt 4096) { throw 'Package entry count exceeds inspection budget.' }
    foreach ($entry in $Archive.Entries) {
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
        if ($entry.Length -lt 0 -or $entry.Length -gt 67108864 -or
            $totalBytes -gt (134217728 - $entry.Length)) {
            throw 'Package expanded byte count exceeds inspection budget.'
        }
        $totalBytes += $entry.Length
        if ($directory -and $entry.Length -ne 0) { throw "Directory package entry contains data: $name" }
        if ($entry.PSObject.Properties['ExternalAttributes']) {
            $attributes = $entry.ExternalAttributes
            $unixType = ($attributes -shr 16) -band 0xF000
            if (($attributes -band 0x400) -ne 0 -or
                ($unixType -ne 0 -and $unixType -ne 0x8000 -and $unixType -ne 0x4000) -or
                ($unixType -eq 0x4000 -and -not $directory) -or
                ($unixType -eq 0x8000 -and $directory)) {
                throw "Linked or special package entry: $name"
            }
        }
        if (-not $directory) { $files.Add($name, $entry) }
    }
    foreach ($name in $seen) {
        $slash = $name.LastIndexOf('/')
        while ($slash -gt 0) {
            $parent = $name.Substring(0, $slash)
            if ($files.ContainsKey($parent)) { throw "Package file/parent conflict: $name" }
            $slash = $name.LastIndexOf('/', $slash - 1)
        }
    }
    return ,$files
}

function Read-PclsPackageMetadata {
    param([Parameter(Mandatory)]$Entry)
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
            return ([System.Text.UTF8Encoding]::new($false, $true)).GetString($data.ToArray()).TrimStart([char]0xFEFF)
        }
        finally { $data.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Test-ReleasePackage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$PackagePath, [Parameter(Mandatory)][string]$ExtensionPath)
    $extension = Read-DistributionYaml $ExtensionPath
    $version = Get-RequiredYamlScalar $extension 'Version'
    $expectedName = "PersonalCloudLibrarySource-$version.pext"
    if ([IO.Path]::GetFileName($PackagePath) -ne $expectedName) { throw "Package filename must be derived from extension.yaml: $expectedName" }
    Test-PclsPackageContent -PackagePath $PackagePath -Extension $extension -Version $version -ExpectedName $expectedName
}

function Test-PclsPackageContent {
    param([string]$PackagePath, $Extension, [string]$Version, [string]$ExpectedName)
    $archive = Open-PclsPackageArchive -Path $PackagePath
    try {
        $required = @('PersonalCloudLibrarySource.dll','extension.yaml','LICENSE','icon.png','Localization/en_US.xaml','Assets/pcls-logo-wide.png')
        $files = Get-PclsPackageIndex -Archive $archive
        $actual = @($files.Keys)
        foreach ($path in $required) { if (-not $files.ContainsKey($path)) { throw "Package is missing required file: $path" } }
        $extra = @($actual | Where-Object { $required -notcontains $_ })
        if ($extra.Count) { throw "Package contains unexpected file(s): $($extra -join ', ')" }
        # Validate actual decompressed lengths of every required payload,
        # not only the two metadata entries. No files are extracted.
        $buffer = [byte[]]::new(65536)
        foreach ($path in $required) {
            $entry = $files[$path]
            if ($entry.Length -le 0) { throw "Package required file is empty: $path" }
            $stream = $entry.Open()
            try {
                $remaining = [long]$entry.Length
                while ($remaining -gt 0) {
                    $read = $stream.Read($buffer, 0, [int][Math]::Min([long]$buffer.Length, $remaining))
                    if ($read -le 0) { throw "Package payload is truncated: $path" }
                    $remaining -= $read
                }
                if ($stream.ReadByte() -ne -1) { throw "Package payload exceeds declared size: $path" }
            }
            finally { $stream.Dispose() }
        }
        $packed = Read-DistributionYamlText -Text (Read-PclsPackageMetadata $files['extension.yaml']) -Path 'package:extension.yaml'
        $license = Read-PclsPackageMetadata $files['LICENSE']
        if (-not $license.Contains('MIT License') -or -not $license.Contains('The Bearclaw & ztwslayer')) {
            throw 'Package is missing required MIT attribution.'
        }
        foreach ($key in @('Id','Name','Version','Module','Type','Icon')) {
            if ((Get-RequiredYamlScalar $packed $key) -ne (Get-RequiredYamlScalar $extension $key)) { throw "Packaged extension.yaml $key does not match source." }
        }
        [pscustomobject]@{ Version = $version; PackageName = $expectedName; Files = $actual }
    }
    finally { $archive.Dispose() }
}

function Test-OfficialToolboxOutput {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$OutputDirectory, [Parameter(Mandatory)][string]$ExtensionPath)
    $extension = Read-DistributionYaml $ExtensionPath
    $id = Get-RequiredYamlScalar $extension 'Id'
    $version = Get-RequiredYamlScalar $extension 'Version'
    $expectedName = "${id}_$($version.Replace('.', '_')).pext"
    $packages = @(Get-ChildItem -LiteralPath $OutputDirectory -Filter '*.pext' -File)
    if ($packages.Count -ne 1) { throw "Toolbox output must contain exactly one .pext; found $($packages.Count)." }
    if ($packages[0].Name -cne $expectedName) {
        throw "Official Toolbox output filename must be '$expectedName', found '$($packages[0].Name)'."
    }

    # Inspect the actual official artifact without creating a temporary copy.
    Test-PclsPackageContent -PackagePath $packages[0].FullName -Extension $extension -Version $version -ExpectedName "PersonalCloudLibrarySource-$version.pext"
}

Export-ModuleMember -Function Find-PlayniteToolbox, Read-DistributionYaml, Assert-ReleaseSurfaces, Assert-UnpublishedCandidate, Test-ReleasePackage, Test-OfficialToolboxOutput, Open-PclsPackageArchive, Get-PclsPackageIndex, Read-PclsPackageMetadata
