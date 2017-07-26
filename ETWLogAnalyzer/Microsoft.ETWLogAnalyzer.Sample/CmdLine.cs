using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Microsoft.ETWLogAnalyzer.Utilities
{
    /// <summary>
    /// Command line utility parser.
    /// </summary>
    internal static class CmdLine
    {
        internal enum Cmd
        {
            ShowHelp = 0,
            Run,
        }

        /// <summary>
        /// Argument abstraction. Includes switch and default value as well as a description.
        /// </summary>
        internal class Argument
        {
            protected Argument(string swtch, string example, string description, string deflt, string value)
            {
                Switch = swtch;
                Example = example;
                Description = description;
                Default = deflt;
                Value = value;
            }

            internal static Argument New(string swtch, string example, string description, string deflt, string value)
            {
                return new Argument(swtch, example, description, deflt, value);
            }

            internal static Argument NewValue(Argument arg, string value)
            {
                if(String.IsNullOrEmpty(value))
                {
                    throw new InvalidOperationException("Do not use null or empty values for any arguments"); 
                }

                arg.Value = value;

                return arg; 
            }

            internal string Switch { get; private set; }
            internal string Example { get; private set; }
            internal string Description { get; private set; }
            internal string Default { get; private set; }
            internal string Value { get; private set; }

            internal virtual string PrettyPrint()
            {
                return $"{Switch}{SwitchValueSeparator}<{Example}>: {Description}. If not specified defauls to {Default}.";
            }
        }

        internal class NoValueArgument : Argument
        {

            private NoValueArgument(string swtch, string description) : base(swtch, String.Empty, description, String.Empty, String.Empty)
            {
            }

            internal static Argument New(string swtch, string description)
            {
                return new NoValueArgument(swtch, description);
            }

            internal override string PrettyPrint()
            {
                return $"{Switch}: {Description}.";
            }
        }

        internal static readonly Dictionary<string, Argument> Arguments = new Dictionary<string, Argument>();

        internal static readonly char SwitchValueSeparator = '=';
        //--//
        internal static readonly string HelpSwitch = "/help";
        internal static readonly string HelpDescription = "Shows this help";
        //--//
        internal static readonly string TargetProcessSwitch = "/target";
        internal static readonly string TargetExample = "process name, without extension";
        internal static readonly string TargetDescription = "name of the process to analyze";
        internal static readonly string TargetDefault = "dotnet";
        //--//
        internal static readonly string EtwLogSwitch = "/etwLog";
        internal static readonly string EtwLogExample = "path to file";
        internal static readonly string EtwLogDescription = "name of the ETW log file to analyze";
        internal static readonly string EtwLogDefault = "." + Path.DirectorySeparatorChar + "PerfViewData.etl.zip";
        //--//
        internal static readonly string ReportPathSwitch = "/reportGenerators";
        internal static readonly string ReportPathExample = "path to a directory";
        internal static readonly string ReportPathDescription = "path to a folder that contains assemblies to load and inspect for report generators";
        internal static readonly string ReportPathDefault = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;
        //--//
        internal static readonly string OutputPathSwitch = "/out-dir";
        internal static readonly string OutputPathExample = "Path (absolute, relative, or environment variable) to store reports to.";
        internal static readonly string OutputPathDescription = "path to store the log files in";
        internal static readonly string OutputPathDefault = "%TEMP%";
        //--//
        internal static readonly string WaitSwitch = "/wait";
        internal static readonly string WaitExample = "/wait";
        internal static readonly string WaitDescription = "if true program waits for a key press before exiting.";    
        internal static readonly string WaitDefault = "true";

        /// <summary>
        /// Add the new arguments here.
        /// </summary>
        static CmdLine()
        {
            Arguments.Add(HelpSwitch, Argument.New(HelpSwitch, String.Empty, HelpDescription, String.Empty, String.Empty));
            Arguments.Add(EtwLogSwitch, Argument.New(EtwLogSwitch, EtwLogExample, EtwLogDescription, EtwLogDefault, EtwLogDefault));
            Arguments.Add(TargetProcessSwitch, Argument.New(TargetProcessSwitch, TargetExample, TargetDescription, TargetDefault, TargetDefault));
            Arguments.Add(ReportPathSwitch, Argument.New(ReportPathSwitch, ReportPathExample, ReportPathDescription, ReportPathDefault, ReportPathDefault));
            Arguments.Add(OutputPathSwitch, Argument.New(OutputPathSwitch, OutputPathExample, OutputPathDescription, OutputPathDefault, OutputPathDefault));
            Arguments.Add(WaitSwitch, Argument.New(WaitSwitch, WaitExample, WaitDescription, WaitDefault, WaitDefault));
        }

        /// <summary>
        /// Parses arguments and cache the values.
        /// </summary>
        /// <param name="args"> Arguments as passed to the program. </param>
        /// <returns> Enum value to determine if program should show help or run. </returns>
        internal static Cmd Process(string[] args)
        {
            foreach(var arg in args)
            {
                var swtchEnd = arg.IndexOf(SwitchValueSeparator);

                bool hasValue = swtchEnd != -1;

                var swtchName = hasValue ? arg.Substring(0, swtchEnd) : arg;

                if(Arguments.ContainsKey(swtchName))
                {
                    if(swtchName == HelpSwitch)
                    {
                        return Cmd.ShowHelp;
                    }

                    if (hasValue)
                    {
                        var swtchValue = arg.Substring(swtchEnd + 1);

                        if (String.IsNullOrEmpty(swtchValue))
                        {
                            return Cmd.ShowHelp;
                        }

                        Argument.NewValue(Arguments[swtchName], swtchValue);
                    }
                }
                else
                {
                    return Cmd.ShowHelp;
                }
            }

            return Cmd.Run;
        }

        /// <summary>
        /// Display help.
        /// </summary>
        /// <returns> -1 </returns>
        internal static int Usage()
        {
            System.Console.WriteLine("Starts a new instance of the ETWLogAnalyzer for the JitBench repo. See https://github.com/aspnet/jitbench");
            System.Console.WriteLine("Arguments:");

            foreach (var arg in Arguments.Values)
            {
                System.Console.WriteLine(arg.PrettyPrint()); 
            }

            System.Console.WriteLine("");

            return -1;
        }
    }
}
