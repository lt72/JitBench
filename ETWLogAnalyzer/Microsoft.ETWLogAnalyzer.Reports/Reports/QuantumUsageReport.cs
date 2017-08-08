using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ETWLogAnalyzer.Abstractions;
using Microsoft.ETWLogAnalyzer.ReportVisitors;
using Microsoft.ETWLogAnalyzer.Framework;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.Reports
{
    /// <summary>
    /// Report that analyzes how much of the scheduler quantums/time slices the different threads/methods are using.
    /// </summary>
    public class QuantumUsageReport : IReport
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

        private static readonly string FormatString = "{0, -42}:\t{1:F2}";

        private Dictionary<int, Dictionary<MethodUniqueIdentifier, QuantumTimeInfo>> _methodJistStatsPerThread;
        private Dictionary<int, MethodUniqueIdentifier> _firstMethodJitted;
        private Dictionary<int, Dictionary<MethodUniqueIdentifier, long>> _contextSwitchesPerMethod;
        private Dictionary<int, Dictionary<MethodUniqueIdentifier, long>> _hardFaultsPerMethod;
        private Dictionary<int, Dictionary<MethodUniqueIdentifier, long>> _unnecessaryContextSwitchesForMethodPerThread;
        public string Name => "quantum_usage_report.txt";

        public QuantumUsageReport()
        {
            _methodJistStatsPerThread = new Dictionary<int, Dictionary<MethodUniqueIdentifier, QuantumTimeInfo>>();
            _firstMethodJitted = new Dictionary<int, MethodUniqueIdentifier>();
            _contextSwitchesPerMethod = new Dictionary<int, Dictionary<MethodUniqueIdentifier, long>>();
            _hardFaultsPerMethod = new Dictionary<int, Dictionary<MethodUniqueIdentifier, long>>();
            _unnecessaryContextSwitchesForMethodPerThread = new Dictionary<int, Dictionary<MethodUniqueIdentifier, long>>();
        }

        public bool Analyze(IEventModel data)
        {
            foreach (int threadId in data.ThreadList)
            {
                var jitTimeVisitor = new JitTimeAccumulatorVisitor(threadId);
                var availableQuantumTimeVisitor = new AvailableQuantumAccumulatorVisitor(threadId);
                var jitMethodVisitor = new GetFirstMatchingEventVisitor<PARSERS.Clr.MethodLoadUnloadVerboseTraceData>();
                var contextSwitchesPerMethodVisitor = new GetCountEventsBetweenAllStartStopEventsPairVisitor<PARSERS.Clr.MethodJittingStartedTraceData, PARSERS.Clr.MethodLoadUnloadVerboseTraceData, PARSERS.Kernel.CSwitchTraceData, MethodUniqueIdentifier>(matchingCriteria: x => x.OldThreadID == threadId);
                var hardFaultsPerMethodVisitor = new GetCountEventsBetweenAllStartStopEventsPairVisitor<PARSERS.Clr.MethodJittingStartedTraceData, PARSERS.Clr.MethodLoadUnloadVerboseTraceData, PARSERS.Kernel.MemoryHardFaultTraceData, MethodUniqueIdentifier>();
                var potentiallyUnnecessarySwitchVisitor = new UnnecessaryContextSwitchesVisitor(threadId);

                Controller.RunVisitorForResult(jitTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(availableQuantumTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(jitMethodVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(potentiallyUnnecessarySwitchVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(contextSwitchesPerMethodVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(hardFaultsPerMethodVisitor, data.GetThreadTimeline(threadId));

                if (jitTimeVisitor.State == VisitorState.Error
                    || availableQuantumTimeVisitor.State == VisitorState.Error
                    || jitMethodVisitor.State == VisitorState.Error
                    || contextSwitchesPerMethodVisitor.State == VisitorState.Error
                    || hardFaultsPerMethodVisitor.State == VisitorState.Error
                    || potentiallyUnnecessarySwitchVisitor.State == VisitorState.Error)
                {
                    return false;
                }

                var methodUniqueId = (jitMethodVisitor.Result == null) ? null : new MethodUniqueIdentifier(jitMethodVisitor.Result);
                _methodJistStatsPerThread.Add(threadId, ZipResults(jitTimeVisitor.Result, availableQuantumTimeVisitor.Result));
                _firstMethodJitted.Add(threadId, methodUniqueId);
                _contextSwitchesPerMethod.Add(threadId, contextSwitchesPerMethodVisitor.Result);
                _hardFaultsPerMethod.Add(threadId, hardFaultsPerMethodVisitor.Result);
                _unnecessaryContextSwitchesForMethodPerThread.Add(threadId, potentiallyUnnecessarySwitchVisitor.Result);
            }
            return true;
        }

        public bool Persist(string folderPath)
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
                    if (_firstMethodJitted.TryGetValue(threadInfo.Key, out var methodUniqueId))
                    {
                        var firstJittedMethod = methodUniqueId == null ? "<none>" : methodUniqueId.FullyQualifiedName;

                        writer.WriteLine($"First jitted method '{firstJittedMethod}'.");
                    }
                    writer.WriteLine(String.Format(FormatString, "Effective jitting time [ms]", threadQuantumTime.JitTimeUsed));
                    writer.WriteLine(String.Format(FormatString, "Quantum time assigned for jit [ms]", threadQuantumTime.AvailableQuantumTime));
                    writer.WriteLine(String.Format(FormatString, "Quantum Efficiency [%]", efficiency));
                    writer.WriteLine(String.Format(FormatString, "Total context switches", _contextSwitchesPerMethod[threadInfo.Key].Values.Aggregate(0, (long accum, long val) => (accum + val))));
                    writer.WriteLine(String.Format(FormatString, "Potentially unnecessary context switches", _unnecessaryContextSwitchesForMethodPerThread[threadInfo.Key].Values.Aggregate(0, (long accum, long val) => accum + val)));
                    writer.WriteLine(String.Format(FormatString, "Total page faults", _hardFaultsPerMethod[threadInfo.Key].Values.Aggregate(0, (long accum, long val) => (accum + val))));
                    writer.RemoveIndentationLevel();
                }

                writer.SkipLine();
                writer.SkipLine();

                writer.WriteTitle("Method Jitting Statistics");

                foreach (var methodsInThread in _methodJistStatsPerThread)
                {
                    foreach (var methodInfoTimePair in methodsInThread.Value)
                    {
                        writer.WriteHeader("Method " + methodInfoTimePair.Key);

                        writer.AddIndentationLevel();

                        double jitTime = methodInfoTimePair.Value.JitTimeUsed;
                        double requestedTime = methodInfoTimePair.Value.AvailableQuantumTime;
                        writer.WriteLine(String.Format(FormatString, "Effective jitting time [ms]", jitTime));
                        writer.WriteLine(String.Format(FormatString, "Quantum time assigned for jit [ms]", requestedTime));
                        writer.WriteLine(String.Format(FormatString, "Quantum efficiency [%]", 100.0 * jitTime / requestedTime));
                        writer.WriteLine(String.Format(FormatString, "Total context switches", _contextSwitchesPerMethod[methodsInThread.Key][methodInfoTimePair.Key]));
                        writer.WriteLine(String.Format(FormatString, "Potentially unnecessary context switches", _unnecessaryContextSwitchesForMethodPerThread[methodsInThread.Key][methodInfoTimePair.Key]));
                        writer.WriteLine(String.Format(FormatString, "Total page faults", _hardFaultsPerMethod[methodsInThread.Key][methodInfoTimePair.Key]));

                        writer.RemoveIndentationLevel();
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Helper to accumulate times of methods into a single structure.
        /// </summary>
        /// <returns> Accumulated times in a QuantumTimeInfo structure. </returns>
        private QuantumTimeInfo AccumulateMethodTimes(Dictionary<MethodUniqueIdentifier, QuantumTimeInfo> threadMethodJitTimes)
        {
            double threadJitTime = threadMethodJitTimes.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.JitTimeUsed);
            double threadQuantumJitTime = threadMethodJitTimes.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.AvailableQuantumTime);
            return new QuantumTimeInfo(threadJitTime, threadQuantumJitTime);
        }

        /// <summary>
        /// Aggregates different results per method in a helper structure.
        /// </summary>
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
