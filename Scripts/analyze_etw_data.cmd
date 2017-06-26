@echo off
SETLOCAL EnableDelayedExpansion
set ORIGINALDIR=%CD%
set SCRIPTDIR=%~dp0
set BASEDIR=%SCRIPTDIR%..
set DOTNET=%BASEDIR%\.dotnet\dotnet.exe
set ETWAPPDIR=%BASEDIR%\ETWLogAnalyzer
set NUGET=%BASEDIR%\Tools\nuget.exe
set CONFIGURATION=Release
set APP=%ETWAPPDIR%\Microsoft.ETWLogAnalyzer.Sample\bin\%CONFIGURATION%\
set ETL=%BASEDIR%\JitBench\src\MusicStore\bin\%CONFIGURATION%\netcoreapp2.0\publish\PerfViewData.etl
set OUT_DIR=E:\Reports

cd "%ETWAPPDIR%"
echo %CD%

"%NUGET%" restore
devenv Microsoft.ETWLogAnalyzer.sln /Build %CONFIGURATION%

cd "%APP%"
Microsoft.ETWLogAnalyzer.Sample.exe /put=dotnet /etwLog=%ETL% /out-dir=%OUT_DIR%

cd %ORIGINALDIR%




