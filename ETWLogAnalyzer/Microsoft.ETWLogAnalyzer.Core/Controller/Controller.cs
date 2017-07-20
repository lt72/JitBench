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
    /// <summary>
    /// This class provides a set of methods such that:
    ///     - Reports can get dynamically generated from a model that implements IEventModel.
    ///     - Data that is stored in the ETWData model can be persisted and loaded.
    ///     - Some helper methods can be used by ReportVisitors can safely access the model's data.
    /// </summary>
    public static class Controller
    {
        /// <summary>
        /// This methosd provides a way so visitors can be run until the result is found. The method will
        /// continue to run until the enumerator has no next element or the visitor reports an error or done state.
        /// </summary>
        /// <typeparam name="R"> Return type of the visitor. </typeparam>
        /// <param name="visitor"> Visitor to be run. </param>
        /// <param name="iterator"> IEnumerator used to iterate over an event timeline. </param>
        public static void RunVisitorForResult<R>(EventVisitor<R> visitor, IEnumerator<TRACING.TraceEvent> iterator)
        {
            while (iterator.MoveNext() && visitor.State == VisitorState.Continue)
            {
                if (visitor.IsRelevant(iterator.Current))
                {
                    double d = iterator.Current.TimeStampRelativeMSec;
                    visitor.Visit(iterator.Current);
                }
            }
        }

        /// <summary>
        /// This method looks for the report assemblies, and dynamically instantiates .
        /// A log file will get generated in the output folder directory stating the reports found and
        /// their status.
        /// </summary>
        /// <param name="reportsFolder"> Folder that contains the report assemblies. </param>
        /// <param name="outputFolder"> Folder to output the reports and the logs to. </param>
        /// <param name="dataModel"> Model containing the process's events. </param>
        public static void ProcessReports(string reportsFolder, string outputFolder, IEventModel dataModel)
        {
            string baseFolder = Environment.ExpandEnvironmentVariables(outputFolder);

            Directory.CreateDirectory(baseFolder);

            string logFilePath = Path.Combine(baseFolder, "report_status.log");

            Console.WriteLine($"Writing reports to {baseFolder}. See log stored in the same path for details.");
            using (var logFile = new StreamWriter(logFilePath))
            {
                foreach (Abstractions.IReport report in LoadReports(reportsFolder, logFile))
                {
                    try
                    {
                        if (!report.Analyze(dataModel))
                        {
                            logFile.WriteLine($"ERROR: Processing of report {report.Name} failed during analysis.");
                            continue;
                        }

                        if (!report.Persist(baseFolder))
                        {
                            logFile.WriteLine($"ERROR: Processing of report {report.Name} failed during persistence.");
                            continue;
                        }

                        logFile.WriteLine($"SUCCESS: Report {report.Name} written to {baseFolder}");
                    }
                    catch (Exception exception)
                    {
                        logFile.WriteLine($"ERROR: Report {report.Name} finished with unhandled exception. Message: {exception.Source}::{exception.Message}.");
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Serializes the ETWData given to XML
        /// </summary>
        /// <param name="model"> Model to serialize </param>
        /// <param name="filePath"> Path to serialize to </param>
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

        /// <summary>
        /// Deserializes the given XML to an ETWData instance.
        /// </summary>
        /// <param name="filePath"> XML-serialized model file </param>
        /// <returns> The deserialized model </returns>
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

        /// <summary>
        /// Dynamically tries to load and instantiate the reports in the given folder.
        /// </summary>
        /// <param name="reportsPath"> Folder containing the report assemblies. </param>
        /// <param name="logFile"> Stream to log errors to </param>
        /// <returns></returns>
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
                        IReport obj = null;

                        try
                        {
                            obj = Activator.CreateInstance(t) as IReport;
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
                        logFile.WriteLine($"Report loaded: {obj.Name}.");
                    }
                }
            }

            return reports;
        }
    }
}
