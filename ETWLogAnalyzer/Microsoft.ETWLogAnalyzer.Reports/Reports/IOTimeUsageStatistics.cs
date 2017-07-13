using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ETWLogAnalyzer.Abstractions;
using Microsoft.ETWLogAnalyzer.ReportVisitors;
using Microsoft.ETWLogAnalyzer.ReportWriters;
using Microsoft.ETWLogAnalyzer.Framework;

namespace Microsoft.ETWLogAnalyzer.Reports
{
    public class IOTimeUsageStatistics : IReport
    {
        private class TimeAllocationInfo
        {
            public double PerceivedJitTime { get; private set; }
            public double EffectiveJitTime { get; private set; }
            public double IdleTime { get; private set; }
            public double IOTime { get; private set; }
            public double UnscheduledNonIOTime { get; private set; }

            public TimeAllocationInfo(double perceivedTime, double effectiveTime, double idleTime, double ioTime, double unscheduledTime)
            {
                PerceivedJitTime = perceivedTime;
                EffectiveJitTime = effectiveTime;
                IdleTime = idleTime;
                IOTime = ioTime;
                UnscheduledNonIOTime = unscheduledTime;
            }
        }

        private Dictionary<int, Dictionary<MethodUniqueIdentifier, TimeAllocationInfo>> _methodUnscheduledTimeStats;

        public IOTimeUsageStatistics()
        {
            _methodUnscheduledTimeStats = new Dictionary<int, Dictionary<MethodUniqueIdentifier, TimeAllocationInfo>>();
        }


        public string Name => "io_time_usage_stats.txt";
        private static readonly string FormatString = "{0, -35}:\t{1:F2}";

        public IReport Analyze(IEventModel data)
        {
            foreach (var threadId in data.ThreadList)
            {
                var jitTimeVisitor = new JitTimeAccumulatorVisitor(threadId);
                var perceivedJitTimeVisitor = new PerceivedJitTimeVisitor(threadId);
                var unscheduledTimeClassifierVisitor = new UnscheduledTimeClassifierVisitor(threadId);

                Controller.RunVisitorForResult(jitTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(perceivedJitTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(unscheduledTimeClassifierVisitor, data.GetThreadTimeline(threadId));

                _methodUnscheduledTimeStats.Add(threadId, CombineResultsByMethod(
                    jitTimeVisitor.Result, perceivedJitTimeVisitor.Result, unscheduledTimeClassifierVisitor.Result));
            }

            return this;
        }

        public void Persist(string folderPath)
        {
            using (var writer = new PlainTextWriter(System.IO.Path.Combine(folderPath, Name)))
            {
                writer.WriteTitle("Jit time allocation statistics per thread");
                writer.Write($"\nThe process used {_methodUnscheduledTimeStats.Count} thread(s) as follows:");

                foreach (var threadInfo in _methodUnscheduledTimeStats)
                {
                    writer.WriteHeader("Thread " + threadInfo.Key);
                    WriteTimeAlloc(writer, AccumulateMethodTimes(threadInfo.Value));
                }

                writer.SkipLine();
                writer.SkipLine();

                writer.WriteTitle("Jitting statistics per method");

                foreach (var methodsInThread in _methodUnscheduledTimeStats.Values)
                {
                    foreach (var methodIdTimeAllodPair in methodsInThread)
                    {
                        writer.WriteHeader("Method " + methodIdTimeAllodPair.Key);
                        WriteTimeAlloc(writer, methodIdTimeAllodPair.Value);
                    }
                }
            }
        }

        private void WriteTimeAlloc(TextReportWriter writer, TimeAllocationInfo timeAllocInfo)
        {
            writer.AddIndentationLevel();
            writer.WriteLine(String.Format(FormatString, "Effective jitting time [ms]", timeAllocInfo.EffectiveJitTime));
            writer.WriteLine(String.Format(FormatString, "Nominal jitting time [ms]", timeAllocInfo.PerceivedJitTime));
            writer.WriteLine(String.Format(FormatString, "I/O time [ms]", timeAllocInfo.IOTime));
            writer.WriteLine(String.Format(FormatString, "Idle time [ms]", timeAllocInfo.IdleTime));
            writer.WriteLine(String.Format(FormatString, "Non-I/O unscheduled time [ms]", timeAllocInfo.UnscheduledNonIOTime));

            if (timeAllocInfo.PerceivedJitTime > 0)
            {
                writer.SkipLine();
                writer.WriteLine(String.Format(FormatString, "Jitting share [%]", 100.0 * timeAllocInfo.EffectiveJitTime / timeAllocInfo.PerceivedJitTime));
                writer.WriteLine(String.Format(FormatString, "I/O share [%]", 100.0 * timeAllocInfo.IOTime / timeAllocInfo.PerceivedJitTime));
                writer.WriteLine(String.Format(FormatString, "Idle share [%]", 100.0 * timeAllocInfo.IdleTime / timeAllocInfo.PerceivedJitTime));
                writer.WriteLine(String.Format(FormatString, "Non-I/O unscheduled share [%]", 100.0 * timeAllocInfo.UnscheduledNonIOTime / timeAllocInfo.PerceivedJitTime));
            }
            
            writer.RemoveIndentationLevel();
        }

        private TimeAllocationInfo AccumulateMethodTimes(Dictionary<MethodUniqueIdentifier, TimeAllocationInfo> threadsMethodTimeAllocs)
        {
            double threadJitTime = threadsMethodTimeAllocs.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.EffectiveJitTime);
            double threadPerceivedJitTime = threadsMethodTimeAllocs.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.PerceivedJitTime);
            double threadIoTime = threadsMethodTimeAllocs.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.IOTime);
            double threadIdleTime = threadsMethodTimeAllocs.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.IdleTime);
            double threadNonIOUnschedTime = threadsMethodTimeAllocs.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.UnscheduledNonIOTime);
            return new TimeAllocationInfo(threadPerceivedJitTime, threadJitTime, threadIdleTime, threadIoTime, threadNonIOUnschedTime);
        }

        private Dictionary<MethodUniqueIdentifier, TimeAllocationInfo> CombineResultsByMethod(
            Dictionary<MethodUniqueIdentifier, double> effectiveJitTimes,
            Dictionary<MethodUniqueIdentifier, double> nominalJitTimes,
            Dictionary<MethodUniqueIdentifier, (double idleTime, double ioTime, double otherUnscheduledTime)> unschedulesTimes)
        {
            return (from methodUniqueId in effectiveJitTimes.Keys
                    let effectiveTime = effectiveJitTimes[methodUniqueId]
                    let perceivedTime = nominalJitTimes[methodUniqueId]
                    let ioTime = unschedulesTimes[methodUniqueId].ioTime
                    let otherUnscheduledTime = unschedulesTimes[methodUniqueId].otherUnscheduledTime
                    let idleTime = unschedulesTimes[methodUniqueId].idleTime
                    select (methodUniqueId, timeAlloc: new TimeAllocationInfo(perceivedTime, effectiveTime, idleTime, ioTime, otherUnscheduledTime)))
                    .ToDictionary(x => x.methodUniqueId, x => x.timeAlloc);
        }
    }
}
