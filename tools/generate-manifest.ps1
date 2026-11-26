<#
.SYNOPSIS
Generates a Personal Cloud Library Source manifest from a filesystem root or rclone remote.

.DESCRIPTION
This script catalogs user-owned files that already exist in a local folder, external drive,
mapped drive, NAS path, or rclone remote and writes a manifest for the Personal Cloud Library
Source Playnite plugin.

It does not provide, download, scrape, or include games, ROMs, BIOS files, keys, cracks,
or copyrighted content.
#>

[CmdletBinding()]
param(
    [string]$SourceRoot,
    [string]$RcloneRemoteRoot,
    [string]$OutputPath = ".\personal-cloud-library.generated.json",
    [string]$ReportPath = "",
    [string]$RclonePath = "rclone",
    [ValidateRange(5, 3600)][int]$TimeoutSeconds = 300,
    [string[]]$IncludeExtensions = @(),
    [string[]]$ExcludeFolders = @(),
    [switch]$Recurse,
    [switch]$Overwrite,
    [switch]$DryRun,
    [switch]$FastList,
    [switch]$IncludeNonLaunchablePackages,
    [switch]$NoReport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$DefaultSingleFileExtensions = @(
    ".nes", ".sfc", ".smc", ".n64", ".z64", ".v64",
    ".gb", ".gbc", ".gba",
    ".gg", ".gen", ".md", ".sms", ".32x",
    ".rvz", ".gcz", ".iso", ".chd", ".cso", ".pbp",
    ".zip", ".7z",
    ".xci", ".nsp", ".3ds", ".cia",
    ".exe", ".bat", ".cmd", ".lnk"
)

$DefaultDiscLaunchExtensions = @(".m3u", ".cue", ".chd", ".pbp", ".iso", ".exe", ".bat", ".cmd", ".lnk")
$DefaultIgnoredExtensions = @(
    ".xml", ".json", ".txt",
    ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp",
    ".mp4", ".mkv", ".avi",
    ".sav", ".srm", ".state",
    ".h3", ".tik", ".tmd", ".cert", ".app"
)

$DefaultExcludedFolders = @(
    "CloudLibrary",
    "MetadataCache",
    "ArtworkCache",
    "TitleAliases",
    "ExternalMetadata",
    "BIOS Menus",
    "Cracked",
    "Saves",
    "Save States",
    "Screenshots",
    "Manuals",
    "BoxArt",
    "Media",
    ".git",
    ".vs",
    "bin",
    "obj"
)

$PlatformAliases = @{
    "Nintendo Entertainment System"       = "Nintendo NES"
    "Super Nintendo Entertainment System" = "Nintendo SNES"
    "Nintendo 64"                         = "Nintendo 64"
    "Game Boy"                            = "Nintendo Game Boy"
    "Game Boy Color"                      = "Nintendo Game Boy Color"
    "Game Boy Advance"                    = "Nintendo Game Boy Advance"
    "GameCube"                            = "Nintendo GameCube"
    "Wii U"                               = "Nintendo Wii U"
    "Nintendo Switch"                     = "Nintendo Switch"
    "3DS Backup"                          = "Nintendo 3DS"
    "Nintendo 3DS"                        = "Nintendo 3DS"
    "Sega Genesis"                        = "Sega Genesis"
    "Game Gear"                           = "Sega Game Gear"
    "Dreamcast"                           = "Sega Dreamcast"
    "PlayStation"                         = "Sony PlayStation"
    "PlayStation 2"                       = "Sony PlayStation 2"
    "PlayStation 3"                       = "Sony PlayStation 3"
    "PlayStation 4"                       = "Sony PlayStation 4"
    "PlayStation Portable"                = "Sony PSP"
    "PSP"                                 = "Sony PSP"
    "Xbox"                                = "Microsoft Xbox"
    "Xbox 360"                            = "Microsoft Xbox 360"
    "PC"                                  = "PC"
    "Windows"                             = "PC"
}

if ($PSBoundParameters.ContainsKey("SourceRoot") -and $PSBoundParameters.ContainsKey("RcloneRemoteRoot")) {
    throw "Provide either -SourceRoot or -RcloneRemoteRoot, but not both."
}

if (-not $PSBoundParameters.ContainsKey("SourceRoot") -and -not $PSBoundParameters.ContainsKey("RcloneRemoteRoot")) {
    throw "Provide -SourceRoot for filesystem scanning or -RcloneRemoteRoot for rclone scanning."
}

$sourceMode = if ($PSBoundParameters.ContainsKey("RcloneRemoteRoot")) { "rclone" } else { "filesystem" }
# -Recurse remains a compatibility switch; scanning is always recursive.
$MaxScanEntries = 150000
$MaxManifestItems = 100000
$MaxManifestBytes = 32 * 1024 * 1024

if ($IncludeExtensions.Count -eq 0) {
    $IncludeExtensions = $DefaultSingleFileExtensions
}

$IncludeExtensions = @($IncludeExtensions | ForEach-Object { $_.ToLowerInvariant() } | Sort-Object -Unique)
$IgnoredExtensions = @($DefaultIgnoredExtensions | ForEach-Object { $_.ToLowerInvariant() } | Sort-Object -Unique)
$AllExcludedFolders = @($DefaultExcludedFolders + $ExcludeFolders) | Sort-Object -Unique

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = [System.IO.Path]::ChangeExtension($OutputPath, ".report.txt")
}

function Assert-OrdinaryOutputPath {
    param([string]$Path)
    $full = [IO.Path]::GetFullPath($Path)
    $leaf = Get-Item -LiteralPath $full -Force -ErrorAction SilentlyContinue
    if ($null -ne $leaf -and ($leaf.PSIsContainer -or
        ($leaf.Attributes -band [IO.FileAttributes]::ReparsePoint))) {
        throw 'Catalog output must be an ordinary file.'
    }
    $parent = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($full))
    while ($null -ne $parent) {
        if ($parent.Exists -and ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Catalog output directory must not be linked.'
        }
        $parent = $parent.Parent
    }
}

if (-not $DryRun) {
    $OutputPath = [IO.Path]::GetFullPath($OutputPath)
    $ReportPath = [IO.Path]::GetFullPath($ReportPath)
    if (-not $NoReport -and [string]::Equals($OutputPath, $ReportPath, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Manifest and report must have different output paths.'
    }
    $targets = @($OutputPath)
    if (-not $NoReport) { $targets += $ReportPath }
    foreach ($target in $targets) {
        Assert-OrdinaryOutputPath $target
        if ((Test-Path -LiteralPath $target) -and (-not $Overwrite)) {
            throw "Output file already exists. Use -Overwrite to replace it: $target"
        }
    }
}

function Normalize-SourcePath {
    param([string]$Path)
    return ($Path -replace "\\", "/").Trim("/")
}

function Convert-ToCachePath {
    param([string]$SourcePath)
    return (Normalize-SourcePath $SourcePath).Replace("/", "\")
}

function Get-PathParts {
    param([string]$Path)
    return @((Normalize-SourcePath $Path) -split "/" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Get-ParentPath {
    param([string]$Path)
    $normalized = Normalize-SourcePath $Path
    $lastSlash = $normalized.LastIndexOf("/")
    if ($lastSlash -lt 0) {
        return ""
    }

    return $normalized.Substring(0, $lastSlash)
}

function Get-FileNameFromPath {
    param([string]$Path)
    return [System.IO.Path]::GetFileName((Normalize-SourcePath $Path).Replace("/", "\"))
}

function Get-ExtensionFromPath {
    param([string]$Path)
    return [System.IO.Path]::GetExtension((Get-FileNameFromPath $Path)).ToLowerInvariant()
}

function Remove-ExtensionFromName {
    param([string]$Name)
    return [System.IO.Path]::GetFileNameWithoutExtension($Name)
}

function New-Slug {
    param([string]$Text)
    $clean = ($Text.ToLowerInvariant() -replace "[^a-z0-9]+", "-").Trim("-")
    if ([string]::IsNullOrWhiteSpace($clean)) {
        return "item"
    }

    return $clean
}

function Remove-KnownGameSuffixes {
    param([string]$Title)
    $result = $Title
    $result = $result -replace '\s*\((USA|Europe|Japan|World|En|Fr|De|Es|It|Rev\s*\d+|Beta|Proto|Demo)[^)]*\)\s*', ' '
    $result = $result -replace '\s*\[(Game|DLC|Update)\]\s*', ' '
    $result = $result -replace '\s*\[[0-9A-Fa-f]{8,16}\]\s*', ' '
    $result = $result -replace '\s+', ' '
    $result = $result.Trim()
    if ([string]::IsNullOrWhiteSpace($result)) {
        return $Title
    }

    return $result
}

function Get-PlatformFromPath {
    param([string]$Path)
    $parts = @(Get-PathParts $Path)
    if ($parts.Count -eq 0) {
        return "Unknown"
    }

    $first = $parts[0]
    if ($PlatformAliases.ContainsKey($first)) {
        return $PlatformAliases[$first]
    }

    return $first
}

function Test-IsUnderExcludedFolder {
    param([string]$Path)
    $parts = @(Get-PathParts $Path)
    foreach ($part in $parts) {
        foreach ($excluded in $AllExcludedFolders) {
            if ($part.Equals($excluded, [System.StringComparison]::OrdinalIgnoreCase)) {
                return $true
            }
        }
    }

    return $false
}

function Test-IsUnderDetectedDirectory {
    param([string]$FilePath, [System.Collections.Generic.HashSet[string]]$DetectedDirectories)
    $normalizedFile = Normalize-SourcePath $FilePath
    $slash = $normalizedFile.LastIndexOf('/')
    while ($slash -gt 0) {
        if ($DetectedDirectories.Contains($normalizedFile.Substring(0, $slash))) { return $true }
        $slash = $normalizedFile.LastIndexOf('/', $slash - 1)
    }
    return $false
}

function Get-PackageRole {
    param([string]$FolderName)
    if ($FolderName -match '\[DLC\]') { return "dlc" }
    if ($FolderName -match '\[Update\]') { return "update" }
    if ($FolderName -match '\[Game\]') { return "game" }
    return "game"
}

function New-ManifestItem {
    param(
        [string]$Title,
        [string]$Platform,
        [string]$SourcePath,
        [string]$SourceType,
        [string]$CachePath,
        [string]$InstallDirectory,
        [string]$LaunchFile,
        [string]$PackageRole = ""
    )

    $id = New-Slug "$Platform $Title $SourcePath"
    $item = [ordered]@{
        id               = $id
        title            = $Title
        platform         = $Platform
        sourcePath       = $SourcePath
        sourceType       = $SourceType
        cachePath        = $CachePath
        installDirectory = $InstallDirectory
        launchFile       = $LaunchFile
        notes            = "Generated by Personal Cloud Library Source manifest generator."
    }

    if (-not [string]::IsNullOrWhiteSpace($PackageRole)) {
        $item.packageRole = $PackageRole
    }

    return $item
}

function Quote-Argument {
    param([string]$Value)
    # Match Windows ProcessStartInfo quoting, including trailing backslashes.
    $result = [Text.StringBuilder]::new('"')
    $slashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq [char]92) { $slashes++; continue }
        if ($character -eq [char]34) {
            [void]$result.Append([char]92, $slashes * 2 + 1)
        }
        else { [void]$result.Append([char]92, $slashes) }
        [void]$result.Append($character)
        $slashes = 0
    }
    [void]$result.Append([char]92, $slashes * 2)
    [void]$result.Append([char]34)
    return $result.ToString()
}

function Invoke-RcloneJsonList {
    param([string]$RcloneExe, [string]$RemoteRoot, [bool]$UseFastList)
    $arguments = 'lsjson -R --no-mimetype --no-modtime --ask-password=false'
    if ($UseFastList) { $arguments += ' --fast-list' }
    $arguments += ' -- ' + (Quote-Argument $RemoteRoot)
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $RcloneExe
    $psi.Arguments = $arguments
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [Text.Encoding]::UTF8
    $psi.CreateNoWindow = $true
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $psi
    $started = $false
    try {
        if (-not $process.Start()) { throw 'Unable to start rclone.' }
        $started = $true
        $process.StandardInput.Close()
        $timer = [Diagnostics.Stopwatch]::StartNew()
        $stdout = [Text.StringBuilder]::new()
        $outBuffer = New-Object char[] 8192
        $errBuffer = New-Object char[] 4096
        $outRead = $process.StandardOutput.ReadAsync($outBuffer, 0, $outBuffer.Length)
        $errRead = $process.StandardError.ReadAsync($errBuffer, 0, $errBuffer.Length)
        while ($null -ne $outRead -or $null -ne $errRead -or -not $process.HasExited) {
            if ($timer.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                throw 'rclone listing exceeded the configured timeout.'
            }
            $progress = $false
            if ($null -ne $outRead -and $outRead.IsCompleted) {
                $count = $outRead.GetAwaiter().GetResult()
                if ($count -eq 0) { $outRead = $null }
                else {
                    if ($stdout.Length -gt ($MaxManifestBytes - $count)) {
                        throw 'rclone listing exceeds the character inspection budget.'
                    }
                    [void]$stdout.Append($outBuffer, 0, $count)
                    $outRead = $process.StandardOutput.ReadAsync($outBuffer, 0, $outBuffer.Length)
                }
                $progress = $true
            }
            if ($null -ne $errRead -and $errRead.IsCompleted) {
                $count = $errRead.GetAwaiter().GetResult()
                if ($count -eq 0) { $errRead = $null }
                else { $errRead = $process.StandardError.ReadAsync($errBuffer, 0, $errBuffer.Length) }
                $progress = $true
            }
            if (-not $progress) { Start-Sleep -Milliseconds 20 }
        }
        if ($process.ExitCode -ne 0) {
            throw "rclone lsjson failed with exit code $($process.ExitCode). Check the configured source."
        }
        if ($stdout.Length -eq 0) { throw 'rclone lsjson returned no JSON output.' }
        return @($stdout.ToString() | ConvertFrom-Json)
    }
    finally {
        if ($started) {
            try {
                if (-not $process.HasExited) {
                    $process.Kill()
                    [void]$process.WaitForExit(5000)
                }
            } catch { }
        }
        $process.Dispose()
    }
}

function Get-ScanEntries {
    param(
        [string]$Mode,
        [string]$FilesystemRoot,
        [string]$RemoteRoot,
        [string]$RcloneExe,
        [bool]$UseFastList
    )

    if ($Mode -eq "filesystem") {
        if (-not (Test-Path -LiteralPath $FilesystemRoot -PathType Container)) {
            throw "SourceRoot was not found or is not a directory: $FilesystemRoot"
        }

        $rootFullPath = [IO.Path]::GetFullPath($FilesystemRoot)
        $pending = [System.Collections.Generic.Stack[string]]::new()
        $pending.Push($rootFullPath)
        $results = [System.Collections.Generic.List[object]]::new()
        $count = 0
        while ($pending.Count -gt 0) {
            $current = $pending.Pop()
            $attributes = [IO.File]::GetAttributes($current)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -or
                -not ($attributes -band [IO.FileAttributes]::Directory)) {
                throw 'Library directory became linked or unavailable during discovery.'
            }
            foreach ($path in [IO.Directory]::EnumerateFileSystemEntries($current)) {
                if (++$count -gt $MaxScanEntries) { throw 'Library scan limit reached.' }
                $attributes = [IO.File]::GetAttributes($path)
                if ($attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
                $relative = $path.Substring($rootFullPath.Length).TrimStart([char]92, [char]47)
                if ([string]::IsNullOrWhiteSpace($relative)) { throw 'Invalid library entry.' }
                $isDir = ($attributes -band [IO.FileAttributes]::Directory) -ne 0
                $results.Add([pscustomobject]@{ Path = (Normalize-SourcePath $relative); IsDir = $isDir })
                if ($isDir) { $pending.Push($path) }
            }
        }

        return $results.ToArray()
    }

    return @(Invoke-RcloneJsonList -RcloneExe $RcloneExe -RemoteRoot $RemoteRoot -UseFastList $UseFastList)
}

$sourceRootSummary = if ($sourceMode -eq "filesystem") {
    [System.IO.Path]::GetFullPath($SourceRoot)
} else {
    $RcloneRemoteRoot.TrimEnd("/")
}

Write-Host ""
Write-Host "Personal Cloud Library Source Manifest Generator"
Write-Host "-----------------------------------------------"
Write-Host "Source mode: $sourceMode"
Write-Host "Source root: $sourceRootSummary"
Write-Host "Output:      $OutputPath"
Write-Host ""

if ($sourceMode -eq "filesystem") {
    Write-Host "Scanning filesystem..."
} else {
    Write-Host "Scanning remote with rclone lsjson..."
}

$entries = @(Get-ScanEntries -Mode $sourceMode -FilesystemRoot $SourceRoot -RemoteRoot $RcloneRemoteRoot -RcloneExe $RclonePath -UseFastList ([bool]$FastList))
if ($entries.Count -gt $MaxScanEntries) { throw 'Library scan limit reached.' }
$entryPaths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($entry in $entries) {
    $raw = [string]$entry.Path
    $normalized = Normalize-SourcePath $raw
    if ([string]::IsNullOrWhiteSpace($raw) -or $raw.StartsWith('/') -or $raw.StartsWith([string][char]92) -or
        ($normalized.Split('/') | Where-Object { $_ -eq '.' -or $_ -eq '..' -or [string]::IsNullOrWhiteSpace($_) -or $_ -match '[<>:"|?*\x00-\x1f]' }) -or
        -not $entryPaths.Add($normalized)) {
        throw 'Library enumeration returned an unsafe or duplicate path.'
    }
}
$dirs = @($entries | Where-Object { $_.IsDir -eq $true })
$files = @($entries | Where-Object { $_.IsDir -ne $true })

Write-Host "Entries returned: $($entries.Count)"
Write-Host ""

$directoryPaths = @{}
foreach ($dir in $dirs) {
    $path = Normalize-SourcePath $dir.Path
    if (-not [string]::IsNullOrWhiteSpace($path)) {
        $directoryPaths[$path.ToLowerInvariant()] = $true
    }
}

$filesByParent = @{}
foreach ($file in $files) {
    $path = Normalize-SourcePath $file.Path
    $parent = Get-ParentPath $path
    $parentKey = $parent.ToLowerInvariant()
    if (-not $filesByParent.ContainsKey($parentKey)) {
        $filesByParent[$parentKey] = New-Object System.Collections.Generic.List[object]
    }

    $filesByParent[$parentKey].Add($file)
}

$items = New-Object System.Collections.Generic.List[object]
$detectedDirectories = New-Object System.Collections.Generic.List[string]
$detectedDirectorySet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$skipped = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

foreach ($dir in $dirs) {
    $dirPath = Normalize-SourcePath $dir.Path
    if ([string]::IsNullOrWhiteSpace($dirPath)) {
        continue
    }

    if (Test-IsUnderExcludedFolder $dirPath) {
        $skipped.Add("Skipped excluded folder candidate: $dirPath")
        continue
    }

    $parts = @(Get-PathParts $dirPath)
    $dirName = $parts[$parts.Count - 1]

    $codeKey = (Normalize-SourcePath "$dirPath/code").ToLowerInvariant()
    $contentKey = (Normalize-SourcePath "$dirPath/content").ToLowerInvariant()
    $metaKey = (Normalize-SourcePath "$dirPath/meta").ToLowerInvariant()
    $isWiiUPackage = $directoryPaths.ContainsKey($codeKey) -and $directoryPaths.ContainsKey($contentKey) -and $directoryPaths.ContainsKey($metaKey)

    if ($isWiiUPackage) {
        $role = Get-PackageRole $dirName
        if (($role -ne "game") -and (-not $IncludeNonLaunchablePackages)) {
            $skipped.Add("Skipped Wii U non-game package: $dirPath")
            continue
        }

        $title = Remove-KnownGameSuffixes $dirName
        $platform = Get-PlatformFromPath $dirPath
        $sourcePath = $dirPath
        $cachePath = Convert-ToCachePath $sourcePath
        $item = New-ManifestItem -Title $title -Platform $platform -SourcePath $sourcePath -SourceType "directory" -CachePath $cachePath -InstallDirectory $cachePath -LaunchFile "" -PackageRole $role
        $items.Add($item)
        $detectedDirectories.Add($dirPath)
        [void]$detectedDirectorySet.Add($dirPath)
        continue
    }

    $parentKey = $dirPath.ToLowerInvariant()
    if ($filesByParent.ContainsKey($parentKey)) {
        $childFiles = $filesByParent[$parentKey].ToArray()
        $preferredLaunch = $null

        foreach ($ext in $DefaultDiscLaunchExtensions) {
            $preferredLaunch = $childFiles |
                Where-Object { (Get-ExtensionFromPath $_.Path) -eq $ext } |
                Sort-Object Path |
                Select-Object -First 1

            if ($null -ne $preferredLaunch) {
                break
            }
        }

        if ($null -ne $preferredLaunch) {
            $launchPath = Normalize-SourcePath $preferredLaunch.Path
            $launchFile = Get-FileNameFromPath $launchPath
            $title = Remove-KnownGameSuffixes $dirName
            $platform = Get-PlatformFromPath $dirPath
            $sourcePath = $dirPath
            $cachePath = Convert-ToCachePath $launchPath
            $installDirectory = Convert-ToCachePath $sourcePath
            $item = New-ManifestItem -Title $title -Platform $platform -SourcePath $sourcePath -SourceType "directory" -CachePath $cachePath -InstallDirectory $installDirectory -LaunchFile $launchFile
            $items.Add($item)
            $detectedDirectories.Add($dirPath)
        [void]$detectedDirectorySet.Add($dirPath)
            continue
        }
    }
}

foreach ($file in $files) {
    $path = Normalize-SourcePath $file.Path
    if ([string]::IsNullOrWhiteSpace($path)) {
        continue
    }

    if (Test-IsUnderExcludedFolder $path) {
        $skipped.Add("Skipped file under excluded folder: $path")
        continue
    }

    if (Test-IsUnderDetectedDirectory -FilePath $path -DetectedDirectories $detectedDirectorySet) {
        $skipped.Add("Skipped file inside detected directory package: $path")
        continue
    }

    $ext = Get-ExtensionFromPath $path
    if ($IgnoredExtensions -contains $ext) {
        $skipped.Add("Skipped ignored extension: $path")
        continue
    }

    if ($ext -eq ".bin") {
        $skipped.Add("Skipped standalone .bin item: $path")
        continue
    }

    if (-not ($IncludeExtensions -contains $ext)) {
        $skipped.Add("Skipped unsupported extension: $path")
        continue
    }

    $fileName = Get-FileNameFromPath $path
    $title = Remove-KnownGameSuffixes (Remove-ExtensionFromName $fileName)
    $platform = Get-PlatformFromPath $path
    $sourcePath = $path
    $cachePath = Convert-ToCachePath $sourcePath
    $installDirectory = Convert-ToCachePath (Get-ParentPath $sourcePath)
    if ([string]::IsNullOrWhiteSpace($installDirectory)) {
        $installDirectory = "."
    }

    $item = New-ManifestItem -Title $title -Platform $platform -SourcePath $sourcePath -SourceType "file" -CachePath $cachePath -InstallDirectory $installDirectory -LaunchFile $fileName
    $items.Add($item)
}

if ($items.Count -gt $MaxManifestItems) { throw 'Generated manifest exceeds importer item limit.' }
$generatedIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($item in $items) {
    if (-not $generatedIds.Add($item.id)) {
        throw 'Generated game IDs collide. Resolve duplicate names or provide an explicit manifest.'
    }
}
$sortedItems = @($items | Sort-Object platform, title, sourcePath)
$duplicateGroups = $sortedItems | Group-Object platform, title | Where-Object { $_.Count -gt 1 }
foreach ($group in $duplicateGroups) {
    $warnings.Add("Duplicate-looking title group: $($group.Name) has $($group.Count) entries")
}

$generatedAt = (Get-Date).ToUniversalTime().ToString("o")
$manifest = [ordered]@{
    version     = 3
    generatedBy = "Personal Cloud Library Source manifest generator"
    generatedAt = $generatedAt
    sourceMode  = $sourceMode
    itemCount   = $sortedItems.Count
    items       = $sortedItems
}

$json = $manifest | ConvertTo-Json -Depth 30
if ($json.Length -gt $MaxManifestBytes -or [Text.Encoding]::UTF8.GetByteCount($json) -gt $MaxManifestBytes) {
    throw 'Generated manifest exceeds importer size limit.'
}

Write-Host "Generated manifest items: $($sortedItems.Count)"
Write-Host "Detected directory items: $($detectedDirectories.Count)"
Write-Host "Skipped entries: $($skipped.Count)"
Write-Host "Warnings: $($warnings.Count)"
Write-Host ""

if ($DryRun) {
    Write-Host "Dry run enabled. No files were written."
    Write-Host ""
    Write-Host "First generated items:"
    $sortedItems | Select-Object -First 10 | ConvertTo-Json -Depth 30
    exit 0
}

function Write-CatalogText {
    param([string]$Path, [string]$Text)
    Assert-OrdinaryOutputPath $Path
    $existed = [IO.File]::Exists($Path)
    if ($existed -and -not $Overwrite) { throw 'Output already exists; replacement refused.' }
    $directory = [IO.Path]::GetDirectoryName($Path)
    [void][IO.Directory]::CreateDirectory($directory)
    Assert-OrdinaryOutputPath $Path
    $temp = Join-Path $directory ([guid]::NewGuid().ToString('N') + '.tmp')
    $bytes = [Text.UTF8Encoding]::new($false, $true).GetBytes($Text)
    $stream = [IO.FileStream]::new($temp, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $stream.Write($bytes, 0, $bytes.Length)
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }

    # Do not delete a staging pathname after failure. It may identify a
    # substituted object. Replacement remains a pathname-based operation;
    # use only a trusted output directory, never an adversarial workspace.
    Assert-OrdinaryOutputPath $Path
    Assert-OrdinaryOutputPath $temp
    if ($existed) {
        if (-not [IO.File]::Exists($Path)) { throw 'Prior output disappeared; staging retained.' }
        $backup = $Path + '.' + [guid]::NewGuid().ToString('N') + '.bak'
        if (Test-Path -LiteralPath $backup) { throw 'Backup destination is occupied.' }
        [IO.File]::Replace($temp, $Path, $backup)
    }
    else { [IO.File]::Move($temp, $Path) }
}

Write-CatalogText -Path $OutputPath -Text $json

if (-not $NoReport) {
    $reportLines = New-Object System.Collections.Generic.List[string]
    $reportLines.Add("Personal Cloud Library Source Manifest Generator Report")
    $reportLines.Add("================================================")
    $reportLines.Add("Generated at: $generatedAt")
    $reportLines.Add("Source mode: $sourceMode")
    $reportLines.Add("Source root: $sourceRootSummary")
    $reportLines.Add("Output: $OutputPath")
    $reportLines.Add("")
    $reportLines.Add("Total scanned entries: $($entries.Count)")
    $reportLines.Add("Total directories: $($dirs.Count)")
    $reportLines.Add("Total files: $($files.Count)")
    $reportLines.Add("Generated item count: $($sortedItems.Count)")
    $reportLines.Add("Detected directory item count: $($detectedDirectories.Count)")
    $reportLines.Add("Skipped entry count: $($skipped.Count)")
    $reportLines.Add("Warnings count: $($warnings.Count)")
    $reportLines.Add("")
    $reportLines.Add("Detected Directory Items")
    $reportLines.Add("-----------------------")
    if ($detectedDirectories.Count -eq 0) {
        $reportLines.Add("None")
    } else {
        foreach ($directory in $detectedDirectories) {
            $reportLines.Add("- $directory")
        }
    }

    $reportLines.Add("")
    $reportLines.Add("Warnings")
    $reportLines.Add("--------")
    if ($warnings.Count -eq 0) {
        $reportLines.Add("None")
    } else {
        foreach ($warning in $warnings) {
            $reportLines.Add("- $warning")
        }
    }

    $reportLines.Add("")
    $reportLines.Add("Skipped Entries")
    $reportLines.Add("---------------")
    if ($skipped.Count -eq 0) {
        $reportLines.Add("None")
    } else {
        foreach ($skip in ($skipped | Select-Object -First 2000)) {
            $reportLines.Add("- $skip")
        }

        if ($skipped.Count -gt 2000) {
            $reportLines.Add("- ... truncated. Total skipped entries: $($skipped.Count)")
        }
    }

    Write-CatalogText -Path $ReportPath -Text (($reportLines.ToArray() -join [Environment]::NewLine) + [Environment]::NewLine)
}

Write-Host "Manifest written:"
Write-Host $OutputPath
if (-not $NoReport) {
    Write-Host ""
    Write-Host "Report written:"
    Write-Host $ReportPath
}

Write-Host ""
Write-Host "Done."
