using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using MusicStore.ETWLogAnalyzer.ReportWriters;

namespace MusicStore.ETWLogAnalyzer
{
    internal class JitAndIO : ReportBase
    {
        private class JitTime
        {
            public double JitEvents = 0;
            public double MinimumTime = Double.MaxValue;
            public double MaximunTime = Double.MinValue;
            public double TotalTime = 0;
            public double AverageTime = 0;
        }

        private readonly List<ETWData.JitEvent> _allJit;
        private readonly Dictionary<int, JitTime> _JitTimePerThread;
        private readonly HashSet<string> _JitWithIO;
        
        public JitAndIO()
        {
            _allJit = new List<ETWData.JitEvent>();
            _JitTimePerThread = new Dictionary<int, JitTime>();
            _JitWithIO = new HashSet<string>();
        }

        public override ReportBase Analyze(ETWData data)
        {
            //
            // Collect all Jit events in one container
            //
            foreach (var thList in data.JitEvents.Values)
            {
                foreach (var jit in thList.Values)
                {
                    _allJit.Add(jit);
                }
            }

            //_allJit.Sort(new ETWData.JitEvent.JitDurationComparer());

            //
            // Calculate total/min/max/average JIT time for all threads
            //
            foreach (var thID in data.JitEvents.Keys)
            {
                var thData = data.JitEvents[thID].Values;
                
                //
                // Check if this jit events on this thread includes any I/O event
                //
                foreach (var jit in thData)
                {
                    var IO = data.ThreadEvents[thID].Where<TRACING.TraceEvent>((ev) =>
                    {
                        return ev.TimeStampRelativeMSec > jit.BeginTime && ev.TimeStampRelativeMSec < jit.BeginTime + jit.Duration;
                    });

                    if (IO.Count<TRACING.TraceEvent>() > 0)
                    {
                        _JitWithIO.Add(jit.FullyQualifiedMethodName);
                    }
                }

                //
                // Record jit time for this thread
                //
                var jitTime = new JitTime();

                jitTime.JitEvents += thData.Count;

                foreach (var jit in thData)
                {
                    var duration = jit.Duration;

                    jitTime.MinimumTime = Math.Min(jitTime.MinimumTime, duration);
                    jitTime.MaximunTime = Math.Min(jitTime.MaximunTime, duration);
                    jitTime.TotalTime += duration;
                }

                jitTime.AverageTime = jitTime.TotalTime / jitTime.JitEvents;

                _JitTimePerThread.Add(thID, jitTime); 
            }

            return this;
        }

        public override void Persist(TextReportWriter writer, bool dispose)
        {
            writer.WriteHeader("JIT time");
            foreach (var jitTime in _JitTimePerThread)
            {
                writer.WriteLine($"Thread {jitTime.Key}\t\t: jit events: {jitTime.Value.JitEvents}, jit total time: {jitTime.Value.TotalTime}, (min: {jitTime.Value.MinimumTime}, avg: {jitTime.Value.AverageTime}, max: {jitTime.Value.MaximunTime})");
            }

            writer.WriteHeader("Jit time for all methods in order of Jit start time:");

            _allJit.Sort(new ETWData.JitEvent.JitStartComparer());

            foreach (var jit in _allJit)
            {
                bool hasIO = _JitWithIO.Contains(jit.FullyQualifiedMethodName);
                
                writer.WriteLine($"Method: {jit}, jit start at {jit.BeginTime}, duration {jit.Duration}, has I/O ? : {hasIO}");
            }

            writer.WriteHeader("");
            writer.WriteHeader("");
            writer.WriteHeader("");

            writer.WriteHeader("Jit time for all methods in order of Jit time duration:");

            _allJit.Sort(new ETWData.JitEvent.JitDurationComparer());

            foreach (var jit in _allJit)
            {
                bool hasIO = _JitWithIO.Contains(jit.FullyQualifiedMethodName);

                writer.WriteLine($"Method: {jit}, jit start at {jit.BeginTime}, duration {jit.Duration}, has I/O ? : {hasIO}");
            }

            if (dispose)
            {
                writer.Dispose(); 
            }            
        }
    }
}
