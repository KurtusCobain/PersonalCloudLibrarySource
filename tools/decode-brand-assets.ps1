param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Read-Base64Parts {
    param([Parameter(Mandatory = $true)][string]$Pattern)

    $parts = Get-ChildItem -Path $Pattern | Sort-Object Name
    if (-not $parts)
    {
        throw "No brand asset parts matched: $Pattern"
    }

    return ($parts | ForEach-Object {
        (Get-Content -LiteralPath $_.FullName -Raw).Trim()
    }) -join ''
}

function Write-Base64File {
    param(
        [Parameter(Mandatory = $true)][string]$Base64,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    $parent = Split-Path -Parent $Destination
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    [IO.File]::WriteAllBytes($Destination, [Convert]::FromBase64String($Base64))
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$iconBase64 = (Get-Content -LiteralPath (Join-Path $repoRoot 'tools\pcls-icon-flat.b64') -Raw).Trim()
$wideBase64 = Read-Base64Parts (Join-Path $repoRoot 'tools\assets\pcls-logo-wide.part*')

Write-Base64File $iconBase64 (Join-Path $OutputDirectory 'icon.png')
Write-Base64File $wideBase64 (Join-Path $OutputDirectory 'Assets\pcls-logo-wide.png')

Write-Host "Decoded PCLS brand assets into: $OutputDirectory"
