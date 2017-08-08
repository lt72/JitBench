@if not defined _echo @echo off
SETLOCAL EnableDelayedExpansion

:: ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
:: Do not modify these variables
set ORIGINALDIR=%CD%
set SCRIPTDIR=%~dp0
set BASEDIR=%SCRIPTDIR%..
set DOTNET=%BASEDIR%\.dotnet\dotnet.exe
set ETWAPPDIR=%BASEDIR%\ETWLogAnalyzer
set NUGET=%BASEDIR%\Tools\nuget.exe
:: Modify only these variables as needed.
set CONFIGURATION=Release
set APP=%ETWAPPDIR%\Microsoft.ETWLogAnalyzer.Sample\bin\%CONFIGURATION%\
set REPORTS=%ETWAPPDIR%\Microsoft.ETWLogAnalyzer.Reports\bin\%CONFIGURATION%\
set ETL_FOLDER=E:\Perfview_Backups
set TARGET=dotnet
set OUT_DIR=E:\Reports
set WAIT=false
:: ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

cd "%ETWAPPDIR%"
"%NUGET%" restore
devenv Microsoft.ETWLogAnalyzer.sln /Clean %CONFIGURATION%
devenv Microsoft.ETWLogAnalyzer.sln /Build %CONFIGURATION%
cd "%APP%"

for %%F in (%ETL_FOLDER%\*.etl) do (
	mkdir "%OUT_DIR%\%%~nF"
	Microsoft.ETWLogAnalyzer.Sample.exe /reportGenerators=%REPORTS% /target=%TARGET% /etwLog=%%F /out-dir=%OUT_DIR%\%%~nF /wait=%WAIT%
)

cd %ORIGINALDIR%





