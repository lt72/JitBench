using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.ETWLogAnalyzer.ReportWriters;
using Microsoft.ETWLogAnalyzer.Abstractions;
using Microsoft.ETWLogAnalyzer.ReportVisitors;
using Microsoft.ETWLogAnalyzer.Framework;

using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.Reports
{
    public class ThreadStatistics : IReport
    {
        private class QuantumTimeInfo
        {
            public double JitTimeUsed { get; private set; }
            public double AvailableQuantumTime { get; private set; }

            public QuantumTimeInfo(double jitTime, double quantumTime)
            {
                JitTimeUsed = jitTime;
                AvailableQuantumTime = quantumTime;
            }
        }

        private static readonly string FormatString = "{0, -35}:\t{1,9}";

        private Dictionary<int, Dictionary<MethodUniqueIdentifier, QuantumTimeInfo>> _methodJistStatsPerThread;
        private Dictionary<int, MethodUniqueIdentifier> _firstMethodJitted;

        public ThreadStatistics()
        {
            _methodJistStatsPerThread = new Dictionary<int, Dictionary<MethodUniqueIdentifier, QuantumTimeInfo>>();
            _firstMethodJitted = new Dictionary<int, MethodUniqueIdentifier>();
        }

        public string Name => "quantum_usage_stats.txt";

        public IReport Analyze(EventModelBase data)
        {
            foreach (int threadId in data.GetThreadList)
            {
                var jitTimeVisitor = new JitTimeAccumulatorVisitor(threadId);
                var availableQuantumTimeVisitor = new AvailableQuantumAccumulatorVisitor(threadId);
                var jitMethodVisitor = new GetFirstMatchingEventVisitor<PARSERS.Clr.MethodLoadUnloadVerboseTraceData>();
                Controller.RunVisitorForResult(jitTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(availableQuantumTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(jitMethodVisitor, data.GetThreadTimeline(threadId));

                Debug.Assert(jitTimeVisitor.State != EventVisitor<Dictionary<MethodUniqueIdentifier, double>>.VisitorState.Error
                    && availableQuantumTimeVisitor.State != EventVisitor<Dictionary<MethodUniqueIdentifier, double>>.VisitorState.Error);

                _methodJistStatsPerThread.Add(threadId,
                    ZipResults(jitTimeVisitor.Result, availableQuantumTimeVisitor.Result));

                var methodUniqueId = (jitMethodVisitor.Result == null) ? null : new MethodUniqueIdentifier(jitMethodVisitor.Result);
                _firstMethodJitted.Add(threadId, methodUniqueId);
            }
            return this;
        }

        public void Persist(string folderPath)
        {
            using (var writer = new ReportWriters.PlainTextWriter(System.IO.Path.Combine(folderPath, Name)))
            {
                writer.WriteTitle("Thread Usage with Respect to Jitting");
                writer.Write($"\nThe process used {_methodJistStatsPerThread.Count} thread(s) as follows:");

                foreach (var threadInfo in _methodJistStatsPerThread)
                {
                    QuantumTimeInfo threadQuantumTime = AccumulateMethodTimes(threadInfo.Value);
                    var efficiency = (threadQuantumTime.AvailableQuantumTime == 0) ?
                        100 :
                        threadQuantumTime.JitTimeUsed / threadQuantumTime.AvailableQuantumTime * 100;

                    writer.WriteHeader("Thread " + threadInfo.Key);
                    writer.AddIndentationLevel();
                    if (_firstMethodJitted.TryGetValue(threadInfo.Key, out var methodUniqueId) && methodUniqueId != null)
                    {
                        writer.WriteLine($"First jitted method {methodUniqueId.FullyQualifiedName}.");
                    }
                    writer.WriteLine(String.Format(FormatString, "Effective jitting time [ms]", threadQuantumTime.JitTimeUsed));
                    writer.WriteLine(String.Format(FormatString, "Quantum time assigned for jit [ms]", threadQuantumTime.AvailableQuantumTime));
                    writer.WriteLine(String.Format(FormatString, "Quantum Efficiency [%]", efficiency));
                    writer.RemoveIndentationLevel();
                }

                writer.SkipLine();
                writer.SkipLine();

                writer.WriteTitle("Method Jitting Statistics");

                foreach (var methodsInThread in _methodJistStatsPerThread.Values)
                {
                    foreach (var methodInfoTimePair in methodsInThread)
                    {
                        writer.WriteHeader("Method " + methodInfoTimePair.Key);

                        writer.AddIndentationLevel();

                        double jitTime = methodInfoTimePair.Value.JitTimeUsed;
                        double requestedTime = methodInfoTimePair.Value.AvailableQuantumTime;
                        writer.WriteLine(String.Format(FormatString, "Effective jitting time [ms]", jitTime));
                        writer.WriteLine(String.Format(FormatString, "Quantum time assigned for jit [ms]", requestedTime));
                        writer.WriteLine(String.Format(FormatString, "Quantum efficiency [%]", 100.0 * jitTime / requestedTime));

                        writer.RemoveIndentationLevel();
                    }
                }
            }
        }

        // Helpers

        private QuantumTimeInfo AccumulateMethodTimes(Dictionary<MethodUniqueIdentifier, QuantumTimeInfo> threadMethodJitTimes)
        {
            double threadJitTime = threadMethodJitTimes.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.JitTimeUsed);
            double threadQuantumJitTime = threadMethodJitTimes.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.AvailableQuantumTime);
            return new QuantumTimeInfo(threadJitTime, threadQuantumJitTime);
        }

        private Dictionary<MethodUniqueIdentifier, QuantumTimeInfo> ZipResults(
            Dictionary<MethodUniqueIdentifier, double> jitTimeUsedPerMethod,
            Dictionary<MethodUniqueIdentifier, double> availableJitTimePerMethod)
        {
            return (from methodUniqueId in jitTimeUsedPerMethod.Keys
                    let jitTime = jitTimeUsedPerMethod[methodUniqueId]
                    let quantumTime = availableJitTimePerMethod[methodUniqueId]
                    select new KeyValuePair<MethodUniqueIdentifier, QuantumTimeInfo>(
                        methodUniqueId,
                        new QuantumTimeInfo(jitTime, quantumTime)))
                    .ToDictionary(x => x.Key, x => x.Value);
        }
    }
}
