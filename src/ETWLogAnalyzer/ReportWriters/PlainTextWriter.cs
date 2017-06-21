using System.IO;

namespace MusicStore.ETWLogAnalyzer.ReportWriters
{
    public class PlainTextWriter : TextReportWriter
    {
        public PlainTextWriter(string file) : base(new StreamWriter(file))
        {
        }
    }
}
