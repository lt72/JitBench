using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ETWLogAnalyzer.Abstractions;
using Microsoft.ETWLogAnalyzer.ReportVisitors;
using Microsoft.ETWLogAnalyzer.ReportWriters;
using Microsoft.ETWLogAnalyzer.Framework;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.Reports
{
    public class LifetimeStatistics : IReport
    {
        private static readonly string FormatString = "{0, -35}:\t{1,9}";
        private double _processStartTime;
        private double _processFirstReq;
        private double _processProgramStarted;
        private double _processEndTime;
        private string _processName;
        private int _pid;

        public LifetimeStatistics()
        {
        }

        public string Name => "lifetime_stats.txt";

        public IReport Analyze(EventModelBase data)
        {
            _processStartTime = data.ProcessStart.TimeStampRelativeMSec;
            _processEndTime = data.ProcessStop.TimeStampRelativeMSec;
            _processName = data.ProcessStart.ProcessName;
            _pid = data.ProcessStart.ProcessID;

            foreach (int threadId in data.GetThreadList)
            {
                var startVisitor = new GetFirstMatchingEventVisitor((x) => (x is PARSERS.Kernel.ProcessTraceData && (x as PARSERS.Kernel.ProcessTraceData).));
                Controller.RunVisitorForResult(startVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(stopVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(jitVisitor, data.GetThreadTimeline(threadId));
            }

            return this;
        }

        public void Persist(TextReportWriter writer, bool dispose)
        {
            writer.WriteTitle("Process data");

            writer.WriteLine(String.Format(FormatString, "Process name [-]", _processName));
            writer.WriteLine(String.Format(FormatString, "Process ID [-]", _pid));
            writer.WriteLine(String.Format(FormatString, "Process start time [ms]", _processStartTime));
            writer.WriteLine(String.Format(FormatString, "Process stop time [ms]", _processEndTime));
            writer.WriteLine(String.Format(FormatString, "Process duration [ms]", _processEndTime - _processStartTime));


            writer.WriteTitle("Thread lifetime information");

            writer.Write($"\nThe process used {_methodJitStatsPerThread.Count} thread(s) as follows:");

            foreach (var threadInfo in _methodJitStatsPerThread)
            {
                writer.WriteHeader("Thread " + threadInfo.Key);
                writer.AddIndentationLevel();
                writer.WriteLine(String.Format(FormatString, "Start time [ms]", ));
                writer.WriteLine(String.Format(FormatString, "Stop time [ms]", ));
                writer.WriteLine(String.Format(FormatString, "First method Jitted [-]", ));
                writer.RemoveIndentationLevel();
            }

            writer.SkipLine();
            writer.SkipLine();

            if (dispose)
            {
                writer.Dispose();
            }
        }

        // Helpers

        private JitTimeInfo AccumulateMethodTimes(Dictionary<MethodUniqueIdentifier, JitTimeInfo> threadMethodJitTimes)
        {
            double threadJitTime = threadMethodJitTimes.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.JitTimeUsed);
            double threadPerceivdJitTime = threadMethodJitTimes.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.PerceivedJitTime);
            return new JitTimeInfo(threadJitTime, threadPerceivdJitTime);
        }

        private Dictionary<MethodUniqueIdentifier, JitTimeInfo> ZipResults(
            Dictionary<MethodUniqueIdentifier, double> jitTimeUsedPerMethod,
            Dictionary<MethodUniqueIdentifier, double> perceivedJitTimesPerMethod)
        {
            return (from methodUniqueId in jitTimeUsedPerMethod.Keys
                    let effectiveTime = jitTimeUsedPerMethod[methodUniqueId]
                    let perceivedTime = perceivedJitTimesPerMethod[methodUniqueId]
                    select new KeyValuePair<MethodUniqueIdentifier, JitTimeInfo>(
                        methodUniqueId,
                        new JitTimeInfo(effectiveTime, perceivedTime)))
                    .ToDictionary(x => x.Key, x => x.Value);
        }
    }
}
