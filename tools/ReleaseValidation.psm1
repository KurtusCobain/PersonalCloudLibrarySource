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
    $text = Get-Content -LiteralPath $resolved -Raw
    if ([string]::IsNullOrWhiteSpace($text)) { throw "YAML document is empty: $resolved" }

    $tokens = New-Object System.Collections.ArrayList
    $lineNumber = 0
    foreach ($rawLine in ($text -split "`r?`n")) {
        $lineNumber++
        if ($rawLine -match "`t") { throw "YAML indentation at line $lineNumber contains a tab." }
        if ($rawLine -match '^\s*$' -or $rawLine -match '^\s*#') { continue }
        $indent = $rawLine.Length - $rawLine.TrimStart(' ').Length
        if (($indent % 2) -ne 0) { throw "YAML indentation at line $lineNumber must use two-space levels." }
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

function Test-ReleasePackage {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$PackagePath, [Parameter(Mandatory)][string]$ExtensionPath)
    $extension = Read-DistributionYaml $ExtensionPath
    $version = Get-RequiredYamlScalar $extension 'Version'
    $expectedName = "PersonalCloudLibrarySource-$version.pext"
    if ([IO.Path]::GetFileName($PackagePath) -ne $expectedName) { throw "Package filename must be derived from extension.yaml: $expectedName" }
    $temp = Join-Path ([IO.Path]::GetTempPath()) ('pcls-release-inspect-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    try {
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory((Resolve-Path $PackagePath).Path, $temp)
        $required = @('PersonalCloudLibrarySource.dll','extension.yaml','icon.png','Localization/en_US.xaml','Assets/pcls-logo-wide.png')
        $actual = @(Get-ChildItem -LiteralPath $temp -File -Recurse | ForEach-Object { $_.FullName.Substring($temp.Length).TrimStart('\','/').Replace('\','/') })
        foreach ($path in $required) { if ($actual -notcontains $path) { throw "Package is missing required file: $path" } }
        $extra = @($actual | Where-Object { $required -notcontains $_ })
        if ($extra.Count) { throw "Package contains unexpected file(s): $($extra -join ', ')" }
        $packed = Read-DistributionYaml (Join-Path $temp 'extension.yaml')
        foreach ($key in @('Id','Name','Version','Module','Type','Icon')) {
            if ((Get-RequiredYamlScalar $packed $key) -ne (Get-RequiredYamlScalar $extension $key)) { throw "Packaged extension.yaml $key does not match source." }
        }
        [pscustomobject]@{ Version = $version; PackageName = $expectedName; Files = $actual }
    }
    finally { Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue }
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

    # Toolbox has its own ID/version filename convention. Validate that exact name
    # first, then use the distribution filename only as an adapter for the shared
    # package content and manifest inspection.
    $inspectionRoot = Join-Path ([IO.Path]::GetTempPath()) ('pcls-toolbox-inspect-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $inspectionRoot -Force | Out-Null
    try {
        $inspectionPath = Join-Path $inspectionRoot "PersonalCloudLibrarySource-$version.pext"
        Copy-Item -LiteralPath $packages[0].FullName -Destination $inspectionPath
        Test-ReleasePackage -PackagePath $inspectionPath -ExtensionPath $ExtensionPath
    }
    finally {
        Remove-Item -LiteralPath $inspectionRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Export-ModuleMember -Function Find-PlayniteToolbox, Read-DistributionYaml, Assert-ReleaseSurfaces, Test-ReleasePackage, Test-OfficialToolboxOutput
