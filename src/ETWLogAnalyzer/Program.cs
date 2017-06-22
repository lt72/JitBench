using System;
using System.Collections.Generic;
using System.IO;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using MusicStore.ETWLogAnalyzer.Reports;

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
            // Parse command line.

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

            Console.WriteLine($"Opening ETW log file '{etwLogFile}'...");
            
            // Find process of interest, there may be multiple, and we only want to look at the child-most one.

            PARSERS.Kernel.ProcessTraceData put = FindChildmostPutStart(etwLogFile);
            
            if (put == null)
            {
                Console.WriteLine($"...could not find dotnet.exe process in ETW log file '{etwLogFile}'!");
                return 1;
            }
            
            // ~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~o~~~ //

            Console.WriteLine("...analyzing data...");

            ETWData data = GenerateModel(put);
            
            // Generate some reports

            Console.WriteLine("...Generating reports...");

            GenerateReports(data);

            Console.WriteLine("...done!");

            return 0;
        }

        private static void GenerateReports(ETWData etwData)
        {
            Controller.RegisterReports(new List<Type> {
                typeof(ThreadStatistics)
            });

            Controller.ProcessReports(CmdLine.Arguments[CmdLine.OutputPathSwitch].Value, etwData);
        }

        private static ETWData GenerateModel(PARSERS.Kernel.ProcessTraceData put)
        {
            var events = new Helpers.ETWEventsHolder(put.ProcessID);
            using (var source = new TRACING.ETWTraceEventSource(CmdLine.Arguments[CmdLine.EtwLogSwitch].Value))
            {
                // Kernel events
                
                PARSERS.KernelTraceEventParser kernelParser = source.Kernel;
                
                // Threading
                
                // Using only thread start and thread stop events. DC events are used to analyze permanent processes
                // but we are only interested in out process which whould be transient.
                kernelParser.ThreadStart += delegate (PARSERS.Kernel.ThreadTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                kernelParser.ThreadStop += delegate (PARSERS.Kernel.ThreadTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                kernelParser.ThreadCSwitch += delegate (PARSERS.Kernel.CSwitchTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                kernelParser.DispatcherReadyThread += delegate (PARSERS.Kernel.DispatcherReadyThreadTraceData data)
                {
                    events.StoreIfRelevant(data);
                };
                
                // I/O

                // File API's are ignored for now. We care about blocking I/O (where non-memory reads are necessary).
                kernelParser.AddCallbackForEvents(delegate (PARSERS.Kernel.DiskIOTraceData data)
                {
                    events.StoreIfRelevant(data);
                });

                kernelParser.AddCallbackForEvents(delegate (PARSERS.Kernel.DiskIOInitTraceData data)
                {
                    events.StoreIfRelevant(data);
                });
                                
                // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
                
                // CLR events

                PARSERS.ClrTraceEventParser clrParser = source.Clr;
                
                // Thread creating

                clrParser.ThreadCreating += delegate (PARSERS.Clr.ThreadStartWorkTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                // JIT Start and Stop
                
                clrParser.MethodJittingStarted += delegate (PARSERS.Clr.MethodJittingStartedTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                clrParser.MethodLoadVerbose += delegate (PARSERS.Clr.MethodLoadUnloadVerboseTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                // Loader - Assemblies and modules

                clrParser.LoaderModuleLoad += delegate (PARSERS.Clr.ModuleLoadUnloadTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                clrParser.LoaderModuleUnload += delegate (PARSERS.Clr.ModuleLoadUnloadTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                clrParser.LoaderAssemblyLoad += delegate (PARSERS.Clr.AssemblyLoadUnloadTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                clrParser.LoaderAssemblyUnload += delegate (PARSERS.Clr.AssemblyLoadUnloadTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                // Custom instrumentation 

                PARSERS.DynamicTraceEventParser eventSourceParser = source.Dynamic;

                eventSourceParser.All += delegate (TRACING.TraceEvent data)
                {
                    var name = data.ProviderName;

                    if (name == "aspnet-JitBench-MusicStore")
                    {
                        events.StoreIfRelevant(data);
                    }
                };
                
                source.Process();
            }

            return new ETWData(put, events);
        }

        // Helpers

        private static string Normalize(string name)
        {
            return name.Replace('\\', '/');
        }

        private static PARSERS.Kernel.ProcessTraceData FindChildmostPutStart(string etwLogFile)
        {
            //
            // Host may spawn more than one dotnet process. We only need to look 
            // at the child process that actually hosts MusicStore.dll.
            //

            var processUnderTest = CmdLine.Arguments[CmdLine.PUTSwitch].Value;

            var processes = new Dictionary<int, PARSERS.Kernel.ProcessTraceData>();
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
                    processes.Add(proc.ParentID, (PARSERS.Kernel.ProcessTraceData)proc.Clone());
                };

                source.Process();
            }

            //
            // The process we are interested in is the last child. 
            // It will have no parent
            //
            PARSERS.Kernel.ProcessTraceData put = null;
            foreach (var proc in processes.Values)
            {
                if (!processes.ContainsKey(proc.ProcessID))
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
