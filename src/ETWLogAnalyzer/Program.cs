using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;


namespace MusicStore.ETWLogAnalyzer
{
    public class Program
    {
        static Program()
        {
        }

        /// <summary>
        /// Executes the analysis tool. 
        /// Gather ETW logs with '\\clrmain\tools\PerfView.exe -ClrEvents:Jit /Providers:*aspnet-JitBench-MusicStore run dotnet MusicStore.dll'
        /// from the directory where MusicStore.dll is published.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static int Main(string[] args)
        {
            if (CmdLine.Process(args) == CmdLine.Cmd.ShowHelp)
            {
                return CmdLine.Usage();
            }

            var testProcess = CmdLine.TestProcess;
            var etwLogFile  = CmdLine.EtwLog;
            
            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //
            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //
            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //

            Console.WriteLine($"Opening ETW log file '{etwLogFile}'...");

            //
            // Find process of interest, there may be multiple, and we only want to look at the child-most one.
            // 
            var testProcessData = FindTestProcessProcessStart(etwLogFile, testProcess);
            
            if (testProcessData == null)
            {
                Console.WriteLine($"...could not find {testProcess} process in ETW log file '{etwLogFile}'!");

                return 1;
            }

            Console.WriteLine("...analyzing data for {testProcess}...");

            //
            // Now re-parse the log looking for the actual data we are interested in.
            //
            var events = new ETWData.ETWEventsHolder(testProcessData.ProcessID);            
            using (var source = new TRACING.ETWTraceEventSource(etwLogFile))
            {
                var id = testProcessData.ProcessID;

                //
                // Kernel
                //
                var kernelParser = new PARSERS.KernelTraceEventParser(source);

                //
                // Kernel - Threading
                //
                kernelParser.ThreadStart += delegate (PARSERS.Kernel.ThreadTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                kernelParser.ThreadStop += delegate (PARSERS.Kernel.ThreadTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                kernelParser.ThreadDCStart += delegate (PARSERS.Kernel.ThreadTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                kernelParser.ThreadDCStop += delegate (PARSERS.Kernel.ThreadTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                kernelParser.ThreadCompCS += delegate (TRACING.EmptyTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                kernelParser.ThreadCSwitch += delegate (PARSERS.Kernel.CSwitchTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                //
                // Kernel - PMC counters
                //
                kernelParser.PerfInfoPMCSample += delegate (PARSERS.Kernel.PMCCounterProfTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                //
                // Kernel - Memory
                //
                kernelParser.VirtualMemFree += delegate (PARSERS.Kernel.VirtualAllocTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                kernelParser.MemoryHardFault += delegate (PARSERS.Kernel.MemoryHardFaultTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                kernelParser.MemoryTransitionFault += delegate (PARSERS.Kernel.MemoryPageFaultTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                ////////
                // Kernel - I/O
                //
                kernelParser.FileIOCreate += delegate (PARSERS.Kernel.FileIOCreateTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                //////kernelParser.FileIOWrite += delegate (PARSERS.Kernel.FileIOReadWriteTraceData data)
                //////{
                //////    events.DiscardOrRecord(data);
                //////};
                //////kernelParser.DiskIORead += delegate (PARSERS.Kernel.DiskIOTraceData data)
                //////{
                //////    events.DiscardOrRecord(data);
                //////};
                //////kernelParser.DiskIOWrite += delegate (PARSERS.Kernel.DiskIOTraceData data)
                //////{
                //////    events.DiscardOrRecord(data);
                //////};
                //
                // Kernel - Registry
                //
                kernelParser.RegistryCreate += delegate (PARSERS.Kernel.RegistryTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                kernelParser.RegistryOpen += delegate (PARSERS.Kernel.RegistryTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                kernelParser.RegistryClose += delegate (PARSERS.Kernel.RegistryTraceData data)
                {
                    events.DiscardOrRecord(data);
                };
                kernelParser.RegistryQuery += delegate (PARSERS.Kernel.RegistryTraceData data)
                {
                    events.DiscardOrRecord(data);
                };

                //
                // DotNET
                // 
                var clrParser = new PARSERS.ClrTraceEventParser(source);

                //
                // Runtime
                //
                clrParser.RuntimeStart += delegate (PARSERS.Clr.RuntimeInformationTraceData data) {

                    events.DiscardOrRecord(data);
                };

                clrParser.ThreadCreating += delegate (PARSERS.Clr.ThreadStartWorkTraceData data) {

                    events.DiscardOrRecord(data);
                };

                clrParser.ThreadRunning += delegate (PARSERS.Clr.ThreadStartWorkTraceData data) {

                    events.DiscardOrRecord(data);
                };

                //
                // JIT
                //

                clrParser.MethodJittingStarted += delegate (PARSERS.Clr.MethodJittingStartedTraceData data) {

                    events.DiscardOrRecord(data);
                };

                clrParser.MethodLoadVerbose += delegate (PARSERS.Clr.MethodLoadUnloadVerboseTraceData data) {
                    // this is the actual "JIT finished" event!
                    events.DiscardOrRecord(data);
                };

                //
                // Contention
                //
                clrParser.ContentionStart += delegate (PARSERS.Clr.ContentionTraceData data) {

                    events.DiscardOrRecord(data);
                };
                clrParser.ContentionStop += delegate (PARSERS.Clr.ContentionTraceData data) {

                    events.DiscardOrRecord(data);
                };

                //
                // GC
                //
                clrParser.GCStart += delegate (PARSERS.Clr.GCStartTraceData data) {

                    events.DiscardOrRecord(data);
                };
                clrParser.GCStop += delegate (PARSERS.Clr.GCEndTraceData data) {

                    events.DiscardOrRecord(data);
                };

                //
                // Loader
                //
                clrParser.LoaderModuleLoad += delegate (PARSERS.Clr.ModuleLoadUnloadTraceData data) {

                    events.DiscardOrRecord(data);
                };
                clrParser.LoaderModuleUnload += delegate (PARSERS.Clr.ModuleLoadUnloadTraceData data) {

                    events.DiscardOrRecord(data);
                };
                clrParser.LoaderAssemblyLoad += delegate (PARSERS.Clr.AssemblyLoadUnloadTraceData data) {

                    events.DiscardOrRecord(data);
                };
                clrParser.LoaderAssemblyUnload += delegate (PARSERS.Clr.AssemblyLoadUnloadTraceData data) {

                    events.DiscardOrRecord(data);
                };

                //
                // Custom instrumentation 
                //
                var eventSourceParser = new PARSERS.DynamicTraceEventParser(source);

                eventSourceParser.All += delegate (TRACING.TraceEvent data)
                {
                    var name = data.ProviderName;

                    if (name == "aspnet-JitBench-MusicStore")
                    {
                        events.DiscardOrRecord(data);
                    }
                };

                //
                // Process log
                //
                source.Process();
            }

            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //
            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //
            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //

            //
            // Generate some reports
            //

            Console.WriteLine("...Generating reports...");
            var etwData = new ETWData(testProcessData, events);

            var startupAndReqs = Environment.ExpandEnvironmentVariables(@"%TEMP%\startup_and_requests.txt");
            Console.WriteLine($"... file: {startupAndReqs}");
            new StartupAndRequests().Analyze(etwData).Persist(new ReportWriters.PlainTextWriter(startupAndReqs), true);

            var threadsSchedule = Environment.ExpandEnvironmentVariables(@"%TEMP%\threads_schedule.txt");
            Console.WriteLine($"... file: {threadsSchedule}");
            new ThreadsSchedule().Analyze(etwData).Persist(new ReportWriters.PlainTextWriter(threadsSchedule), true);

            var assembliesAndModules = Environment.ExpandEnvironmentVariables(@"%TEMP%\assemblies_and_modules.txt");
            Console.WriteLine($"... file: {assembliesAndModules}");
            new Modules().Analyze(etwData).Persist(new ReportWriters.PlainTextWriter(assembliesAndModules), true);

            var jitAndIo = Environment.ExpandEnvironmentVariables(@"%TEMP%\jit_and_io.txt");
            Console.WriteLine($"... file: {jitAndIo}");
            new JitAndIO().Analyze(etwData).Persist(new ReportWriters.PlainTextWriter(jitAndIo), true);

            Console.WriteLine("...done!");

            return 0;
        }

        private static string Normalize(string name)
        {
            return name.Replace('\\', '/');
        }

        private static PARSERS.Kernel.ProcessTraceData FindTestProcessProcessStart(string etwLogFile, string testProcess)
        {
            //
            // dotnet host may spawn more than one dotnet process. We only need to look 
            // at the child process that actually hosts MusicStore.dll.
            //
            var dotnets = new Dictionary<int, PARSERS.Kernel.ProcessTraceData>();

            using (var source = new TRACING.ETWTraceEventSource(etwLogFile))
            {
                //
                // Look through kernel events, and specifically the Process/Start events
                //
                var kernelParser = new PARSERS.KernelTraceEventParser(source);

                kernelParser.ProcessStart += delegate (PARSERS.Kernel.ProcessTraceData proc)
                {
                    if (FilterProcessByName(proc, testProcess)) return;

                    //
                    // Could be the father or child process, accumulate all instances for later analysis. 
                    // Use the pid of the parent process as the key in the lookup. 
                    //
                    dotnets.Add(proc.ProcessID, (PARSERS.Kernel.ProcessTraceData)proc.Clone());
                };

                source.Process();
            }

            //
            // The process we are interested in is the child-most one, which will have no parent
            //
            PARSERS.Kernel.ProcessTraceData testProcessData = null;

            foreach (var proc in dotnets.Values)
            {
                if (dotnets.ContainsKey(proc.ParentID) == false)
                {
                    //
                    // Found a process that is not a parent of any other process
                    //
                    testProcessData = proc; break;
                }
            }

            return testProcessData;
        }

        private static bool FilterProcessByName(TRACING.TraceEvent proc, string processUnderTest)
        {
            return proc.ProcessName != processUnderTest;
        }
    }
}
