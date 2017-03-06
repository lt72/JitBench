using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicStore.ETWLogAnalyzer.ReportWriters
{
    internal class PlainTextWriter : TextReportWriter
    {
        public PlainTextWriter(string file) : base(new StreamWriter(file))
        {
        }
    }
}
