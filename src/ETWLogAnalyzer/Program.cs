using System;
using System.Collections.Generic;
using System.IO;

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
            //
            // Parse command line.
            //
            if (args.Length > 0)
            {
                if (CmdLine.Process(args) == CmdLine.Cmd.ShowHelp)
                {
                    return CmdLine.Usage();
                }
            }

            var etwLogFile = CmdLine.Arguments[CmdLine.EtwLogSwitch].Value;

            if (File.Exists(etwLogFile) == false)
            {
                throw new ArgumentException($"EWT Log File {etwLogFile} does not exist.");
            }

            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //
            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //
            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //

            Console.WriteLine($"Opening ETW log file '{etwLogFile}'...");

            //
            // Find process of interest, there may be multiple, and we only want to look at the child-most one.
            // 
            PARSERS.Kernel.ProcessTraceData put = FindDotnetProcessStart(etwLogFile);
            
            if (put == null)
            {
                Console.WriteLine($"...could not find dotnet.exe process in ETW log file '{etwLogFile}'!");
                return 1;
            }
            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //
            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //
            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //

            Console.WriteLine("...analyzing data...");

            //
            // Now re-parse the log looking for the actual data we are interested in.
            //
            var events = new ETWData.ETWEventsHolder(put.ProcessID);            
            using (var source = new TRACING.ETWTraceEventSource(etwLogFile))
            {
                var id = put.ProcessID;

                //
                // Kernel Events
                //
                PARSERS.KernelTraceEventParser kernelParser = source.Kernel;

                //
                // Kernel - Threading
                //

                // Using only thread start and thread stop events. ThreadDC callbacks can be used 
                // for permanent events if needed, but current collection methods only analyze transient
                // processes, so it's unnecessary to consider these now.
                kernelParser.ThreadStart += delegate (PARSERS.Kernel.ThreadTraceData data)
                {
                    events.DiscardOrRecord(data);
                };

                kernelParser.ThreadStop += delegate (PARSERS.Kernel.ThreadTraceData data)
                {
                    events.DiscardOrRecord(data);
                };

                kernelParser.ThreadCSwitch += delegate (PARSERS.Kernel.CSwitchTraceData data)
                {
                    events.DiscardOrRecord(data);
                };

                kernelParser.DispatcherReadyThread += delegate (PARSERS.Kernel.DispatcherReadyThreadTraceData data)
                {
                    events.DiscardOrRecord(data);
                };

                //
                // Kernel - I/O
                //

                // File API's are ignored for now. We care about blocking I/O (where non-memory reads are necessary).
                kernelParser.AddCallbackForEvents(delegate (PARSERS.Kernel.DiskIOTraceData data)
                {
                    events.DiscardOrRecord(data);
                });

                kernelParser.AddCallbackForEvents(delegate (PARSERS.Kernel.DiskIOInitTraceData data)
                {
                    events.DiscardOrRecord(data);
                });


                //
                // DotNET Events
                //
                PARSERS.ClrTraceEventParser clrParser = source.Clr;
                
                //
                // JIT Start and Stop
                //

                clrParser.MethodJittingStarted += delegate (PARSERS.Clr.MethodJittingStartedTraceData data)
                {
                    events.DiscardOrRecord(data);
                };

                clrParser.MethodLoadVerbose += delegate (PARSERS.Clr.MethodLoadUnloadVerboseTraceData data)
                {
                    // This is the actual "JIT finished" event!
                    events.DiscardOrRecord(data);
                };

                //
                // Loader
                //
                clrParser.LoaderModuleLoad += delegate (PARSERS.Clr.ModuleLoadUnloadTraceData data)
                {
                    events.DiscardOrRecord(data);
                };

                clrParser.LoaderModuleUnload += delegate (PARSERS.Clr.ModuleLoadUnloadTraceData data)
                {
                    events.DiscardOrRecord(data);
                };

                clrParser.LoaderAssemblyLoad += delegate (PARSERS.Clr.AssemblyLoadUnloadTraceData data)
                {
                    events.DiscardOrRecord(data);
                };

                clrParser.LoaderAssemblyUnload += delegate (PARSERS.Clr.AssemblyLoadUnloadTraceData data)
                {
                    events.DiscardOrRecord(data);
                };

                //
                // Custom instrumentation 
                //
                PARSERS.DynamicTraceEventParser eventSourceParser = source.Dynamic;

                eventSourceParser.All += delegate (TRACING.TraceEvent data)
                {
                    var name = data.ProviderName;

                    if (name == "aspnet-JitBench-MusicStore")
                    {
                        var a = data.GetType();
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
            var etwData = new ETWData(put, events);

            Console.WriteLine("...done!");

            return 0;
        }

        private static string Normalize(string name)
        {
            return name.Replace('\\', '/');
        }

        private static PARSERS.Kernel.ProcessTraceData FindDotnetProcessStart(string etwLogFile)
        {
            //
            // dotnet host may spawn more than one dotnet process. We only need to look 
            // at the child process that actually hosts MusicStore.dll.
            //

            var processUnderTest = CmdLine.Arguments[CmdLine.PUTSwitch].Value;

            var dotnets = new Dictionary<int, PARSERS.Kernel.ProcessTraceData>();
            using (var source = new TRACING.ETWTraceEventSource(etwLogFile))
            {
                //
                // Look through kernel events, and specifically the Process/Start events
                //
                var kernelParser = new PARSERS.KernelTraceEventParser(source);

                kernelParser.ProcessStart += delegate (PARSERS.Kernel.ProcessTraceData proc)
                {
                    if (FilterProcessByName(proc, processUnderTest)) return;

                    //
                    // Could be the father or child process, accumulate all instances for later analysis. 
                    // Use the pid of the parent process as the key in the lookup. 
                    //
                    dotnets.Add(proc.ParentID, (PARSERS.Kernel.ProcessTraceData)proc.Clone());
                };

                source.Process();
            }

            //
            // The process we are interested in is the last child. 
            // It will have no parent
            //
            PARSERS.Kernel.ProcessTraceData put = null;
            foreach (var proc in dotnets.Values)
            {
                if (!dotnets.ContainsKey(proc.ProcessID))
                {
                    //
                    // Found a process that is not a parent of any other process
                    //
                    put = proc; break;
                }
            }

            return put;
        }

        private static bool FilterProcessByName(TRACING.TraceEvent proc, string processUnderTest)
        {
            return proc.ProcessName != processUnderTest;
        }
    }
}
