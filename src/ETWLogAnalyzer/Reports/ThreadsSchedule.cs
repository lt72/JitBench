using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using MusicStore.ETWLogAnalyzer.ReportWriters;

namespace MusicStore.ETWLogAnalyzer
{
    internal class ThreadsSchedule : ReportBase
    {
        private Dictionary<int, ETWData.ETWTimeInterval> _schedule;

        public ThreadsSchedule()
        {
        }

        public override ReportBase Analyze(ETWData data)
        {
            _schedule = data.Schedule;

            return this;
        }

        public override void Persist(TextReportWriter writer, bool dispose)
        {
            writer.WriteHeader("Thread Schedule");
            foreach (var th in _schedule)
            {
                writer.WriteLine($"Thread {th.Key}\t\t: start: {th.Value.Begin}, runs to: {th.Value.End})");
            }

            if (dispose)
            {
                writer.Dispose(); 
            }            
        }
    }
}
