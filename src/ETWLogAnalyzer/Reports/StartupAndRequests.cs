using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using MusicStore.ETWLogAnalyzer.ReportWriters;

namespace MusicStore.ETWLogAnalyzer
{
    internal class StartupAndRequests : ReportBase
    {
        private List<TRACING.TraceEvent> _customEvents;

        public StartupAndRequests()
        {
        }

        public override ReportBase Analyze(ETWData data)
        {
            _customEvents = data.CustomEvents;

            return this;
        }

        public override void Persist(TextReportWriter writer, bool dispose)
        {
            writer.WriteHeader("Custom Events");
            foreach (var ev in _customEvents)
            {
                writer.WriteLine($"Thread {ev.ThreadID}\t\t: Event: {ev.EventName} at {ev.TimeStampRelativeMSec})");
            }

            if (dispose)
            {
                writer.Dispose(); 
            }            
        }
    }
}
