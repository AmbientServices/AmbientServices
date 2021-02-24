# Required environment variables $env:version, $env:nugetapikey

"`nCleaning the workspace`n---------------------------" 
Remove-Item -Recurse -Force AmbientServices.Test\TestResults -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force AmbientServices.Test.DelayedLoad\TestResults -ErrorAction SilentlyContinue

"`nSetting Version: $version`n---------------------------" 
"`ndotnet .\SetVersion.dll -- $version"
dotnet .\SetVersion.dll -- $version

"`nBuilding Binaries`n---------------------------" 
dotnet build -c Release
if (!$?) { exit 1 }

"`nRunning Tests with Coverage`n---------------------------" 
# dotnet add package coverlet.collector
# dotnet tool install -g dotnet-reportgenerator-globaltool
dotnet test AmbientServices.Test -f net5.0 --collect:"XPlat Code Coverage" --logger:"trx;LogFileName=unit.testresults.trx"
$testResult = $?
if (!$testResult) {exit 1}
# delete coverage from AmbientServices.Test.DelayedLoad dll (we don't care about coverage in test assemblies)
Remove-Item -Recurse -Force AmbientServices.Test.DelayedLoad\TestResults -ErrorAction SilentlyContinue

"`n`nCreating Nuget Package`n---------------------------" 
dotnet pack -c Release
if (!$?) { exit 1 }

"`n`nPublishing to NuGet`n---------------------------" 
dotnet nuget push AmbientServices\bin\Release\AmbientServices.$version.nupkg -k $env:nugetapikey -s https://api.nuget.org/v3/index.json
if (!$?) { exit 1 }
