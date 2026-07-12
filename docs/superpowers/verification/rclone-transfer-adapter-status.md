# Rclone Transfer Adapter Verification

- Result: **FAIL**
- Verified at: 2026-07-12T02:56:59Z
- Branch: feature/user-friendly-dashboard
- Commit tested: 71e738b46928cade3a2a4cb7af1a5b2c03efa603 plus the focused project registration in this run
- NuGet restore: success
- Debug build: failure
- NUnitLite tests: skipped

## restore log tail

MSBuild auto-detection: using msbuild version '18.7.8.30822' from 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin'.
Restoring NuGet package NUnitLite.3.14.0.
Restoring NuGet package PlayniteSDK.6.16.0.
Restoring NuGet package NUnit.3.14.0.
  GET https://api.nuget.org/v3-flatcontainer/playnitesdk/6.16.0/playnitesdk.6.16.0.nupkg
  GET https://api.nuget.org/v3-flatcontainer/nunitlite/3.14.0/nunitlite.3.14.0.nupkg
  GET https://api.nuget.org/v3-flatcontainer/nunit/3.14.0/nunit.3.14.0.nupkg
  OK https://api.nuget.org/v3-flatcontainer/nunitlite/3.14.0/nunitlite.3.14.0.nupkg 4ms
  OK https://api.nuget.org/v3-flatcontainer/nunit/3.14.0/nunit.3.14.0.nupkg 11ms
  OK https://api.nuget.org/v3-flatcontainer/playnitesdk/6.16.0/playnitesdk.6.16.0.nupkg 13ms
Installed PlayniteSDK 6.16.0 from https://api.nuget.org/v3/index.json to C:\Users\runneradmin\.nuget\packages\playnitesdk\6.16.0 with content hash MYb4x1i0kzKRmyWU3p4jQyBfY/7xSo+xydFOZAnDWU6Y1qrpuXRiyczCAkXbun/AzeXc5WvrSp0Gzp42U2fNaA==.
Installed NUnit 3.14.0 from https://api.nuget.org/v3/index.json to C:\Users\runneradmin\.nuget\packages\nunit\3.14.0 with content hash R7iPwD7kbOaP3o2zldWJbWeMQAvDKD0uld27QvA3PAALl1unl7x0v2J7eGiJOYjimV/BuGT4VJmr45RjS7z4LA==.
Adding package 'PlayniteSDK.6.16.0' to folder 'D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages'
Adding package 'NUnit.3.14.0' to folder 'D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages'
Added package 'PlayniteSDK.6.16.0' to folder 'D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages'
Added package 'NUnit.3.14.0' to folder 'D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages'
Installed NUnitLite 3.14.0 from https://api.nuget.org/v3/index.json to C:\Users\runneradmin\.nuget\packages\nunitlite\3.14.0 with content hash DLiOwZ4R2IsB4G3rxcBE/pCFoE9nVuT7L0NUe7J9xxWka3rh4eK8DWZeepfacy/3HTwYDXiIMiMUYSspSbA6lA==.
Adding package 'NUnitLite.3.14.0' to folder 'D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages'
Added package 'NUnitLite.3.14.0' to folder 'D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages'
  GET https://api.nuget.org/v3/vulnerabilities/index.json
  OK https://api.nuget.org/v3/vulnerabilities/index.json 2ms
  GET https://api.nuget.org/v3-vulnerabilities/2026.07.10.06.21.10/vulnerability.base.json
  GET https://api.nuget.org/v3-vulnerabilities/2026.07.10.06.21.10/2026.07.11.18.21.16/vulnerability.update.json
  OK https://api.nuget.org/v3-vulnerabilities/2026.07.10.06.21.10/vulnerability.base.json 2ms
  OK https://api.nuget.org/v3-vulnerabilities/2026.07.10.06.21.10/2026.07.11.18.21.16/vulnerability.update.json 2ms

NuGet Config files used:
    C:\Users\runneradmin\AppData\Roaming\NuGet\NuGet.Config
    C:\Program Files (x86)\NuGet\Config\Microsoft.VisualStudio.FallbackLocation.config
    C:\Program Files (x86)\NuGet\Config\Microsoft.VisualStudio.Offline.config

Feeds used:
    https://api.nuget.org/v3/index.json
    C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\

Installed:
    3 package(s) to packages.config projects

## build log tail

MSBuild version 18.7.8+1ac568fee for .NET Framework
Build started 7/12/2026 2:56:48 AM.

Project "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln" on node 1 (Build target(s)).
ValidateSolutionConfiguration:
  Building solution configuration "Debug|Any CPU".
Project "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln" (1) is building "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.csproj" (2) on node 1 (default targets).
PrepareForBuild:
  Creating directory "bin\Debug\".
  Creating directory "obj\Debug\".
CoreCompile:
  Setting DOTNET_TieredCompilation to '0'
  C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe /noconfig /nowarn:1701,1702 /fullpaths /nostdlib+ /errorreport:prompt /warn:4 /define:DEBUG;TRACE /highentropyva+ /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\Microsoft.CSharp.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\mscorlib.dll" /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\PlayniteSDK.6.16.0\lib\net462\Playnite.SDK.dll /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\PresentationCore.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\PresentationFramework.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Core.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.DataSetExtensions.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Net.Http.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Windows.Forms.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xaml.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xml.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xml.Linq.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\WindowsBase.dll" /debug+ /debug:full /filealign:512 /optimize- /out:obj\Debug\PersonalCloudLibrarySource.dll /subsystemversion:6.00 /resource:obj\Debug\PersonalCloudLibrarySource.g.resources /target:library /utf8output /deterministic+ /langversion:7.3 Dashboard\CacheStatusService.cs Dashboard\CloudLibraryDashboardState.cs Dashboard\CloudLibraryDashboardView.xaml.cs Dashboard\CloudLibraryDashboardViewModel.cs Dashboard\CloudTransferQueueItemViewModel.cs Dashboard\CloudLibraryDashboardWindowService.cs Dashboard\CloudLibrarySidebarItem.cs Dashboard\DashboardStateStore.cs Dashboard\FriendlySourceNameProvider.cs Dashboard\LibraryStatusService.cs LibraryVerificationReport.cs LibraryVerificationService.cs LocalFileCopier.cs ManifestGenerationOptions.cs ManifestGenerationReport.cs ManifestGenerationService.cs PersonalCloudLibrarySource.GameCommands.cs PersonalCloudLibrarySource.Navigation.cs PersonalCloudLibrarySource.Transfers.cs PersonalCloudLibrarySourceClient.cs PersonalCloudLibrarySource.cs PersonalCloudLibrarySourceSettings.cs PersonalCloudLibrarySourceSettingsV3.cs PersonalCloudLibrarySourceSettingsV3ViewModel.cs PersonalCloudLibrarySourceSettingsView.xaml.cs PersonalCloudLibraryUninstallController.cs Properties\AssemblyInfo.cs RcloneFileCopier.cs RcloneInstallController.cs RcloneManifestReader.cs SafeFileWriteService.cs Services\GameCommandPolicyService.cs Services\GameCommandService.cs Services\PluginNavigationService.cs SettingsMigrationService.cs Setup\SetupDraft.cs Setup\SetupModels.cs Setup\SetupValidationService.cs Setup\SetupWizardView.xaml.cs Setup\SetupWizardViewModel.cs Setup\SetupWizardWindowService.cs Transfers\CloudTransferExecutionResult.cs Transfers\CloudTransferExecutor.cs Transfers\CloudTransferJob.cs Transfers\CloudTransferManager.cs Transfers\LocalTransferAdapter.cs Transfers\RcloneCommandBuilder.cs Transfers\RcloneProcessRunner.cs Transfers\RcloneProgressParser.cs Transfers\RcloneTransferAdapter.cs Transfers\RcloneTransferModels.cs Views\CloudGameDetailsView.xaml.cs Views\CloudGameDetailsViewModel.cs Views\CloudGameDetailsWindowService.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Dashboard\CloudLibraryDashboardView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Setup\SetupWizardView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Views\CloudGameDetailsView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\PersonalCloudLibrarySourceSettingsView.g.cs "obj\Debug\.NETFramework,Version=v4.6.2.AssemblyAttributes.cs"
  Compilation request PersonalCloudLibrarySource, PathToTool=C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe
  CommandLine = ' /noconfig'
  BuildResponseFile = '/nowarn:1701,1702 /fullpaths /nostdlib+ /errorreport:prompt /warn:4 /define:DEBUG;TRACE /highentropyva+ /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\Microsoft.CSharp.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\mscorlib.dll" /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\PlayniteSDK.6.16.0\lib\net462\Playnite.SDK.dll /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\PresentationCore.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\PresentationFramework.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Core.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.DataSetExtensions.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Net.Http.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Windows.Forms.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xaml.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xml.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xml.Linq.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\WindowsBase.dll" /debug+ /debug:full /filealign:512 /optimize- /out:obj\Debug\PersonalCloudLibrarySource.dll /subsystemversion:6.00 /resource:obj\Debug\PersonalCloudLibrarySource.g.resources /target:library /utf8output /deterministic+ /langversion:7.3 Dashboard\CacheStatusService.cs Dashboard\CloudLibraryDashboardState.cs Dashboard\CloudLibraryDashboardView.xaml.cs Dashboard\CloudLibraryDashboardViewModel.cs Dashboard\CloudTransferQueueItemViewModel.cs Dashboard\CloudLibraryDashboardWindowService.cs Dashboard\CloudLibrarySidebarItem.cs Dashboard\DashboardStateStore.cs Dashboard\FriendlySourceNameProvider.cs Dashboard\LibraryStatusService.cs LibraryVerificationReport.cs LibraryVerificationService.cs LocalFileCopier.cs ManifestGenerationOptions.cs ManifestGenerationReport.cs ManifestGenerationService.cs PersonalCloudLibrarySource.GameCommands.cs PersonalCloudLibrarySource.Navigation.cs PersonalCloudLibrarySource.Transfers.cs PersonalCloudLibrarySourceClient.cs PersonalCloudLibrarySource.cs PersonalCloudLibrarySourceSettings.cs PersonalCloudLibrarySourceSettingsV3.cs PersonalCloudLibrarySourceSettingsV3ViewModel.cs PersonalCloudLibrarySourceSettingsView.xaml.cs PersonalCloudLibraryUninstallController.cs Properties\AssemblyInfo.cs RcloneFileCopier.cs RcloneInstallController.cs RcloneManifestReader.cs SafeFileWriteService.cs Services\GameCommandPolicyService.cs Services\GameCommandService.cs Services\PluginNavigationService.cs SettingsMigrationService.cs Setup\SetupDraft.cs Setup\SetupModels.cs Setup\SetupValidationService.cs Setup\SetupWizardView.xaml.cs Setup\SetupWizardViewModel.cs Setup\SetupWizardWindowService.cs Transfers\CloudTransferExecutionResult.cs Transfers\CloudTransferExecutor.cs Transfers\CloudTransferJob.cs Transfers\CloudTransferManager.cs Transfers\LocalTransferAdapter.cs Transfers\RcloneCommandBuilder.cs Transfers\RcloneProcessRunner.cs Transfers\RcloneProgressParser.cs Transfers\RcloneTransferAdapter.cs Transfers\RcloneTransferModels.cs Views\CloudGameDetailsView.xaml.cs Views\CloudGameDetailsViewModel.cs Views\CloudGameDetailsWindowService.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Dashboard\CloudLibraryDashboardView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Setup\SetupWizardView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Views\CloudGameDetailsView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\PersonalCloudLibrarySourceSettingsView.g.cs "obj\Debug\.NETFramework,Version=v4.6.2.AssemblyAttributes.cs"'
  Attempting to create process 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\VBCSCompiler.exe' "-pipename:lk3GEFz8Fq7L5CzBTij4YT+Po32XgtYut3dBs3ahqvk"
  Setting DOTNET_TieredCompilation to '0'
  Successfully created process with process id 9204
  Attempt to open named pipe 'lk3GEFz8Fq7L5CzBTij4YT+Po32XgtYut3dBs3ahqvk'
  Attempt to connect named pipe 'lk3GEFz8Fq7L5CzBTij4YT+Po32XgtYut3dBs3ahqvk'
  Named pipe 'lk3GEFz8Fq7L5CzBTij4YT+Po32XgtYut3dBs3ahqvk' connected
  Begin writing request for PersonalCloudLibrarySource
  End writing request for PersonalCloudLibrarySource
  Begin reading response for PersonalCloudLibrarySource
  End reading response for PersonalCloudLibrarySource
D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Navigation.cs(55,35): error CS1729: 'CloudLibraryDashboardViewModel' does not contain a constructor that takes 4 arguments [D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.csproj]
  CompilerServer: server - server processed compilation - PersonalCloudLibrarySource
Done Building Project "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.csproj" (default targets) -- FAILED.
Project "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln" (1) is building "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\PersonalCloudLibrarySource.Tests.csproj" (3) on node 1 (default targets).
PrepareForBuild:
  Creating directory "bin\Debug\".
  Creating directory "obj\Debug\".
Done Building Project "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\PersonalCloudLibrarySource.Tests.csproj" (default targets) -- FAILED.
Done Building Project "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln" (Build target(s)) -- FAILED.

Build FAILED.

"D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln" (Build target) (1) ->
"D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.csproj" (default target) (2) ->
(CoreCompile target) -> 
  D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Navigation.cs(55,35): error CS1729: 'CloudLibraryDashboardViewModel' does not contain a constructor that takes 4 arguments [D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.csproj]

    0 Warning(s)
    1 Error(s)

Time Elapsed 00:00:10.51

