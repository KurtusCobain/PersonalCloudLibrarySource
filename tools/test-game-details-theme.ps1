# Run with Windows PowerShell -STA. Exercises WPF resource inheritance, not source text.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework
$path = Join-Path (Split-Path -Parent $PSScriptRoot) 'PersonalCloudLibrarySource\Views\CloudGameDetailsView.xaml'
$text = (Get-Content -LiteralPath $path -Raw) -replace 'x:Class="[^"]+"', ''
$view = [Windows.Markup.XamlReader]::Parse($text)
$title = $view.Content.Content.Children[0]
$view.Resources['TextBrush'] = [Windows.Media.Brushes]::White
if ($title.Foreground.Color -ne [Windows.Media.Colors]::White) {
    throw "Game details title does not inherit the host theme TextBrush: $($title.Foreground)"
}
$view.Resources['TextBrush'] = [Windows.Media.Brushes]::Cyan
if ($title.Foreground.Color -ne [Windows.Media.Colors]::Cyan) {
    throw 'Game details title did not follow the changed theme brush.'
}
Write-Host 'PASS: game details text inherits and follows the host theme TextBrush.'
