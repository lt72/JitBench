@echo off

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

set BASEDIR=%~dp0%
set CONFIGURATION=Release

set DOTNET_EVENTS="Default+Binder+Loader+Jit+Threading+Type"
set KERNEL_EVENTS="Default+Process+Thread+ImageLoad+ContextSwitch+DiskIO+DiskIOInit+SplitIO+ThreadTime+FileIO+FileIOInit"
REM set KERNEL_EVENTS="Default+Process+Thread+ImageLoad+ProcessCounters+ContextSwitch+DeferedProcedureCalls+SystemCall+DiskIO+DiskIOInit+Dispatcher+Memory+MemoryHardFaults+VirtualAlloc+VAMap+Registry+AdvancedLocalProcedureCalls+SplitIO+Handle+OS+ThreadTime+FileIO+FileIOInit+IOQueue+ThreadPriority"


pushd src\MusicStore
dotnet publish -c Release -f netcoreapp10
pushd bin\Release\netcoreapp1.0\publish
start /k \\clrmain\tools\PerfView.exe /NoGui /NoView /ClrEvents:%DOTNET_EVENTS% /KernelEvents:%KERNEL_EVENTS% /Providers:*aspnet-JitBench-MusicStore -Zip:FALSE run dotnet MusicStore.dll
popd
popd

