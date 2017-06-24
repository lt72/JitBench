﻿using System.IO;

namespace Microsoft.ETWLogAnalyzer.ReportWriters
{
    public class PlainTextWriter : TextReportWriter
    {
        public PlainTextWriter(string file) : base(new StreamWriter(file))
        {
        }
    }
}