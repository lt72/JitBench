using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ETWLogAnalyzer.Abstractions;
using Microsoft.ETWLogAnalyzer.ReportVisitors;
using Microsoft.ETWLogAnalyzer.ReportWriters;

namespace Microsoft.ETWLogAnalyzer.Reports
{
    public class JitStatistics : ReportBase
    {
        private class JitTimeInfo
        {
            public double JitTimeUsed { get; private set; }
            public double PerceivedJitTime { get; private set; }

            public JitTimeInfo(double effectiveTime, double nominalTime)
            {
                JitTimeUsed = effectiveTime;
                PerceivedJitTime = nominalTime;
            }
        }

        private static readonly string FormatString = "{0, -35}:\t{1,9}";

        private Dictionary<int, Dictionary<MethodUniqueIdentifier, JitTimeInfo>> _methodJitStatsPerThread;

        public JitStatistics()
        {
            _methodJitStatsPerThread = new Dictionary<int, Dictionary<MethodUniqueIdentifier, JitTimeInfo>>();
            Name = "jit_time_stats.txt";
        }

        public override ReportBase Analyze(EventModelBase data)
        {
            foreach (int threadId in data.GetThreadList)
            {
                var jitTimeVisitor = new JitTimeAccumulatorVisitor(threadId);
                var perceivedJitTimeVisitor = new PerceivedJitTimeVisitor(threadId);

                Controller.RunVisitorForResult(jitTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(perceivedJitTimeVisitor, data.GetThreadTimeline(threadId));

                System.Diagnostics.Debug.Assert(jitTimeVisitor.State != EventVisitor<Dictionary<MethodUniqueIdentifier, double>>.VisitorState.Error
                    && perceivedJitTimeVisitor.State != EventVisitor<Dictionary<MethodUniqueIdentifier, double>>.VisitorState.Error);

                _methodJitStatsPerThread.Add(threadId,
                    ZipResults(jitTimeVisitor.Result, perceivedJitTimeVisitor.Result));
            }
            return this;
        }

        public override void Persist(TextReportWriter writer, bool dispose)
        {
            writer.WriteTitle("Jit time statistics per thread");
            writer.Write($"\nThe process used {_methodJitStatsPerThread.Count} thread(s) as follows:");

            foreach (var threadInfo in _methodJitStatsPerThread)
            {
                JitTimeInfo threadJitTimes = AccumulateMethodTimes(threadInfo.Value);
                var efficiency = (threadJitTimes.PerceivedJitTime == 0) ?
                    100 :
                    threadJitTimes.JitTimeUsed / threadJitTimes.PerceivedJitTime * 100;

                writer.WriteHeader("Thread " + threadInfo.Key);
                writer.AddIndentationLevel();
                writer.WriteLine(String.Format(FormatString, "Effective jitting time [ms]", threadJitTimes.JitTimeUsed));
                writer.WriteLine(String.Format(FormatString, "Perceived jitting time [ms]", threadJitTimes.PerceivedJitTime));
                writer.WriteLine(String.Format(FormatString, "Jit time usage efficiency [%]", efficiency));
                writer.RemoveIndentationLevel();
            }

            writer.SkipLine();
            writer.SkipLine();

            writer.WriteTitle("Jitting statistics per method");

            foreach (var methodsInThread in _methodJitStatsPerThread.Values)
            {
                foreach (var methodIdJitTimePair in methodsInThread)
                {
                    writer.WriteHeader("Method " + methodIdJitTimePair.Key);

                    writer.AddIndentationLevel();

                    double jitTime = methodIdJitTimePair.Value.JitTimeUsed;
                    double perceivedTime = methodIdJitTimePair.Value.PerceivedJitTime;
                    writer.WriteLine(String.Format(FormatString, "Effective jitting time [ms]", jitTime));
                    writer.WriteLine(String.Format(FormatString, "Perceived jitting time [ms]", perceivedTime));
                    writer.WriteLine(String.Format(FormatString, "Jit time usage efficiency [%]", 100.0 * jitTime / perceivedTime));

                    writer.RemoveIndentationLevel();
                }
            }

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
