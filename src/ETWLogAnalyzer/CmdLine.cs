using System;
using System.Collections.Generic;
using System.IO;

namespace MusicStore.ETWLogAnalyzer
{
    internal static class CmdLine
    {
        internal enum Cmd
        {
            ShowHelp = 0,
            Run,
        }

        internal static readonly char SwitchValueSeparator = '=';

        internal static Cmd Process(string[] args)
        {
            if(args == null || args.Length == 0)
            {
                Console.WriteLine("INVALID ARGUMENT: no argument were supplied.");

                return Cmd.ShowHelp;
            }

            foreach(var arg in args)
            {
                string swtchValue = String.Empty;

                var swtchEnd = arg.IndexOf(SwitchValueSeparator);

                bool hasValue = swtchEnd != -1;

                var swtchName = hasValue ? arg.Substring(0, swtchEnd) : arg;
                
                if (swtchName == "/help")
                {
                    return Cmd.ShowHelp;
                }
                else if (swtchName == "/etwLog")
                {
                    if (hasValue)
                    {
                        swtchValue = arg.Substring(swtchEnd + 1);                            
                    }

                    if (hasValue == false || String.IsNullOrEmpty(swtchValue))
                    {
                        Console.WriteLine( "INVALID ARGUMENT: No valid value was supplied for /etwLog switch."); 

                        return Cmd.ShowHelp;
                    }

                    EtwLog = swtchValue;
                }
                else if (swtchName == "/testProcess")
                {
                    if (hasValue)
                    {
                        swtchValue = arg.Substring(swtchEnd + 1);
                    }

                    if (hasValue == false || String.IsNullOrEmpty(swtchValue))
                    {
                        Console.WriteLine("INVALID ARGUMENT: No valid value was supplied for /testProcess switch.");

                        return Cmd.ShowHelp;
                    }

                    TestProcess = swtchValue;
                }
                else
                {
                    Console.WriteLine($"INVALID ARGUMENT: switch {swtchName} is not recognized.");

                    return Cmd.ShowHelp;
                }
            }

            if (String.IsNullOrEmpty(TestProcess) || String.IsNullOrEmpty(EtwLog))
            {
                Console.WriteLine($"INVALID ARGUMENT: Parameters /testProcess and /etwLog cannot be null or empty.");

                return Cmd.ShowHelp;
            }

            if (File.Exists(EtwLog) == false)
            {
                Console.WriteLine($"EWT Log File {EtwLog} does not exists.");

                return Cmd.ShowHelp;
            }

            return Cmd.Run;
        }

        internal static int Usage()
        {
            Console.WriteLine("");
            Console.WriteLine("MusicStore.ETWLogAnalyzer.exe: starts a new instance of the ETWLogAnalyzer for the JitBench repo. See https://github.com/aspnet/jitbench");
            Console.WriteLine(@"Usage: MusicStore.ETWLogAnalyzer.exe /testProcess=<process name> /etwLog=<absolute path to etl file>");
            Console.WriteLine("");
            Console.WriteLine(@"Example: MusicStore.ETWLogAnalyzer.exe /testProcess=dotnet /etwLog=c:\temp\PerfViewTrace.etl");
            Console.WriteLine("");


            return -1;
        }

        //--//

        public static string EtwLog { get; internal set; }
        public static string TestProcess { get; internal set; }

    }
}
