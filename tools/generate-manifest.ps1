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
    "ROMcade_Data",
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
$isRecursive = $true

if ($IncludeExtensions.Count -eq 0) {
    $IncludeExtensions = $DefaultSingleFileExtensions
}

$IncludeExtensions = @($IncludeExtensions | ForEach-Object { $_.ToLowerInvariant() } | Sort-Object -Unique)
$IgnoredExtensions = @($DefaultIgnoredExtensions | ForEach-Object { $_.ToLowerInvariant() } | Sort-Object -Unique)
$AllExcludedFolders = @($DefaultExcludedFolders + $ExcludeFolders) | Sort-Object -Unique

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = [System.IO.Path]::ChangeExtension($OutputPath, ".report.txt")
}

if ((Test-Path -LiteralPath $OutputPath) -and (-not $Overwrite) -and (-not $DryRun)) {
    throw "Output file already exists. Use -Overwrite to replace it: $OutputPath"
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
    param(
        [string]$FilePath,
        [string[]]$DetectedDirectories
    )

    $normalizedFile = Normalize-SourcePath $FilePath
    foreach ($directory in $DetectedDirectories) {
        $normalizedDirectory = (Normalize-SourcePath $directory).TrimEnd("/") + "/"
        if ($normalizedFile.StartsWith($normalizedDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
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
    if ($null -eq $Value) {
        return '""'
    }

    return '"' + ($Value -replace '"', '\"') + '"'
}

function Invoke-RcloneJsonList {
    param(
        [string]$RcloneExe,
        [string]$RemoteRoot,
        [bool]$UseFastList
    )

    $arguments = "lsjson " + (Quote-Argument $RemoteRoot) + " -R --no-mimetype --no-modtime"
    if ($UseFastList) {
        $arguments += " --fast-list"
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $RcloneExe
    $psi.Arguments = $arguments
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi

    try {
        [void]$process.Start()
    }
    catch {
        throw "Unable to start rclone. Check that rclone is installed or pass -RclonePath. Error: $($_.Exception.Message)"
    }

    $stdout = $process.StandardOutput.ReadToEnd()
    $stderr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        throw "rclone lsjson failed with exit code $($process.ExitCode). Error: $stderr"
    }

    if ([string]::IsNullOrWhiteSpace($stdout)) {
        throw "rclone lsjson returned no JSON output."
    }

    try {
        return @($stdout | ConvertFrom-Json)
    }
    catch {
        throw "Failed to parse rclone lsjson output as JSON. Error: $($_.Exception.Message)"
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

        $rootFullPath = [System.IO.Path]::GetFullPath($FilesystemRoot)
        $dirEntries = Get-ChildItem -LiteralPath $rootFullPath -Directory -Recurse -Force
        $fileEntries = Get-ChildItem -LiteralPath $rootFullPath -File -Recurse -Force

        $results = New-Object System.Collections.Generic.List[object]

        foreach ($dir in $dirEntries) {
            $relative = $dir.FullName.Substring($rootFullPath.Length).TrimStart('\', '/')
            if ([string]::IsNullOrWhiteSpace($relative)) {
                continue
            }

            $results.Add([pscustomobject]@{
                Path  = (Normalize-SourcePath $relative)
                IsDir = $true
            })
        }

        foreach ($file in $fileEntries) {
            $relative = $file.FullName.Substring($rootFullPath.Length).TrimStart('\', '/')
            if ([string]::IsNullOrWhiteSpace($relative)) {
                continue
            }

            $results.Add([pscustomobject]@{
                Path  = (Normalize-SourcePath $relative)
                IsDir = $false
            })
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

    if (Test-IsUnderDetectedDirectory -FilePath $path -DetectedDirectories $detectedDirectories.ToArray()) {
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

$outputDir = Split-Path -Path $OutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($outputDir) -and -not (Test-Path -LiteralPath $outputDir -PathType Container)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$json | Set-Content -LiteralPath $OutputPath -Encoding UTF8

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

    $reportLines | Set-Content -LiteralPath $ReportPath -Encoding UTF8
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
