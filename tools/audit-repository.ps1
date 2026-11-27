[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ReportPath
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
Import-Module (Join-Path $PSScriptRoot 'ReleaseValidation.psm1') -Force
$rootPrefix = $root.TrimEnd([char]92, [char]47) + [IO.Path]::DirectorySeparatorChar
$reportFullPath = [IO.Path]::GetFullPath($ReportPath)
if ($reportFullPath.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Repository audit report must be written outside the tracked checkout.'
}

Push-Location -LiteralPath $root
try {
    $paths = @(& git -c core.quotepath=false ls-files)
    if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate tracked paths.' }
    $commit = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Could not identify checkout commit.' }
    $records = [System.Collections.Generic.List[object]]::new()
    $failures = [System.Collections.Generic.List[string]]::new()
    $candidates = [System.Collections.Generic.List[object]]::new()
    $tokens = [System.Collections.Generic.Dictionary[string,int]]::new([StringComparer]::Ordinal)
    $sources = [System.Collections.Generic.List[object]]::new()
    $reviewSites = [System.Collections.Generic.List[object]]::new()
    $sourceLines = 0
    $distributionFiles = @(
        'PersonalCloudLibrarySource/extension.yaml',
        'playnite-addon/addon-database.yaml',
        'playnite-addon/installer.yaml',
        'docs/upstream/PersonalCloudLibrarySource-v032.yaml'
    )
    foreach ($relative in $paths) {
        $path = Join-Path $root $relative
        $full = [IO.Path]::GetFullPath($path)
        if (-not $full.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Tracked path escapes checkout: $relative"
        }
        $item = Get-Item -LiteralPath $path
        if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Unexpected tracked path type: $relative"
        }
        $kind = [IO.Path]::GetExtension($relative).ToLowerInvariant()
        $checks = [System.Collections.Generic.List[string]]::new()
        $checks.Add('sha256')
        try {
            switch ($kind) {
                { $_ -in '.ps1', '.psm1' } {
                    $parseTokens = $null
                    $parseErrors = $null
                    [void][System.Management.Automation.Language.Parser]::ParseFile(
                        $full, [ref]$parseTokens, [ref]$parseErrors)
                    if ($parseErrors.Count -gt 0) {
                        throw (($parseErrors | ForEach-Object {
                            "line $($_.Extent.StartLineNumber): $($_.Message)"
                        }) -join '; ')
                    }
                    $checks.Add('powershell-syntax')
                    break
                }
                { $_ -in '.xaml', '.csproj', '.config' } {
                    $settings = [System.Xml.XmlReaderSettings]::new()
                    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
                    $settings.XmlResolver = $null
                    $reader = [System.Xml.XmlReader]::Create($full, $settings)
                    try { while ($reader.Read()) {} } finally { $reader.Dispose() }
                    $checks.Add('xml-wellformed')
                    break
                }
                '.json' {
                    $null = Get-Content -LiteralPath $full -Raw | ConvertFrom-Json -ErrorAction Stop
                    $checks.Add('json-syntax')
                    break
                }
                { $_ -in '.yaml', '.yml' } {
                    if ($relative -in $distributionFiles) {
                        $null = Read-DistributionYaml -Path $full
                        $checks.Add('distribution-yaml-subset')
                    }
                    else { $checks.Add('yaml-inventory-only') }
                    break
                }
            }
            if ($kind -in '.cs', '.xaml', '.ps1', '.psm1') {
                $source = [IO.File]::ReadAllText($full)
                # Count exact identifier spellings across code, XAML and scripts,
                # including comments/strings. This is intentionally conservative
                # lexical triage, not semantic reachability or removal authority.
                foreach ($match in [regex]::Matches($source, '\b[A-Za-z_][A-Za-z0-9_]*\b')) {
                    $name = $match.Value
                    if ($tokens.ContainsKey($name)) { $tokens[$name]++ }
                    else { $tokens.Add($name, 1) }
                }
                if ($kind -eq '.cs' -and $relative.StartsWith('PersonalCloudLibrarySource/')) {
                    $sources.Add([pscustomobject]@{ path = $relative; text = $source })
                    $checks.Add('private-member-triage')
                    # Record every textual review site, including comments and
                    # compatibility APIs. This is a review index, not a proof
                    # of reachability or permission to remove code.
                    $lines = $source -split '\r?\n'
                    $sourceLines += $lines.Count
                    $sitePattern = 'File\.(Delete|Replace|Move|WriteAll\w*|Copy)|Directory\.(Delete|Move|CreateDirectory)|Process\.Start|DllImport|SafeCacheDeletionExecutor|DeleteInspectedDirectory|RcloneProcessFactory|WindowsJobProcessFactory|SetFileInformationByHandle|CreateProcessW'
                    for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
                        foreach ($site in [regex]::Matches($lines[$lineIndex], $sitePattern)) {
                            $reviewSites.Add([pscustomobject]@{
                                path = $relative
                                line = $lineIndex + 1
                                identifier = $site.Value
                            })
                        }
                    }
                    $checks.Add('security-sensitive-reference-index')
                }
            }
        }
        catch { $failures.Add("${relative}: $($_.Exception.Message)") }
        $records.Add([pscustomobject]@{
            path = $relative
            bytes = $item.Length
            sha256 = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash
            checks = @($checks.ToArray())
        })
    }
    $pattern = '(?m)^\s*private\s+(?:(?:static|async|unsafe|new|readonly|volatile)\s+)*[A-Za-z_][A-Za-z0-9_.<>,\[\]? ]*?\s+(?<name>[A-Za-z_]\w*)\s*(?:\(|[=;])'
    foreach ($source in $sources) {
        foreach ($match in [regex]::Matches($source.text, $pattern)) {
            $name = $match.Groups['name'].Value
            if ($tokens.ContainsKey($name) -and $tokens[$name] -eq 1) {
                $line = 1 + ($source.text.Substring(0, $match.Groups['name'].Index).Split([char]10).Length - 1)
                $candidates.Add([pscustomobject]@{ path = $source.path; line = $line; identifier = $name })
            }
        }
    }
    $report = [ordered]@{
        commit = $commit
        tracked_files = $records.Count
        production_csharp_files = $sources.Count
        production_csharp_physical_lines = $sourceLines
        security_sensitive_review_sites = @($reviewSites.ToArray())
        failure_count = $failures.Count
        failures = @($failures.ToArray())
        private_member_candidates = @($candidates.ToArray())
        files = @($records.ToArray())
        limitations = @(
            'Lexical candidate identification is not semantic dead-code proof.',
            'Security-sensitive review sites include textual references; they are not an exhaustive call graph or proof of authorization.',
            'YAML outside distribution subset is inventoried only.',
            'C# compilation, WPF compilation, CodeQL, dependency and runtime gates are separate.',
            'Markdown and binary assets are hashed, not semantically or visually validated.'
        )
    }
    $parent = Split-Path -Parent $reportFullPath
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportFullPath -Encoding utf8
    Write-Host "PCLS_REPOSITORY_AUDIT files=$($records.Count) production_cs=$($sources.Count) candidates=$($candidates.Count) failures=$($failures.Count)"
    Write-Host "PCLS_SOURCE_REVIEW_INDEX lines=$sourceLines sites=$($reviewSites.Count)"
    foreach ($candidate in $candidates) {
        Write-Host "PCLS_AUDIT_CANDIDATE $($candidate.path):$($candidate.line) $($candidate.identifier)"
    }
    foreach ($failure in $failures) { Write-Host "PCLS_AUDIT_FAILURE $failure" }
    if ($failures.Count -gt 0) { throw 'Repository syntax/inventory audit failed.' }
}
finally { Pop-Location }
