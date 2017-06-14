@echo off

set BASEDIR=%~dp0%
set CONFIGURATION=Release

powershell wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -O nuget.exe
nuget restore

pushd src\ETWLogAnalyzer
devenv ETWLogAnalyzer.csproj /Build %CONFIGURATION%
bin\%CONFIGURATION%\MusicStore.ETWLogAnalyzer /put=dotnet /etwLog=%BASEDIR%\src\MusicStore\bin\Release\netcoreapp2.0\publish\PerfViewData.etl /out-dir=E:/Reports
popd





