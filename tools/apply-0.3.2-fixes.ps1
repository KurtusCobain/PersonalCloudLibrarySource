$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot

function Replace-Required {
    param([string]$Path, [string]$Old, [string]$New)
    $text = Get-Content -LiteralPath $Path -Raw
    if (-not $text.Contains($Old)) { throw "Expected text not found in $Path" }
    Set-Content -LiteralPath $Path -Value ($text.Replace($Old, $New)) -Encoding UTF8
}

function Decode-Parts {
    param([string]$Pattern, [string]$Destination)
    $parts = Get-ChildItem -Path $Pattern | Sort-Object Name
    if (-not $parts) { throw "No asset parts matched $Pattern" }
    $base64 = ($parts | ForEach-Object { (Get-Content -LiteralPath $_.FullName -Raw).Trim() }) -join ''
    New-Item -ItemType Directory -Path (Split-Path -Parent $Destination) -Force | Out-Null
    [IO.File]::WriteAllBytes($Destination, [Convert]::FromBase64String($base64))
}

# Decode approved branding assets.
$iconData = (Get-Content -LiteralPath (Join-Path $root 'tools\pcls-icon-flat.b64') -Raw).Trim()
[IO.File]::WriteAllBytes((Join-Path $root 'PersonalCloudLibrarySource\icon.png'), [Convert]::FromBase64String($iconData))
Decode-Parts (Join-Path $root 'tools\assets\pcls-logo-wide.part*') (Join-Path $root 'PersonalCloudLibrarySource\Assets\pcls-logo-wide.png')
Decode-Parts (Join-Path $root 'tools\assets\pcls-logo-full.part*') (Join-Path $root 'docs\assets\pcls-logo-full.png')

# Safer rclone timeout defaults and migration.
$settingsPath = Join-Path $root 'PersonalCloudLibrarySource\PersonalCloudLibrarySourceSettings.cs'
Replace-Required $settingsPath 'private int rcloneTimeoutSeconds = 30;' 'private int rcloneTimeoutSeconds = 90;'

$migrationPath = Join-Path $root 'PersonalCloudLibrarySource\SettingsMigrationService.cs'
$migrationText = Get-Content -LiteralPath $migrationPath -Raw
$oldMigration = @'
            if (wasMigrated)
            {
                settings.SettingsVersion = PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion;
            }
'@
$newMigration = @'
            if (wasMigrated && previousVersion <= 3 && settings.RcloneTimeoutSeconds == 30)
            {
                settings.RcloneTimeoutSeconds = 90;
            }

            if (wasMigrated)
            {
                settings.SettingsVersion = PersonalCloudLibrarySourceSettingsV3.CurrentSettingsVersion;
            }
'@
if (-not $migrationText.Contains($oldMigration)) { throw 'Migration insertion point was not found.' }
Set-Content -LiteralPath $migrationPath -Value ($migrationText.Replace($oldMigration, $newMigration)) -Encoding UTF8

# Make the setup wizard inherit Playnite foreground colors.
$wizardPath = Join-Path $root 'PersonalCloudLibrarySource\Setup\SetupWizardView.xaml'
$wizardText = Get-Content -LiteralPath $wizardPath -Raw
if ($wizardText -notmatch 'Foreground="\{DynamicResource TextBrush\}"') {
    $wizardText = $wizardText.Replace('d:DesignWidth="720">', "d:DesignWidth=`"720`"`r`n             Foreground=`"{DynamicResource TextBrush}`">")
}
Set-Content -LiteralPath $wizardPath -Value $wizardText -Encoding UTF8

# Show only fields relevant to the selected provider.
$settingsViewPath = Join-Path $root 'PersonalCloudLibrarySource\PersonalCloudLibrarySourceSettingsView.xaml'
$settingsView = Get-Content -LiteralPath $settingsViewPath -Raw
$sourcePattern = '(?s)                        <GroupBox Header="Existing manifest or local/NAS library".*?                        </GroupBox>\s*                    </StackPanel>\s*                </ScrollViewer>\s*            </TabItem>'
$sourceReplacement = @'
                        <GroupBox Header="Existing manifest file" Margin="0,0,0,14">
                            <GroupBox.Style>
                                <Style TargetType="GroupBox">
                                    <Setter Property="Visibility" Value="Collapsed" />
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding Settings.SourceProviderType}" Value="LocalFile">
                                            <Setter Property="Visibility" Value="Visible" />
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </GroupBox.Style>
                            <Grid Margin="12">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" />
                                </Grid.RowDefinitions>
                                <TextBlock Grid.Row="0" Grid.ColumnSpan="2" Text="Manifest JSON path" FontWeight="SemiBold" />
                                <TextBox Grid.Row="1" Grid.Column="0" Text="{Binding Settings.LocalManifestPath, UpdateSourceTrigger=PropertyChanged}" Margin="0,5,8,0" />
                                <Button Grid.Row="1" Grid.Column="1" Content="Browse" Width="90" Margin="0,5,0,0" Click="BrowseLocalManifestPath_Click" />
                            </Grid>
                        </GroupBox>

                        <GroupBox Header="Local drive, external drive, or NAS folder" Margin="0,0,0,14">
                            <GroupBox.Style>
                                <Style TargetType="GroupBox">
                                    <Setter Property="Visibility" Value="Collapsed" />
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding Settings.SourceProviderType}" Value="LocalFolder">
                                            <Setter Property="Visibility" Value="Visible" />
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </GroupBox.Style>
                            <Grid Margin="12">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" />
                                </Grid.RowDefinitions>
                                <TextBlock Grid.Row="0" Grid.ColumnSpan="2" Text="Library root folder or network path" FontWeight="SemiBold" />
                                <TextBox Grid.Row="1" Grid.Column="0" Text="{Binding Settings.LocalLibraryRoot, UpdateSourceTrigger=PropertyChanged}" Margin="0,5,8,12" />
                                <Button Grid.Row="1" Grid.Column="1" Content="Browse" Width="90" Margin="0,5,0,12" Click="BrowseLocalLibraryRoot_Click" />
                                <TextBlock Grid.Row="2" Grid.ColumnSpan="2" Text="Manifest path relative to the library root (optional)" FontWeight="SemiBold" />
                                <TextBox Grid.Row="3" Grid.ColumnSpan="2" Text="{Binding Settings.ManifestRelativePath, UpdateSourceTrigger=PropertyChanged}" Margin="0,5,0,0" />
                            </Grid>
                        </GroupBox>

                        <GroupBox Header="Cloud storage through rclone">
                            <GroupBox.Style>
                                <Style TargetType="GroupBox">
                                    <Setter Property="Visibility" Value="Collapsed" />
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding Settings.SourceProviderType}" Value="RcloneRemote">
                                            <Setter Property="Visibility" Value="Visible" />
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </GroupBox.Style>
                            <Grid Margin="12">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto" /><RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" /><RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" /><RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" /><RowDefinition Height="Auto" />
                                    <RowDefinition Height="Auto" />
                                </Grid.RowDefinitions>
                                <TextBlock Grid.Row="0" Grid.ColumnSpan="2" Text="rclone executable" FontWeight="SemiBold" />
                                <TextBox Grid.Row="1" Grid.Column="0" Text="{Binding Settings.RcloneExecutablePath, UpdateSourceTrigger=PropertyChanged}" Margin="0,5,8,12" />
                                <Button Grid.Row="1" Grid.Column="1" Content="Browse" Width="90" Margin="0,5,0,12" Click="BrowseRcloneExecutablePath_Click" />
                                <TextBlock Grid.Row="2" Grid.ColumnSpan="2" Text="Configured remote name" FontWeight="SemiBold" />
                                <TextBox Grid.Row="3" Grid.ColumnSpan="2" Text="{Binding Settings.RcloneRemoteName, UpdateSourceTrigger=PropertyChanged}" Margin="0,5,0,12" />
                                <TextBlock Grid.Row="4" Grid.ColumnSpan="2" Text="Remote manifest path" FontWeight="SemiBold" />
                                <TextBox Grid.Row="5" Grid.ColumnSpan="2" Text="{Binding Settings.RcloneManifestPath, UpdateSourceTrigger=PropertyChanged}" Margin="0,5,0,12" />
                                <TextBlock Grid.Row="6" Grid.ColumnSpan="2" Text="Remote content root" FontWeight="SemiBold" />
                                <TextBox Grid.Row="7" Grid.ColumnSpan="2" Text="{Binding Settings.RcloneContentRoot, UpdateSourceTrigger=PropertyChanged}" Margin="0,5,0,12" />
                                <WrapPanel Grid.Row="8" Grid.ColumnSpan="2">
                                    <Button Content="Test rclone connection" Click="TestRcloneConnection_Click" Margin="0,0,8,8" Padding="10,5" />
                                    <Button Content="Test manifest load" Click="TestManifestLoad_Click" Margin="0,0,8,8" Padding="10,5" />
                                </WrapPanel>
                            </Grid>
                        </GroupBox>
                    </StackPanel>
                </ScrollViewer>
            </TabItem>
'@
$updatedSettingsView = [regex]::Replace($settingsView, $sourcePattern, $sourceReplacement, 1)
if ($updatedSettingsView -eq $settingsView) { throw 'Source settings XAML section was not replaced.' }
Set-Content -LiteralPath $settingsViewPath -Value $updatedSettingsView -Encoding UTF8

# Return verification reports and show actionable errors.
$viewModelPath = Join-Path $root 'PersonalCloudLibrarySource\PersonalCloudLibrarySourceSettings.cs'
$viewModelText = Get-Content -LiteralPath $viewModelPath -Raw
$verifyPattern = '(?s)        public void VerifySetup\(\)\s*        \{.*?        \}\s*\r?\n\s*        public void TestRcloneConnection\(\)'
$verifyReplacement = @'
        public LibraryVerificationReport VerifySetup()
        {
            try
            {
                List<string> errors;
                VerifySettings(out errors);
                var report = plugin.GenerateVerificationReport(Settings, errors);
                RefreshSetupStatusFromVerificationReport(report);
                MessageBox.Show(VerificationMessageBuilder.Build(report), "Personal Cloud Library Source");
                return report;
            }
            catch (Exception ex)
            {
                RefreshSetupStatusFromException(ex);
                MessageBox.Show("Setup verification failed: " + ex.Message, "Personal Cloud Library Source");
                return null;
            }
        }

        public void TestRcloneConnection()
'@
$updatedViewModel = [regex]::Replace($viewModelText, $verifyPattern, $verifyReplacement, 1)
if ($updatedViewModel -eq $viewModelText) { throw 'VerifySetup method was not replaced.' }
Set-Content -LiteralPath $viewModelPath -Value $updatedViewModel -Encoding UTF8

# Synchronize verification results with dashboard state.
$navigationPath = Join-Path $root 'PersonalCloudLibrarySource\PersonalCloudLibrarySource.Navigation.cs'
$navigation = Get-Content -LiteralPath $navigationPath -Raw
$navigation = $navigation.Replace('private DashboardStateStore dashboardStateStore;', "private DashboardStateStore dashboardStateStore;`r`n        private LibraryVerificationReport latestVerificationReport;")
$oldBuild = @'
            dashboardStateStore.Current = dashboardLibraryStatusService.BuildState(
                pluginSettings,
                new LibraryStatusContext
                {
                    SourceAvailable = IsConfiguredSourceAvailable(pluginSettings),
                    ManifestItemCount = manifestCount,
                    ImportedGameCount = importedCount,
                    CachedGameCount = cachedCount,
                    WarningCount = 0,
                    ActiveTransferCount = GetActiveTransferCount(),
                    FailedTransferCount = GetFailedTransferCount(),
                    SourceDescription = ResolveDashboardSourceDescription(pluginSettings),
                    ManifestDescription = DescribeManifestPath(pluginSettings),
                    CachePath = pluginSettings?.LocalCacheFolder ?? string.Empty
                });
'@
$newBuild = @'
            var context = new LibraryStatusContext
            {
                SourceAvailable = IsConfiguredSourceAvailable(pluginSettings),
                ManifestItemCount = manifestCount,
                ImportedGameCount = importedCount,
                CachedGameCount = cachedCount,
                WarningCount = 0,
                ActiveTransferCount = GetActiveTransferCount(),
                FailedTransferCount = GetFailedTransferCount(),
                SourceDescription = ResolveDashboardSourceDescription(pluginSettings),
                ManifestDescription = DescribeManifestPath(pluginSettings),
                CachePath = pluginSettings?.LocalCacheFolder ?? string.Empty
            };
            context = VerificationDashboardStateService.Apply(context, latestVerificationReport);
            dashboardStateStore.Current = dashboardLibraryStatusService.BuildState(pluginSettings, context);
'@
if (-not $navigation.Contains($oldBuild)) { throw 'Dashboard state build block was not found.' }
$navigation = $navigation.Replace($oldBuild, $newBuild)
$navigation = $navigation.Replace('            settings.VerifySetup();', '            latestVerificationReport = settings.VerifySetup();')
Set-Content -LiteralPath $navigationPath -Value $navigation -Encoding UTF8

# Register service and packaged assets.
$projectPath = Join-Path $root 'PersonalCloudLibrarySource\PersonalCloudLibrarySource.csproj'
$project = Get-Content -LiteralPath $projectPath -Raw
if ($project -notmatch 'Dashboard\\VerificationDashboardStateService.cs') {
    $project = $project.Replace('<Compile Include="Dashboard\LibraryStatusService.cs" />', '<Compile Include="Dashboard\LibraryStatusService.cs" />' + "`r`n    <Compile Include=`"Dashboard\VerificationDashboardStateService.cs`" />")
}
if ($project -notmatch 'Assets\\pcls-logo-wide.png') {
    $project = $project.Replace('<Content Include="icon.png">', '<Content Include="Assets\pcls-logo-wide.png">' + "`r`n      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>`r`n    </Content>`r`n    <Content Include=`"icon.png`">")
}
Set-Content -LiteralPath $projectPath -Value $project -Encoding UTF8

$packagePath = Join-Path $root 'tools\package-extension.ps1'
$package = Get-Content -LiteralPath $packagePath -Raw
if ($package -notmatch 'projectOutput "Assets"') {
    $insert = @'

$assetsPath = Join-Path $projectOutput "Assets"
if (Test-Path -LiteralPath $assetsPath) {
    Copy-Item -LiteralPath $assetsPath -Destination $packageFolder -Recurse -Force
}
'@
    $package = $package.Replace('if (Test-Path -LiteralPath $packagePath) {', $insert + "`r`nif (Test-Path -LiteralPath `$packagePath) {")
}
Set-Content -LiteralPath $packagePath -Value $package -Encoding UTF8

# Version this test iteration as 0.3.2.
Replace-Required (Join-Path $root 'PersonalCloudLibrarySource\extension.yaml') 'Version: 0.3.1' 'Version: 0.3.2'
Replace-Required (Join-Path $root 'PersonalCloudLibrarySource\Properties\AssemblyInfo.cs') 'AssemblyVersion("0.3.1.0")' 'AssemblyVersion("0.3.2.0")'
Replace-Required (Join-Path $root 'PersonalCloudLibrarySource\Properties\AssemblyInfo.cs') 'AssemblyFileVersion("0.3.1.0")' 'AssemblyFileVersion("0.3.2.0")'

Write-Host '0.3.2 fixes and branding assets applied.'
