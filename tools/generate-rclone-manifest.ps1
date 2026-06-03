<#
.SYNOPSIS
Thin compatibility wrapper for rclone-based manifest generation.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RcloneRemoteRoot,
    [string]$OutputPath = ".\personal-cloud-library.generated.json",
    [string]$ReportPath = "",
    [string]$RclonePath = "rclone",
    [string[]]$IncludeExtensions = @(),
    [string[]]$ExcludeFolders = @(),
    [switch]$Overwrite,
    [switch]$DryRun,
    [switch]$FastList,
    [switch]$IncludeNonLaunchablePackages,
    [switch]$NoReport
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$scriptPath = Join-Path -Path $PSScriptRoot -ChildPath "generate-manifest.ps1"
if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) {
    throw "The universal generator script was not found: $scriptPath"
}

& $scriptPath `
    -RcloneRemoteRoot $RcloneRemoteRoot `
    -OutputPath $OutputPath `
    -ReportPath $ReportPath `
    -RclonePath $RclonePath `
    -IncludeExtensions $IncludeExtensions `
    -ExcludeFolders $ExcludeFolders `
    -Overwrite:$Overwrite `
    -DryRun:$DryRun `
    -FastList:$FastList `
    -IncludeNonLaunchablePackages:$IncludeNonLaunchablePackages `
    -NoReport:$NoReport
