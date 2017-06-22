using System;
using System.Collections.Generic;
using System.Linq;

using MusicStore.ETWLogAnalyzer.ReportWriters;
using MusicStore.ETWLogAnalyzer.AbstractBases;
using MusicStore.ETWLogAnalyzer.ReportVisitors;

namespace MusicStore.ETWLogAnalyzer.Reports
{
    internal class ThreadStatistics : ReportBase
    {
        private class MethodQuantumInfo
        {
            public double JitTimeUsed { get; private set; }
            public double AvailableQuantumTime { get; private set; }

            public MethodQuantumInfo(double jitTime, double quantumTime)
            {
                JitTimeUsed = jitTime;
                AvailableQuantumTime = quantumTime;
            }
        }

        private Dictionary<int, Dictionary<ETWData.MethodUniqueIdentifier, MethodQuantumInfo>> _methodJistStatsPerThread;
                
        public ThreadStatistics()
        {
            _methodJistStatsPerThread = new Dictionary<int, Dictionary<ETWData.MethodUniqueIdentifier, MethodQuantumInfo>>();
            Name = "thread_quantum_stats.txt";
        }

        public override ReportBase Analyze(ETWData data)
        {
            foreach (int threadId in data.GetThreadList())
            {
                var jitTimeVisitor = new JitTimeAccumulatorVisitor(threadId);
                var availableQuantumTimeVisitor = new AvailableQuantumAccumulatorVisitor(threadId);
                Controller.RunVisitorForResult(jitTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(availableQuantumTimeVisitor, data.GetThreadTimeline(threadId));

                System.Diagnostics.Debug.Assert(jitTimeVisitor.State != EventVisitor<Dictionary<ETWData.MethodUniqueIdentifier, double>>.VisitorState.Error
                    && availableQuantumTimeVisitor.State != EventVisitor<Dictionary<ETWData.MethodUniqueIdentifier, double>>.VisitorState.Error);

                _methodJistStatsPerThread.Add(threadId,
                    ZipResults(jitTimeVisitor.Result, availableQuantumTimeVisitor.Result));
            }
            return this;
        }

        public override void Persist(TextReportWriter writer, bool dispose)
        {
            writer.WriteTitle("Thread Usage with Respect to Jitting");
            writer.Write($"\nThe process used {_methodJistStatsPerThread.Count} thread(s) as follows:");

            foreach(var threadInfo in _methodJistStatsPerThread)
            {
                writer.WriteHeader("Thread " + threadInfo.Key);

                writer.AddIndentationLevel();
                (double threadJitTime, double threadQuantumJitTime) = AccumulateMethodTimes(threadInfo.Value);

                var formatString = "{0, -35}:\t{1,9}";
                writer.WriteLine(String.Format(formatString, "Effective jitting time [ms]", threadJitTime));
                writer.WriteLine(String.Format(formatString, "Quantum time assigned for jit[ms]", threadQuantumJitTime));
                var efficiency = (threadQuantumJitTime == 0) ? 100 : threadJitTime / threadQuantumJitTime * 100;
                writer.WriteLine(String.Format(formatString, "Quantum Efficiency [%]", efficiency));
                writer.RemoveIndentationLevel();
            }

            if (dispose)
            {
                writer.Dispose();
            }
        }

        // Helpers

        private (double, double) AccumulateMethodTimes(Dictionary<ETWData.MethodUniqueIdentifier, MethodQuantumInfo> threadMethodJitTimes)
        {
            double threadJitTime = threadMethodJitTimes.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.JitTimeUsed);
            double threadQuantumJitTime = threadMethodJitTimes.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.AvailableQuantumTime);
            return (threadJitTime, threadQuantumJitTime);
        }

        private Dictionary<ETWData.MethodUniqueIdentifier, MethodQuantumInfo> ZipResults(
            Dictionary<ETWData.MethodUniqueIdentifier, double> jitTimeUsedPerMethod,
            Dictionary<ETWData.MethodUniqueIdentifier, double> availableJitTimePerMethod)
        {
            return (from methodUniqueId in jitTimeUsedPerMethod.Keys
                    let jitTime = jitTimeUsedPerMethod[methodUniqueId]
                    let quantumTime = availableJitTimePerMethod[methodUniqueId]
                    select new KeyValuePair<ETWData.MethodUniqueIdentifier, MethodQuantumInfo>(
                        methodUniqueId,
                        new MethodQuantumInfo(jitTime, quantumTime))).ToDictionary(x => x.Key, x => x.Value);
        }
    }
}
