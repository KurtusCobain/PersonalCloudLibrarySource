# User-Friendly Dashboard Branch Audit

- Result: **PASS**
- Verified at: 2026-07-12T03:24:51Z
- Branch: feature/user-friendly-dashboard
- Source commit: 24e69a69b65d42011fa35118206ac3e24d6000d1
- NuGet restore: success
- Debug build: success
- NUnitLite tests: success

## Repairs included

- Restored dashboard constructor consistency.
- Added queued rclone execution to CloudTransferExecutor.
- Registered orphaned activity and rclone tests in the projects.
- Added deterministic ordering for active and retryable transfer jobs.
- Forced a clean NuGet restore after removing incomplete package directories.
- Removed restored package contents, temporary workflows, patch scripts, markers, logs, and stale verification reports after a passing run.

## restore log tail

MSBuild auto-detection: using msbuild version '18.7.8.30822' from 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin'.
Restoring NuGet package NUnitLite.3.14.0.
Restoring NuGet package NUnit.3.14.0.
Restoring NuGet package PlayniteSDK.6.16.0.
  GET https://api.nuget.org/v3-flatcontainer/playnitesdk/6.16.0/playnitesdk.6.16.0.nupkg
  GET https://api.nuget.org/v3-flatcontainer/nunit/3.14.0/nunit.3.14.0.nupkg
  GET https://api.nuget.org/v3-flatcontainer/nunitlite/3.14.0/nunitlite.3.14.0.nupkg
  OK https://api.nuget.org/v3-flatcontainer/playnitesdk/6.16.0/playnitesdk.6.16.0.nupkg 9ms
  OK https://api.nuget.org/v3-flatcontainer/nunit/3.14.0/nunit.3.14.0.nupkg 10ms
  OK https://api.nuget.org/v3-flatcontainer/nunitlite/3.14.0/nunitlite.3.14.0.nupkg 12ms
Installed PlayniteSDK 6.16.0 from https://api.nuget.org/v3/index.json to C:\Users\runneradmin\.nuget\packages\playnitesdk\6.16.0 with content hash MYb4x1i0kzKRmyWU3p4jQyBfY/7xSo+xydFOZAnDWU6Y1qrpuXRiyczCAkXbun/AzeXc5WvrSp0Gzp42U2fNaA==.
Installed NUnit 3.14.0 from https://api.nuget.org/v3/index.json to C:\Users\runneradmin\.nuget\packages\nunit\3.14.0 with content hash R7iPwD7kbOaP3o2zldWJbWeMQAvDKD0uld27QvA3PAALl1unl7x0v2J7eGiJOYjimV/BuGT4VJmr45RjS7z4LA==.
Adding package 'NUnit.3.14.0' to folder 'D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages'
Adding package 'PlayniteSDK.6.16.0' to folder 'D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages'
Installed NUnitLite 3.14.0 from https://api.nuget.org/v3/index.json to C:\Users\runneradmin\.nuget\packages\nunitlite\3.14.0 with content hash DLiOwZ4R2IsB4G3rxcBE/pCFoE9nVuT7L0NUe7J9xxWka3rh4eK8DWZeepfacy/3HTwYDXiIMiMUYSspSbA6lA==.
Adding package 'NUnitLite.3.14.0' to folder 'D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages'
Added package 'PlayniteSDK.6.16.0' to folder 'D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages'
Added package 'NUnit.3.14.0' to folder 'D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages'
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

PrepareForBuild:
  Creating directory "bin\Debug\".
  Creating directory "obj\Debug\".
CoreCompile:
  Setting DOTNET_TieredCompilation to '0'
  C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe /noconfig /nowarn:1701,1702 /fullpaths /nostdlib+ /errorreport:prompt /warn:4 /define:DEBUG;TRACE /highentropyva+ /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\Microsoft.CSharp.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\mscorlib.dll" /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\PlayniteSDK.6.16.0\lib\net462\Playnite.SDK.dll /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\PresentationCore.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\PresentationFramework.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Core.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.DataSetExtensions.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Net.Http.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Windows.Forms.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xaml.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xml.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xml.Linq.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\WindowsBase.dll" /debug+ /debug:full /filealign:512 /optimize- /out:obj\Debug\PersonalCloudLibrarySource.dll /subsystemversion:6.00 /resource:obj\Debug\PersonalCloudLibrarySource.g.resources /target:library /utf8output /deterministic+ /langversion:7.3 Dashboard\CacheStatusService.cs Dashboard\DashboardActivityService.cs Dashboard\CloudLibraryDashboardState.cs Dashboard\CloudLibraryDashboardView.xaml.cs Dashboard\CloudLibraryDashboardViewModel.cs Dashboard\CloudTransferQueueItemViewModel.cs Dashboard\CloudLibraryDashboardWindowService.cs Dashboard\CloudLibrarySidebarItem.cs Dashboard\DashboardStateStore.cs Dashboard\FriendlySourceNameProvider.cs Dashboard\LibraryStatusService.cs LibraryVerificationReport.cs LibraryVerificationService.cs LocalFileCopier.cs ManifestGenerationOptions.cs ManifestGenerationReport.cs ManifestGenerationService.cs PersonalCloudLibrarySource.GameCommands.cs PersonalCloudLibrarySource.Navigation.cs PersonalCloudLibrarySource.Transfers.cs PersonalCloudLibrarySourceClient.cs PersonalCloudLibrarySource.cs PersonalCloudLibrarySourceSettings.cs PersonalCloudLibrarySourceSettingsV3.cs PersonalCloudLibrarySourceSettingsV3ViewModel.cs PersonalCloudLibrarySourceSettingsView.xaml.cs PersonalCloudLibraryUninstallController.cs Properties\AssemblyInfo.cs RcloneFileCopier.cs RcloneInstallController.cs RcloneManifestReader.cs SafeFileWriteService.cs Services\GameCommandPolicyService.cs Services\GameCommandService.cs Services\PluginNavigationService.cs SettingsMigrationService.cs Setup\SetupDraft.cs Setup\SetupModels.cs Setup\SetupValidationService.cs Setup\SetupWizardView.xaml.cs Setup\SetupWizardViewModel.cs Setup\SetupWizardWindowService.cs Transfers\CloudTransferExecutionResult.cs Transfers\CloudTransferExecutor.cs Transfers\CloudTransferJob.cs Transfers\CloudTransferManager.cs Transfers\LocalTransferAdapter.cs Transfers\RcloneCommandBuilder.cs Transfers\RcloneProcessRunner.cs Transfers\RcloneProgressParser.cs Transfers\RcloneTransferAdapter.cs Transfers\RcloneTransferModels.cs Transfers\TransferActivityTracker.cs Views\CloudGameDetailsView.xaml.cs Views\CloudGameDetailsViewModel.cs Views\CloudGameDetailsWindowService.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Dashboard\CloudLibraryDashboardView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Setup\SetupWizardView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Views\CloudGameDetailsView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\PersonalCloudLibrarySourceSettingsView.g.cs "obj\Debug\.NETFramework,Version=v4.6.2.AssemblyAttributes.cs"
  Compilation request PersonalCloudLibrarySource, PathToTool=C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe
  CommandLine = ' /noconfig'
  BuildResponseFile = '/nowarn:1701,1702 /fullpaths /nostdlib+ /errorreport:prompt /warn:4 /define:DEBUG;TRACE /highentropyva+ /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\Microsoft.CSharp.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\mscorlib.dll" /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\PlayniteSDK.6.16.0\lib\net462\Playnite.SDK.dll /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\PresentationCore.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\PresentationFramework.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Core.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.DataSetExtensions.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Data.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Net.Http.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Windows.Forms.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xaml.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xml.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Xml.Linq.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\WindowsBase.dll" /debug+ /debug:full /filealign:512 /optimize- /out:obj\Debug\PersonalCloudLibrarySource.dll /subsystemversion:6.00 /resource:obj\Debug\PersonalCloudLibrarySource.g.resources /target:library /utf8output /deterministic+ /langversion:7.3 Dashboard\CacheStatusService.cs Dashboard\DashboardActivityService.cs Dashboard\CloudLibraryDashboardState.cs Dashboard\CloudLibraryDashboardView.xaml.cs Dashboard\CloudLibraryDashboardViewModel.cs Dashboard\CloudTransferQueueItemViewModel.cs Dashboard\CloudLibraryDashboardWindowService.cs Dashboard\CloudLibrarySidebarItem.cs Dashboard\DashboardStateStore.cs Dashboard\FriendlySourceNameProvider.cs Dashboard\LibraryStatusService.cs LibraryVerificationReport.cs LibraryVerificationService.cs LocalFileCopier.cs ManifestGenerationOptions.cs ManifestGenerationReport.cs ManifestGenerationService.cs PersonalCloudLibrarySource.GameCommands.cs PersonalCloudLibrarySource.Navigation.cs PersonalCloudLibrarySource.Transfers.cs PersonalCloudLibrarySourceClient.cs PersonalCloudLibrarySource.cs PersonalCloudLibrarySourceSettings.cs PersonalCloudLibrarySourceSettingsV3.cs PersonalCloudLibrarySourceSettingsV3ViewModel.cs PersonalCloudLibrarySourceSettingsView.xaml.cs PersonalCloudLibraryUninstallController.cs Properties\AssemblyInfo.cs RcloneFileCopier.cs RcloneInstallController.cs RcloneManifestReader.cs SafeFileWriteService.cs Services\GameCommandPolicyService.cs Services\GameCommandService.cs Services\PluginNavigationService.cs SettingsMigrationService.cs Setup\SetupDraft.cs Setup\SetupModels.cs Setup\SetupValidationService.cs Setup\SetupWizardView.xaml.cs Setup\SetupWizardViewModel.cs Setup\SetupWizardWindowService.cs Transfers\CloudTransferExecutionResult.cs Transfers\CloudTransferExecutor.cs Transfers\CloudTransferJob.cs Transfers\CloudTransferManager.cs Transfers\LocalTransferAdapter.cs Transfers\RcloneCommandBuilder.cs Transfers\RcloneProcessRunner.cs Transfers\RcloneProgressParser.cs Transfers\RcloneTransferAdapter.cs Transfers\RcloneTransferModels.cs Transfers\TransferActivityTracker.cs Views\CloudGameDetailsView.xaml.cs Views\CloudGameDetailsViewModel.cs Views\CloudGameDetailsWindowService.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Dashboard\CloudLibraryDashboardView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Setup\SetupWizardView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Views\CloudGameDetailsView.g.cs D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\PersonalCloudLibrarySourceSettingsView.g.cs "obj\Debug\.NETFramework,Version=v4.6.2.AssemblyAttributes.cs"'
  Attempting to create process 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\VBCSCompiler.exe' "-pipename:lk3GEFz8Fq7L5CzBTij4YT+Po32XgtYut3dBs3ahqvk"
  Setting DOTNET_TieredCompilation to '0'
  Successfully created process with process id 9576
  Attempt to open named pipe 'lk3GEFz8Fq7L5CzBTij4YT+Po32XgtYut3dBs3ahqvk'
  Attempt to connect named pipe 'lk3GEFz8Fq7L5CzBTij4YT+Po32XgtYut3dBs3ahqvk'
  Named pipe 'lk3GEFz8Fq7L5CzBTij4YT+Po32XgtYut3dBs3ahqvk' connected
  Begin writing request for PersonalCloudLibrarySource
  End writing request for PersonalCloudLibrarySource
  Begin reading response for PersonalCloudLibrarySource
  End reading response for PersonalCloudLibrarySource
  CompilerServer: server - server processed compilation - PersonalCloudLibrarySource
_CopyFilesMarkedCopyLocal:
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\PlayniteSDK.6.16.0\lib\net462\Playnite.SDK.dll" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\Playnite.SDK.dll".
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\PlayniteSDK.6.16.0\lib\net462\Playnite.SDK.xml" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\Playnite.SDK.xml".
  Creating "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Personal.8DF925D2.Up2Date" because "AlwaysCreate" was specified.
  Touching "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\Personal.8DF925D2.Up2Date".
_CopyOutOfDateSourceItemsToOutputDirectory:
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\extension.yaml" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\extension.yaml".
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\icon.png" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\icon.png".
  Creating directory "bin\Debug\Localization".
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\Localization\en_US.xaml" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\Localization\en_US.xaml".
CopyFilesToOutputDirectory:
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\PersonalCloudLibrarySource.dll" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\PersonalCloudLibrarySource.dll".
  PersonalCloudLibrarySource -> D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\PersonalCloudLibrarySource.dll
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\obj\Debug\PersonalCloudLibrarySource.pdb" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\PersonalCloudLibrarySource.pdb".
Done Building Project "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.csproj" (default targets).
Project "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln" (1) is building "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\PersonalCloudLibrarySource.Tests.csproj" (3) on node 1 (default targets).
PrepareForBuild:
  Creating directory "bin\Debug\".
  Creating directory "obj\Debug\".
CoreCompile:
  Setting DOTNET_TieredCompilation to '0'
  C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe /noconfig /nowarn:1701,1702 /fullpaths /nostdlib+ /platform:anycpu32bitpreferred /warn:4 /define:DEBUG;TRACE /highentropyva+ /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\mscorlib.dll" /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\NUnit.3.14.0\lib\net45\nunit.framework.dll /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\NUnitLite.3.14.0\lib\net45\nunitlite.dll /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\PersonalCloudLibrarySource.dll /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\PlayniteSDK.6.16.0\lib\net462\Playnite.SDK.dll /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Core.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.dll" /debug+ /debug:full /filealign:512 /optimize- /out:obj\Debug\PersonalCloudLibrarySource.Tests.exe /subsystemversion:6.00 /target:exe /utf8output /deterministic+ /langversion:7.3 Dashboard\FriendlySourceNameProviderTests.cs Dashboard\DashboardActivityServiceTests.cs Dashboard\LibraryStatusServiceTests.cs Program.cs Services\GameCommandPolicyServiceTests.cs Services\GameCommandServiceTests.cs Services\PluginNavigationServiceTests.cs SettingsMigrationServiceTests.cs Setup\SetupWizardViewModelTests.cs Transfers\CloudTransferExecutorTests.cs Transfers\CloudTransferManagerTests.cs Transfers\CloudTransferQueueItemViewModelTests.cs Transfers\LocalTransferAdapterTests.cs Transfers\RcloneCommandBuilderTests.cs Transfers\RcloneProgressParserTests.cs Transfers\RcloneTransferAdapterTests.cs Transfers\TransferActivityTrackerTests.cs "obj\Debug\.NETFramework,Version=v4.6.2.AssemblyAttributes.cs"
  Compilation request PersonalCloudLibrarySource.Tests, PathToTool=C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe
  CommandLine = ' /noconfig'
  BuildResponseFile = '/nowarn:1701,1702 /fullpaths /nostdlib+ /platform:anycpu32bitpreferred /warn:4 /define:DEBUG;TRACE /highentropyva+ /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\mscorlib.dll" /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\NUnit.3.14.0\lib\net45\nunit.framework.dll /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\NUnitLite.3.14.0\lib\net45\nunitlite.dll /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\PersonalCloudLibrarySource.dll /reference:D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\PlayniteSDK.6.16.0\lib\net462\Playnite.SDK.dll /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.Core.dll" /reference:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\System.dll" /debug+ /debug:full /filealign:512 /optimize- /out:obj\Debug\PersonalCloudLibrarySource.Tests.exe /subsystemversion:6.00 /target:exe /utf8output /deterministic+ /langversion:7.3 Dashboard\FriendlySourceNameProviderTests.cs Dashboard\DashboardActivityServiceTests.cs Dashboard\LibraryStatusServiceTests.cs Program.cs Services\GameCommandPolicyServiceTests.cs Services\GameCommandServiceTests.cs Services\PluginNavigationServiceTests.cs SettingsMigrationServiceTests.cs Setup\SetupWizardViewModelTests.cs Transfers\CloudTransferExecutorTests.cs Transfers\CloudTransferManagerTests.cs Transfers\CloudTransferQueueItemViewModelTests.cs Transfers\LocalTransferAdapterTests.cs Transfers\RcloneCommandBuilderTests.cs Transfers\RcloneProgressParserTests.cs Transfers\RcloneTransferAdapterTests.cs Transfers\TransferActivityTrackerTests.cs "obj\Debug\.NETFramework,Version=v4.6.2.AssemblyAttributes.cs"'
  Attempt to open named pipe 'lk3GEFz8Fq7L5CzBTij4YT+Po32XgtYut3dBs3ahqvk'
  Attempt to connect named pipe 'lk3GEFz8Fq7L5CzBTij4YT+Po32XgtYut3dBs3ahqvk'
  Named pipe 'lk3GEFz8Fq7L5CzBTij4YT+Po32XgtYut3dBs3ahqvk' connected
  Begin writing request for PersonalCloudLibrarySource.Tests
  End writing request for PersonalCloudLibrarySource.Tests
  Begin reading response for PersonalCloudLibrarySource.Tests
  End reading response for PersonalCloudLibrarySource.Tests
  CompilerServer: server - server processed compilation - PersonalCloudLibrarySource.Tests
_CopyFilesMarkedCopyLocal:
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\PersonalCloudLibrarySource.dll" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\PersonalCloudLibrarySource.dll".
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\NUnit.3.14.0\lib\net45\nunit.framework.dll" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\nunit.framework.dll".
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\PlayniteSDK.6.16.0\lib\net462\Playnite.SDK.dll" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\Playnite.SDK.dll".
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\NUnitLite.3.14.0\lib\net45\nunitlite.dll" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\nunitlite.dll".
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\bin\Debug\PersonalCloudLibrarySource.pdb" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\PersonalCloudLibrarySource.pdb".
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\NUnit.3.14.0\lib\net45\nunit.framework.xml" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\nunit.framework.xml".
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\packages\PlayniteSDK.6.16.0\lib\net462\Playnite.SDK.xml" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\Playnite.SDK.xml".
  Creating "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\obj\Debug\Personal.3ECEF045.Up2Date" because "AlwaysCreate" was specified.
  Touching "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\obj\Debug\Personal.3ECEF045.Up2Date".
_CopyOutOfDateSourceItemsToOutputDirectory:
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\extension.yaml" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\extension.yaml".
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\icon.png" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\icon.png".
  Creating directory "bin\Debug\Localization".
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\Localization\en_US.xaml" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\Localization\en_US.xaml".
CopyFilesToOutputDirectory:
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\obj\Debug\PersonalCloudLibrarySource.Tests.exe" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\PersonalCloudLibrarySource.Tests.exe".
  PersonalCloudLibrarySource.Tests -> D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\PersonalCloudLibrarySource.Tests.exe
  Copying file from "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\obj\Debug\PersonalCloudLibrarySource.Tests.pdb" to "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\bin\Debug\PersonalCloudLibrarySource.Tests.pdb".
Done Building Project "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.Tests\PersonalCloudLibrarySource.Tests.csproj" (default targets).
Done Building Project "D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource\PersonalCloudLibrarySource.sln" (Build target(s)).

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:08.75

## tests log tail

Runtime Environment
   OS Version: Microsoft Windows NT 10.0.26100
  CLR Version: 4.0.30319.42000

Test Files
    D:/a/PersonalCloudLibrarySource/PersonalCloudLibrarySource/PersonalCloudLibrarySource.Tests/bin/Debug/PersonalCloudLibrarySource.Tests.exe

Test Discovery
  Start time: 2026-07-12 03:24:50Z
    End time: 2026-07-12 03:24:50Z
    Duration: 0.058 seconds

Run Settings
    Number of Test Workers: 4
    Work Directory: D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource
    Internal Trace: Off

Test Run Summary
  Overall result: Passed
  Test Count: 70, Passed: 70, Failed: 0, Warnings: 0, Inconclusive: 0, Skipped: 0
  Start time: 2026-07-12 03:24:50Z
    End time: 2026-07-12 03:24:51Z
    Duration: 0.686 seconds

Results (nunit3) saved as D:\a\PersonalCloudLibrarySource\PersonalCloudLibrarySource\TestResult.xml

