using System.Collections.Generic;
using TRACING = Microsoft.Diagnostics.Tracing;
using Microsoft.ETWLogAnalyzer.Abstractions;
using System.IO;
using System.Reflection;
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;

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
                    double d = iterator.Current.TimeStampRelativeMSec;
                    visitor.Visit(iterator.Current);
                }
            }
        }

        public static void ProcessReports(string reportsFolder, string outputFolder, ETWData etwData)
        {
            string baseFolder = Environment.ExpandEnvironmentVariables(outputFolder);
            string logFilePath = Path.Combine(baseFolder, "report_status.log");
            Console.WriteLine($"Writing reports to {baseFolder}. See log stored in the same path for details.");
            using (var logFile = new System.IO.StreamWriter(logFilePath))
            {
                foreach (Abstractions.IReport report in LoadReports(reportsFolder, logFile))
                {
                    try
                    {
                        if (!report.Analyze(etwData).Persist(baseFolder))
                        {
                            logFile.WriteLine($"ERROR: Processing report {report.Name}. Unexpected error received.");
                        }
                        else
                        {
                            logFile.WriteLine($"SUCCESS: Report {report.Name} written to {baseFolder}");
                        }
                    }
                    catch (Exception exception)
                    {
                        logFile.WriteLine($"ERROR: Report {report.Name} finished with unhandled exception. Message: {exception.Source}::{exception.Message}.");
                        continue;
                    }
                }
            }
        }

        public static void SerializeDataModel(ETWData model, string filePath)
        {
            try
            {
                XmlSerializer formatter = new XmlSerializer(typeof(ETWData));
                using (FileStream fs = new FileStream(GetSerializedModelFilePath(filePath), FileMode.Create))
                {
                    formatter.Serialize(fs, model);
                }
            }
            catch (SerializationException ex)
            {
                Console.WriteLine("Failed to serialize. Reason: " + ex.Message);
                throw;
            }
        }

        public static ETWData DeserializeDataModel(string filePath)
        {
            ETWData model = null;

            try
            {
                XmlSerializer formatter = new XmlSerializer(typeof(ETWData));
                using (FileStream fs = new FileStream(GetSerializedModelFilePath(filePath), FileMode.Open))
                {
                    model = (ETWData)formatter.Deserialize(fs);
                }
            }
            catch (SerializationException ex)
            {
                Console.WriteLine("Failed to deserialize. Reason: " + ex.Message);
                throw;
            }

            return model;
        }

        public static string GetSerializedModelFilePath(string filePath)
        {
            return $"{filePath}" + ".xml";
        }

        private static IList<Abstractions.IReport> LoadReports(string reportsPath, StreamWriter logFile)
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
                            logFile.WriteLine($"ERROR: Failed to create instance of report for type {t.FullName}. Report will not be generated.");
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
