using System.Collections.Generic;
using TRACING = Microsoft.Diagnostics.Tracing;
using Microsoft.ETWLogAnalyzer.Abstractions;

namespace Microsoft.ETWLogAnalyzer
{
    public static class Controller
    {
        private static readonly List<System.Type> _reportList = new List<System.Type>();

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

        public  static void RegisterReports(List<System.Type> reportsToAdd)
        {
            _reportList.AddRange(reportsToAdd);
        }

        public static void ProcessReports(string folder, ETWData etwData)
        {
            string baseFolder = System.Environment.ExpandEnvironmentVariables(folder);
            
            foreach (System.Type reportType in _reportList)
            {
                if (reportType.IsSubclassOf(typeof(ReportBase)))
                {
                    continue;
                }

                var reportInstance = System.Activator.CreateInstance(reportType) as ReportBase;
                System.Diagnostics.Debug.Assert(reportInstance != null);

                reportInstance.Analyze(etwData).Persist(
                    new ReportWriters.PlainTextWriter(System.IO.Path.Combine(baseFolder, reportInstance.Name)), true);
            }
        }
    }
}
