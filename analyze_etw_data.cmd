@echo off

set BASEDIR=%~dp0%
set CONFIGURATION=Release

pushd src\ETWLogAnalyzer
devenv ETWLogAnalyzer.csproj /Build %CONFIGURATION%
bin\%CONFIGURATION%\MusicStore.ETWLogAnalyzer /put=dotnet /etwLog=%BASEDIR%\src\MusicStore\bin\Release\netcoreapp1.0\publish\PerfViewData.etl
popd





