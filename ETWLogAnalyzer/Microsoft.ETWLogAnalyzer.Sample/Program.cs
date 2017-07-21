using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ETWLogAnalyzer.Framework;
using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer
{
    public static class Program
    {
        static Program()
        {
        }

        /// <summary>
        /// Executes the analysis tool. 
        /// Gather ETW logs with collect_etw_data.cmd in the Scripts folder of the project.
        /// </summary>
        /// <param name="args"> Command line parameters. See CmdLine or run with /Help flag for documentation. </param>
        /// <returns></returns>
        public static int Main(string[] args)
        {
            // Parse command line.
            if (args.Length > 0 && CmdLine.Process(args) == CmdLine.Cmd.ShowHelp)
            {
                return CmdLine.Usage();
            }

            var etwLogFile = CmdLine.Arguments[CmdLine.EtwLogSwitch].Value;

            if (File.Exists(etwLogFile) == false)
            {
                throw new ArgumentException($"EWT Log File {etwLogFile} does not exist.");
            }

            ETWData data = null;
            if (ShouldTryDeserialize(CmdLine.Arguments[CmdLine.EtwLogSwitch].Value))
            {
                Console.WriteLine($"Deserializing model for {etwLogFile}...");
                data = Controller.DeserializeDataModel(CmdLine.Arguments[CmdLine.EtwLogSwitch].Value);
            }

            if(data == null)
            {
                Console.WriteLine($"Model could not be deserialized. Generating model from ETW log...");
                data = GenerateModel();
            }

            if (data == null)
            {
                Console.WriteLine($"Model could not be generated. No reports will be written.");
            }
            else
            {
                Console.WriteLine("Generating reports...");

                Controller.ProcessReports(
                    CmdLine.Arguments[CmdLine.ReportPathSwitch].Value,
                    CmdLine.Arguments[CmdLine.OutputPathSwitch].Value,
                    data);

                Console.WriteLine("All done!");
            }

            if (CmdLine.Arguments[CmdLine.WaitSwitch].Value == "true")
            {
                Console.WriteLine("");
                Console.WriteLine("Press any key to terminate.");
                Console.ReadLine();
            }

            return 0;
        }

        /// <summary>
        /// Determines if the persisted model corresponds to the log.
        /// </summary>
        /// <param name="filePath"> Path to the log file. </param>
        /// <returns> True if the persisted model can be used. </returns>
        private static bool ShouldTryDeserialize(string filePath)
        {
            return false;

            //////string serializedPath = Controller.GetSerializedModelFilePath(filePath);

            //////if (File.Exists(serializedPath) == false)
            //////{
            //////    return false;
            //////}
            //////else if (File.GetLastAccessTime(serializedPath) > File.GetLastAccessTime(filePath))
            //////{
            //////    return true;
            //////}

            //////return false;
        }

        /// <summary>
        /// Generates a model from the log file.
        /// </summary>
        /// <param name="putStart"></param>
        /// <param name="putEnd"></param>
        /// <returns></returns>
        private static ETWData GenerateModel()
        {
            var etwLogFile = CmdLine.Arguments[CmdLine.EtwLogSwitch].Value;
            Console.WriteLine($"Opening ETW log file '{etwLogFile}'...");

            // Find process of interest, there may be multiple, and we only want to look at the child-most one.
            (var targetStart, var targetEnd) = FindChildmostTargetStartAndEnd(etwLogFile);

            if (targetStart == null || targetEnd == null)
            {
                var target = CmdLine.Arguments[CmdLine.TargetProcessSwitch].Value;
                Console.WriteLine($"Process {target} is not a transient within ETW log file '{etwLogFile}'! Can't analyze.");
                return null;
            }

            Console.WriteLine("Classifying events in the log...");
            var events = new Framework.Helpers.ETWEventsHolder(targetStart.ProcessID);
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

                // Page faults

                kernelParser.MemoryHardFault += delegate (PARSERS.Kernel.MemoryHardFaultTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                // I/O

                // File API's are ignored for now. We care about blocking I/O (where non-memory reads are necessary).
                kernelParser.DiskIORead += delegate (PARSERS.Kernel.DiskIOTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                kernelParser.DiskIOReadInit += delegate (PARSERS.Kernel.DiskIOInitTraceData data)
                {
                    events.StoreIfRelevant(data);
                };
                                
                // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
                
                // CLR events

                PARSERS.ClrTraceEventParser clrParser = source.Clr;

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

                clrParser.LoaderAssemblyLoad += delegate (PARSERS.Clr.AssemblyLoadUnloadTraceData data)
                {
                    events.StoreIfRelevant(data);
                };

                // Custom instrumentation 

                PARSERS.DynamicTraceEventParser eventSourceParser = source.Dynamic;

                eventSourceParser.All += delegate (TRACING.TraceEvent data)
                {
                    if (data.ProviderName == "aspnet-JitBench-MusicStore")
                    {
                        events.StoreIfRelevant(data);
                    }
                };
                
                source.Process();
            }

            var model = new ETWData(targetStart, targetEnd, events);

            // Controller.SerializeDataModel( model, CmdLine.Arguments[CmdLine.EtwLogSwitch].Value);

            return model;
        }

        /// <summary>
        /// Finds the childmost process with the name of the process under test.
        /// </summary>
        /// <param name="etwLogFile"> ETW event log </param>
        /// <returns> Pair of process start, process stop events. </returns>
        private static (PARSERS.Kernel.ProcessTraceData, PARSERS.Kernel.ProcessTraceData) FindChildmostTargetStartAndEnd(string etwLogFile)
        {
            // Host may spawn more than one dotnet process. We only need to look 
            // at the child process that actually hosts the program.
            var processUnderTest = CmdLine.Arguments[CmdLine.TargetProcessSwitch].Value;

            var processStarts = new Dictionary<int, PARSERS.Kernel.ProcessTraceData>();
            var processStops = new Dictionary<int, PARSERS.Kernel.ProcessTraceData>();
            using (var source = new TRACING.ETWTraceEventSource(etwLogFile))
            {
                // Process/Start or Process/Stop events lookup
                var kernelParser = new PARSERS.KernelTraceEventParser(source);

                kernelParser.ProcessStart += delegate (PARSERS.Kernel.ProcessTraceData proc)
                {
                    if (proc.ProcessName != processUnderTest)
                    {
                        return;
                    }

                    // Could be the father or child process, accumulate all instances for later analysis. 
                    // Use the pid of the parent process as the key in the lookup.
                    processStarts.Add(proc.ParentID, (PARSERS.Kernel.ProcessTraceData)proc.Clone());
                };

                // Defunct might be a better metric...
                kernelParser.ProcessStop += delegate (PARSERS.Kernel.ProcessTraceData proc)
                {
                    // Can't filter by process name here. For some reason stop events seem to lack the process name.
                    processStops.Add(proc.ProcessID, (PARSERS.Kernel.ProcessTraceData)proc.Clone());
                };

                source.Process();
            }

            // The process we are interested in is the childmost, so its process ID shouldn't be a key.
            PARSERS.Kernel.ProcessTraceData putStart = null;
            PARSERS.Kernel.ProcessTraceData putStop = null;

            foreach (var proc in processStarts.Values)
            {
                if (!processStarts.ContainsKey(proc.ProcessID))
                {
                    putStart = proc;
                    processStops.TryGetValue(proc.ProcessID, out putStop);
                    break;
                }
            }

            return (putStart, putStop);
        }
    }
}
