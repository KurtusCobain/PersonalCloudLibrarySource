$ErrorActionPreference = 'Stop'
$generator = Join-Path $PSScriptRoot 'generate-manifest.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('pcls-generator-tests-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $root 'source'
$target = Join-Path $root 'catalog.json'
$report = Join-Path $root 'catalog.report.txt'
New-Item -ItemType Directory -Path $source | Out-Null
try {
    [IO.File]::WriteAllText((Join-Path $source 'game.nes'), 'fixture')
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $generator -SourceRoot $source -OutputPath $target -ReportPath $report -DryRun
    if ($LASTEXITCODE -ne 0 -or (Test-Path $target) -or (Test-Path $report)) { throw 'Dry run wrote output or failed.' }
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $generator -SourceRoot $source -OutputPath $target -ReportPath $report
    if ($LASTEXITCODE -ne 0) { throw 'Initial generation failed.' }
    $before = [IO.File]::ReadAllText($target)
    $parsed = $before | ConvertFrom-Json
    if ($parsed.itemCount -ne 1) { throw 'Generated item count mismatch.' }
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $generator -SourceRoot $source -OutputPath $target -ReportPath $report -Overwrite
    if ($LASTEXITCODE -ne 0) { throw 'Overwrite generation failed.' }
    $backup = @(Get-ChildItem -LiteralPath $root -Filter 'catalog.json.*.bak' -File)
    if ($backup.Count -ne 1 -or [IO.File]::ReadAllText($backup[0].FullName) -cne $before) { throw 'Prior catalog was not preserved.' }
    [IO.File]::WriteAllText((Join-Path $source 'A+B.nes'), 'fixture')
    [IO.File]::WriteAllText((Join-Path $source 'A-B.nes'), 'fixture')
    $before = [IO.File]::ReadAllText($target)
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $generator -SourceRoot $source -OutputPath $target -ReportPath $report -Overwrite 2>&1
        $status = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $oldPreference }
    if ($status -eq 0 -or ($output -join "`n") -notmatch 'IDs collide' -or [IO.File]::ReadAllText($target) -cne $before) {
        throw 'Identity collision did not preserve existing catalog.'
    }
    Write-Host 'PCLS_CLI_GENERATOR_TESTS passed: dry run, initial generation, backup preservation, identity collision.'
}
finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
exit 0
