using System.IO;

namespace Microsoft.ETWLogAnalyzer.ReportWriters
{
    /// <summary>
    /// Writing utility for reports.
    /// </summary>
    internal class PlainTextWriter : TextReportWriter
    {
        public PlainTextWriter(string file) : base(new StreamWriter(file))
        {
        }
    }
}
