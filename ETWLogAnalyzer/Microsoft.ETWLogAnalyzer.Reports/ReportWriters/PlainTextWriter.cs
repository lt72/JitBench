using System.IO;

namespace Microsoft.ETWLogAnalyzer.ReportWriters
{
    internal class PlainTextWriter : TextReportWriter
    {
        public PlainTextWriter(string file) : base(new StreamWriter(file))
        {
        }
    }
}
