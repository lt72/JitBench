# 1. ETWLogAnalyzer Documentation

<!-- TOC -->

- [1. ETWLogAnalyzer Documentation](#1-etwloganalyzer-documentation)
    - [1.1. Purpose of the tool](#11-purpose-of-the-tool)
    - [1.2. Architecture and Extensibility](#12-architecture-and-extensibility)
        - [1.2.1. Data Storage - the model](#121-data-storage---the-model)
        - [1.2.2. The Controller](#122-the-controller)
        - [1.2.3. ReportsVisitors](#123-reportsvisitors)
        - [1.2.4. Reports](#124-reports)
    - [1.3. How to use the tool with the sample app](#13-how-to-use-the-tool-with-the-sample-app)
    - [1.4. Defined Metrics](#14-defined-metrics)
        - [1.4.1. Process Lifespan](#141-process-lifespan)
        - [1.4.2. Thread Lifespan](#142-thread-lifespan)
        - [1.4.3. Time To Program Start (Custom metric for JitBench)](#143-time-to-program-start-custom-metric-for-jitbench)
        - [1.4.4. Time To Server Start (Custom metric for JitBench)](#144-time-to-server-start-custom-metric-for-jitbench)
        - [1.4.5. Time To Request Served (Custom metric for JitBench)](#145-time-to-request-served-custom-metric-for-jitbench)
        - [1.4.6. Nominal Jit Time](#146-nominal-jit-time)
        - [1.4.7. Effective Jit Time](#147-effective-jit-time)
        - [1.4.8. Available Time to Jit](#148-available-time-to-jit)
    - [1.5. Provided Implementations](#15-provided-implementations)
        - [1.5.1. Model - _ETWData_](#151-model---_etwdata_)
            - [1.5.1.1. The _ETWEventsHolder Helper_](#1511-the-_etweventsholder-helper_)
        - [1.5.2. Visitors](#152-visitors)
        - [1.5.3. Reports and understanding their significance](#153-reports-and-understanding-their-significance)
    - [1.6. Relevant documentation for TraceEvents when generating your own reports](#16-relevant-documentation-for-traceevents-when-generating-your-own-reports)

<!-- /TOC -->

## 1.1. Purpose of the tool

ETWLogAnalyzer is an extensible framework that simplifies the analysis of the impact of modifications to the jitting pipeline based off ETW logs collected from running applications.

## 1.2. Architecture and Extensibility

### 1.2.1. Data Storage - the model

The model that stores the data gathered from the ETW log must implement the [IEventModel](..\ETWLogAnalyzer\Microsoft.ETWLogAnalyzer.Abstractions\Abstractions\Model\IEventModel.cs) interface. The model provides the following:

- Information for the target test process (pid,  start and stop events, etc...).
- List of jitted methods and threads.
- An event iterator for events that happened on a given thread, in time order (this includes JIT events, as well as OS events, such as context switches and page faults).

A concrete example of the model is [ETWData](..\ETWLogAnalyzer\Microsoft.ETWLogAnalyzer.Core\ETWData\ETWData.cs).

### 1.2.2. The Controller

The [controller](..\ETWLogAnalyzer\Microsoft.ETWLogAnalyzer.Core\Controller\Controller.cs) is the component of the framework that manages who has access to the data model and how they can access it as follows:

- `RunVisitorForResult` - Reports will have to use a visitor to analyze the data provided by the model. This method will take the visitor and an iterator to the stream of events that need to be analyzed and run it for the result.
- `ProcessReports` - Given the folder of the assemblies where the reports are and the data to analyze, the controller will generate them and dump them into the specified folder.
- Serialization/Deserialization of the model will be supported in the future.

### 1.2.3. ReportsVisitors

_Report visitors_ are the basic blocks that reports will use to perform analysis on the data. The convenient part is they are small objects of their own so they can store state that's usually needed for event series analysis (e.g a specific type of event sequence that seems to be a performance bottleneck). This might have to happen at the cost of multiple passes, but adds to the extensibility of the analysis tool which is our main focus. Current running time is contained under a few tens of seconds, so performance is not currently an issue.

All visitors extend the generic class [EventVisitorBase](..\ETWLogAnalyzer\Microsoft.ETWLogAnalyzer.Abstractions\Abstractions\ReportVisitors\EventVisitorBase.cs), with the generic paraameter being the type of result expected from the calculation. Within the base class:

- `State` is a property that should be updated to `VisitorState.Error` if any problem is encountered and the report should handle accordingly, or set to `VisitorState.Done` so the controller can escape early from the iterating through the events.
- `Visit` should implement the logic of the analysis
- `AddRelevantTypes` - Most events that wil be found might not be relevant for a type of analysis. The controller will use the types added to restrict the events that use the `Visit` method only to the types added as relevant.
- `IsRelevant` - Method the controller uses to determine if an event is relevant before visiting it. Doesn't need to be overriden if the filtering is performed using the filtering by type as described in `AddRelevantTypes`, otherwise must be implemented.
- `Result` is a property of the type of the generic parameter of the visitor and caches the result of the calculation. It's only valid if `State` is not `VisitorState.Error`

An example of this is [PerceivedJitTimeVisitor](..\ETWLogAnalyzer\Microsoft.ETWLogAnalyzer.Reports\ReportVisitors\PerceivedJitTimeVisitor.cs).

### 1.2.4. Reports

All reports generated by the framework implement the [IReport](..\ETWLogAnalyzer\Microsoft.ETWLogAnalyzer.Abstractions\Abstractions\Reports\IReport.cs) interface. All it provides is the name of the report and two stub methods:

- `Analyze` is the method where reports should create visitors to the data model that's passed in and cache the results in any convenient format.
- `Persist` is the method where the cached analyzed data should be persisted in whatever manner the developer feels meets their purpose.

## 1.3. How to use the tool with the sample app

To run the sample app open an Visual Studio developer console with Admin privileges and navigate to the [scripts](../Scripts/) folder.

- First you need to run the [data collection script](../Scripts/collect_etw_data.cmd). This will compile the JitBench app using a freshly downloaded version .NET core and collect the ETW log. There isn't any need to modify the script unless you want to modify what events get collected. The script also supports disabling superfetch to collect a cold start by running is with the `clean` argument (e.g. `collect_etw_data.cmd clean`).
- Second run the [data analysis script](../Scripts/analyze_etw_data.cmd). You should modify the `OUT_DIR` variable script to point to the folder you want to store the reports to. You can modify the `WAIT` variable to false if you don't want the tool to wait for user input at the end as well.

## 1.4. Defined Metrics

### 1.4.1. Process Lifespan

The lifespan of a process is the time between the `Windows Kernel\Process\Start` and `Windows Kernel\Process\Stop` events.

![Process Lifespan](Images/Process%20Lifespan.png)

### 1.4.2. Thread Lifespan

The lifespan of a thread is the time between the `Windows Kernel\Thread\Start` and `Windows Kernel\Thread\Stop` events.

![Thread lifespan](Images/Thread%20Lifespan.png)]

### 1.4.3. Time To Program Start (Custom metric for JitBench)

The _time to program start_ is the time between the `Windows Kernel\Process\Start` and `aspnet-JitBench-MusicStore/ProgramStarted` events.

![Time To Program Start](Images/Time%20To%20Program%20Start.png)

### 1.4.4. Time To Server Start (Custom metric for JitBench)

The _time to program start_ is the time between the `Windows Kernel\Process\Start` and `aspnet-JitBench-MusicStore/ServerStarted` events.

![Time To Server Start](Images/Time%20To%20Server%20Start.png)

### 1.4.5. Time To Request Served (Custom metric for JitBench)

The _time to program start_ is the time between the `Windows Kernel\Process\Start` and the first `aspnet-JitBench-MusicStore/RequestBatchServed` events.

![Time To Request Served](Images/Time%20To%20Request%20Served.png)

### 1.4.6. Nominal Jit Time

The _nominal jit time_ of a method is the time between its `JittingStarted` and `Method/LoadVerbose`events.

![Nominal Jit Time of Method](Images/Nominal%20Jit%20Time%20Method.png)

The _nominal jit time_ of a thread is the sum of the nominal jit times of the methods jitted in the thread.

![Nominal Jit Time of Thread](Images/Nominal%20Jit%20Time%20Thread.png)


### 1.4.7. Effective Jit Time

The _effective jit time_ of a method is the time between its `Microsoft-Windows-DotNETRuntime/Method/JittingStarted` and `Method/LoadVerbose` events excluding the intervals where the jitting thread is switched out. For example if theres `N` context switche pairs between the `JittingStarted` and `LoadVerbose` then the effective time can be defined as:

![Effective Jit Time of Method](Images/Effective%20Jit%20Time%20Method.png)

It's worth noting that if there are no context switches, the effective and nominal times are equivalent.



The _effective jit time_ of a thread is the sum of the effective jit times of the methods jitted in the thread.

![Effective Jit Time of Thread](Images/Effective%20Jit%20Time%20Thread.png)

### 1.4.8. Available Time to Jit

HOLDER

## 1.5. Provided Implementations

### 1.5.1. Model - _ETWData_

HOLDER

#### 1.5.1.1. The _ETWEventsHolder Helper_

HOLDER

### 1.5.2. Visitors

- `AvailableQuantumAccumulatorVisitor`: This visitor calculates the amount of time that could've potentially been used for jitting (see [](####)) by a thread segmented by method, returning a dictionary mapping MethodUniqueIdentifier to time taken.
- `JitTimeAccumulatorVisitor`: This visitor calculates the amount of effective time that was used by a thread to jit, excluding the tsegmented by method, returning a dictionary mapping MethodUniqueIdentifier to time taken.
- `GetFirstMatchingEventVisitor`
- `PerceivedJitTimeVisitor`

### 1.5.3. Reports and understanding their significance

- `JitTimeStatistics`
- `LifetimeStatistics`
  - Per Thread
  - Per Method
- `QuantumUsageStatistics`:
  - Per Thread
  - Per Method
- `IOStatistics`: In Progress

## 1.6. Relevant documentation for TraceEvents when generating your own reports

The documents and release notes can be found in the [TraceEvent documentation](TraceEventDocs/) folder