using System.Collections.Generic;
using TRACING = Microsoft.Diagnostics.Tracing;
using Microsoft.ETWLogAnalyzer.Abstractions;
using System.IO;
using System.Reflection;
using System;
using System.Linq;

namespace Microsoft.ETWLogAnalyzer.Framework
{
    public static class Controller
    {
        public static void RunVisitorForResult<T>(EventVisitor<T> visitor, IEnumerator<TRACING.TraceEvent> iterator)
        {
            while (iterator.MoveNext() && visitor.State == EventVisitor<T>.VisitorState.Continue)
            {
                if (visitor.IsRelevant(iterator.Current))
                {
                    visitor.Visit(iterator.Current);
                }
            }
        }

        public static void ProcessReports(string reportsFolder, string outputFolder, ETWData etwData)
        {
            string baseFolder = System.Environment.ExpandEnvironmentVariables(outputFolder);
            
            foreach (Abstractions.IReport report in LoadReports( reportsFolder ))
            {
                System.Console.WriteLine($"Writing report {report.Name} to {baseFolder}...");

                report.Analyze(etwData).Persist( new ReportWriters.PlainTextWriter(System.IO.Path.Combine(baseFolder, report.Name)), true);
            }
        }
        
        public static IList<Abstractions.IReport> LoadReports(string reportsPath)
        {
            IList<Abstractions.IReport> reports = new List<Abstractions.IReport>();

            var reportsPathName = Path.GetDirectoryName(reportsPath);

            foreach (var dllFile in Directory.EnumerateFiles(reportsPathName, "*.dll"))
            {
                var pdbFile = Path.ChangeExtension(dllFile, ".pdb");

                Assembly assembly = null;

                try
                {
                    if (File.Exists(pdbFile))
                        assembly = AppDomain.CurrentDomain.Load(File.ReadAllBytes(dllFile), File.ReadAllBytes(pdbFile));
                    else
                        assembly = AppDomain.CurrentDomain.Load(File.ReadAllBytes(dllFile));
                }
                catch (BadImageFormatException)
                {
                }

                foreach (var t in assembly.ExportedTypes)
                {
                    if (t.GetInterfaces().Contains(typeof(Abstractions.IReport)))
                    {
                        Abstractions.IReport obj = null;

                        try
                        {
                            obj = Activator.CreateInstance(t) as Abstractions.IReport;
                        }
                        catch
                        {
                        }

                        if(obj == null)
                        {
                            Console.WriteLine($"Failed to create instance of report for type {t.FullName}. Report will not be generated.");
                            continue;
                        }

                        reports.Add( obj );
                    }
                }
            }

            return reports;
        }
    }
}
