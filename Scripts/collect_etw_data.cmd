@echo off
SETLOCAL EnableDelayedExpansion
REM [-ClrEvents:Default]                 A comma separated list of .NET CLR events to turn on.  See Users guide for
REM                                      details.  Legal values: None, GC, GCHandle, Binder, Loader, Jit, NGen,
REM                                      StartEnumeration, StopEnumeration, Security, AppDomainResourceManagement,
REM                                      JitTracing, Interop, Contention, Exception, Threading,
REM                                      JittedMethodILToNativeMap, OverrideAndSuppressNGenEvents, SupressNGen, Type,
REM                                      GCHeapDump, GCSampledObjectAllocationHigh, GCHeapSurvivalAndMovement,
REM                                      HeapCollect, GCHeapAndTypeNames, GCHeapSnapshot,
REM                                      GCSampledObjectAllocationLow, GCAllObjectAllocation, PerfTrack, Stack,
REM                                      ThreadTransfer, Debugger, Monitoring, Codesymbols, Default, All.
REM [-KernelEvents:Default]              A comma separated list of windows OS kernel events to turn on.  See Users guide
REM                                      for details.  Legal values: None, Process, Thread, ImageLoad, ProcessCounters,
REM                                      ContextSwitch, DeferedProcedureCalls, Interrupt, SystemCall, DiskIO,
REM                                      kFileIO, DiskIOInit, Dispatcher, Memory, MemoryHardFaults, VirtualAlloc,
REM                                      VAMap, NetworkTCPIP, Registry, AdvancedLocalProcedureCalls, SplitIO, Handle,
REM                                      Driver, OS, Profile, Default, ThreadTime, FileIO, FileIOInit, Verbose, All,
REM                                      IOQueue, ThreadPriority, ReferenceSet, PMCProfile.

set ORIGINALDIR=%CD%
set SCRIPTDIR=%~dp0
set BASEDIR=%SCRIPTDIR%..
set PFUTIL=%BASEDIR%\Tools\pfutil.exe
set PERFVIEW=%BASEDIR%\Tools\PerfView.exe
set APPDIR=%BASEDIR%\JitBench\src\MusicStore\
set DOTNET=%BASEDIR%\.dotnet\dotnet.exe
set CONFIGURATION=Release
set ETLFOLDER=%BASEDIR%\JitBench\src\MusicStore\bin\%CONFIGURATION%\netcoreapp2.0\publish

REM Default = GC | Type | GCHeapSurvivalAndMovement | Binder | Loader | Jit | NGen | SupressNGen | StopEnumeration | Security | AppDomainResourceManagement | Exception | Threading | Contention | Stack | JittedMethodILToNativeMap | ThreadTransfer 
set DOTNET_EVENTS="Default+Type"

REM ThreadTime = Default | ContextSwitch | Dispatcher
REM Default = DiskIO | DiskFileIO | DiskIOInit | ImageLoad | MemoryHardFaults | NetworkTCPIP | Process | ProcessCounters | Profile | Thread
REM Verbose = Default | ContextSwitch | DiskIOInit | Dispatcher | FileIO | FileIOInit | MemoryPageFaults | Registry | VirtualAlloc  
REM set KERNEL_EVENTS="Default+Process+Thread+ImageLoad+ProcessCounters+ContextSwitch+DeferedProcedureCalls+SystemCall+DiskIO+DiskIOInit+Dispatcher+Memory+MemoryHardFaults+VirtualAlloc+VAMap+Registry+AdvancedLocalProcedureCalls+SplitIO+Handle+OS+ThreadTime+FileIO+FileIOInit+IOQueue+ThreadPriority+ReferenceSet"
set KERNEL_EVENTS="Verbose"


cd "%BASEDIR%"
powershell "%SCRIPTDIR%\Dotnet-Install.ps1" -SharedRuntime -InstallDir .dotnet -Channel master -Architecture x64 -Version 2.1.0-preview2-25513-01
powershell "%SCRIPTDIR%\Dotnet-Install.ps1" -InstallDir .dotnet -Channel master -Architecture x64 -Version 2.1.0-preview1-006776

cd "%APPDIR%"
"%DOTNET%" nuget locals --clear all
"%DOTNET%" restore
"%DOTNET%" publish -c %CONFIGURATION% -f netcoreapp2.0

IF "%1"=="clean" (
	"%PFUTIL%" -prefetch 0
	"%PFUTIL%" -purge
)

cd "%ETLFOLDER%"
"%PERFVIEW%" /NoGui /NoView /ClrEvents:%DOTNET_EVENTS% /KernelEvents:%KERNEL_EVENTS% /Providers:*aspnet-JitBench-MusicStore /Zip:FALSE /BufferSize:2048 run "%DOTNET%" MusicStore.dll

IF "%1"=="clean" (
	"%PFUTIL%" -prefetch 1
)

cd "%ORIGINALDIR%"