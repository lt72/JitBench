@if not defined _echo @echo off
setlocal EnableDelayedExpansion

:: ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
:: Modify only these variables as needed.
set CONFIGURATION=Release

REM These flags define what type of events get collected. If you want more information on this
REM open Perfview on the Tools folder of the project and under the help menu the Command Line Help gives the options
REM for each one of these under -ClrEvents and -KernelEvents respectively. The users guide contains information on
REM what each one collects under the Command Line Reference section.
set DOTNET_EVENTS="Default+Type"
set KERNEL_EVENTS="Verbose"
set PROVIDERS="*aspnet-JitBench-MusicStore"

REM If you modify these variables you'll have to modify MusicStore's csproj file as well to reflect the changes
set SDK_VERSION="2.1.0-preview1-006776"
set SHARED_FRAMEWORK_VERSION="2.1.0-preview2-25513-01"

:: Do not modify these variables
set ORIGINALDIR=%CD%
set SCRIPTDIR=%~dp0
set BASEDIR=%SCRIPTDIR%..
set PFUTIL=%BASEDIR%\Tools\pfutil.exe
set PERFVIEW=%BASEDIR%\Tools\PerfView.exe
set APPDIR=%BASEDIR%\JitBench\src\MusicStore\
set DOTNET=%BASEDIR%\.dotnet\dotnet.exe
set MUSICSTOREDLLDIR=%BASEDIR%\JitBench\src\MusicStore\bin\%CONFIGURATION%\netcoreapp2.0\publish

::Defaults
set InstallMode=1
set DeleteCache=1
set DisableSuperfetch=0
:: ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

:: Argument parsing
:Arg_Loop
if "%1" == "" goto ArgsDone

if /i "%1" == "-?"    goto Usage
if /i "%1" == "-h"    goto Usage
if /i "%1" == "-help" goto Usage
if /i "%1" == "Help" goto Usage

if /i "%1" == "DisableSuperfetch"	(set DisableSuperfetch=1&shift&goto Arg_Loop)
if /i "%1" == "KeepCache"			(set DeleteCache=0&shift&goto Arg_Loop)
if /i "%1" == "SkipInstall"			(set InstallMode=0&shift&goto Arg_Loop)

if not "%1" == "" (
	echo Unknown switch: "%1"
	goto Usage
)
:ArgsDone

:: ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

:: Installation and checking for executable

if %InstallMode% EQU 1 (
	cd "%BASEDIR%"
	powershell "%SCRIPTDIR%\Dotnet-Install.ps1" -SharedRuntime -InstallDir .dotnet -Channel master -Architecture x64 -Version "%SHARED_FRAMEWORK_VERSION%"
	if %ERRORLEVEL% NEQ 0 (
		echo Failed to install the requested version of the runtime
		exit /b 1
	)
	
	powershell "%SCRIPTDIR%\Dotnet-Install.ps1" -InstallDir .dotnet -Channel master -Architecture x64 -Version "%SDK_VERSION%"
	if %ERRORLEVEL% NEQ 0 (
		echo Failed to install the requested versions of the SDK
		exit /b 1
	)
)

if not exist "%DOTNET%" (
	echo Dotnet not found. Have you run the script in installation mode?
	exit /b 1
)

:: ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

:: Restore and publishing
cd "%APPDIR%"
if %DeleteCache% EQU 1 ( 
	"%DOTNET%" nuget locals --clear all
)
"%DOTNET%" restore
"%DOTNET%" publish -c %CONFIGURATION% -f netcoreapp2.0
:: ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

:: Event collection
cd "%MUSICSTOREDLLDIR%"
IF %DisableSuperfetch% EQU 1 (
	"%PFUTIL%" -prefetch 0
	"%PFUTIL%" -purge
)

"%PERFVIEW%" /NoGui /NoView /ClrEvents:%DOTNET_EVENTS% /KernelEvents:%KERNEL_EVENTS% /Providers:%PROVIDERS% /Zip:FALSE /BufferSize:2048 run "%DOTNET%" MusicStore.dll

IF %DisableSuperfetch% EQU 1 (
	"%PFUTIL%" -prefetch 1
)
:: ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

cd "%ORIGINALDIR%"
exit /b 0

:: Help and Documentation
:Usage
echo.
echo ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
echo               Music Store ETW Collection Script                 
echo ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
echo.
echo Usage:
echo        %~n0 [option1] [option2] ...
echo.
echo.
echo All arguments are optional. The following options are available:
echo.
echo     DisableSuperfetch: Disables SuperFetch to prevent image warming. EXPERIMENTAL: Need to modify runtime. Read documentation for this
echo.
echo     KeepCache: Can be used if the local nuget caches shouldn't be cleared.
echo.
echo     SkipInstall: this flag can be used if the collection script has been previously run.
echo.
echo     Help/-h/-help/-?: Display this message.
echo.
echo.
echo In case you need to modify the events collected modify the variables at the top of this script.
echo In case you need to modify the framework for a custom run please check the documentation for instructions.
echo.
:: ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~