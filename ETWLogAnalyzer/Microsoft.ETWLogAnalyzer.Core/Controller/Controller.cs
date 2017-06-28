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

            foreach (var file in Directory.EnumerateFiles(reportsPathName))
            {
                Assembly assembly = null;

                try
                {
                    assembly = AppDomain.CurrentDomain.Load(File.ReadAllBytes(file));
                }
                catch (BadImageFormatException)
                {
                    continue;
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
            catch(SerializationException ex)
            {
                Console.WriteLine("Failed to deserialize. Reason: " + ex.Message);
                throw;
            }

            return model;
        }

        public static string GetSerializedModelFilePath(string filePath)
        {
            return $"{filePath}"+".xml";
        }
    }
}
