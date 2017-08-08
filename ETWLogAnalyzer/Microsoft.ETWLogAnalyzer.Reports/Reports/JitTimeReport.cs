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
    /// <summary>
    /// This report sumarizes how much time of the perceived time for jitting is used for jitting actually
    /// as well as an analysis of hard memory faults and context switches during jitting events.
    /// </summary>
    public class JitTimeReport : IReport
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

        private static readonly string FormatString = "{0, -42}:\t{1:F2}";

        private Dictionary<int, Dictionary<MethodUniqueIdentifier, JitTimeInfo>> _methodJitStatsPerThread;
        private Dictionary<int, MethodUniqueIdentifier> _firstMethodJitted;
        private Dictionary<int, Dictionary<MethodUniqueIdentifier, long>> _unnecessaryContextSwitchesForMethodPerThread;
        private Dictionary<int, Dictionary<MethodUniqueIdentifier, long>> _contextSwitchesPerMethod;
        private Dictionary<int, Dictionary<MethodUniqueIdentifier, long>> _hardFaultsPerMethod;
        private Dictionary<MethodUniqueIdentifier, int> _ILSizeMap;
        private Dictionary<int, long> _modulesLoadCountPerThread;
        private int _methodCount;

        public string Name => "jit_time_report.txt";

        public JitTimeReport()
        {
            _methodJitStatsPerThread = new Dictionary<int, Dictionary<MethodUniqueIdentifier, JitTimeInfo>>();
            _unnecessaryContextSwitchesForMethodPerThread = new Dictionary<int, Dictionary<MethodUniqueIdentifier, long>>();
            _firstMethodJitted = new Dictionary<int, MethodUniqueIdentifier>();
            _contextSwitchesPerMethod = new Dictionary<int, Dictionary<MethodUniqueIdentifier, long>>();
            _hardFaultsPerMethod = new Dictionary<int, Dictionary<MethodUniqueIdentifier, long>>();
            _modulesLoadCountPerThread = new Dictionary<int, long>();
            _ILSizeMap = new Dictionary<MethodUniqueIdentifier, int>();
        }

        public bool Analyze(IEventModel data)
        {
            _methodCount = System.Linq.Enumerable.Count(data.JittedMethodsList);

            foreach (int threadId in data.ThreadList)
            {
                var jitTimeVisitor = new JitTimeAccumulatorVisitor(threadId);
                var perceivedJitTimeVisitor = new PerceivedJitTimeVisitor(threadId);
                var jitMethodVisitor = new GetFirstMatchingEventVisitor<PARSERS.Clr.MethodLoadUnloadVerboseTraceData>();
                var contextSwitchesPerMethodVisitor = new GetCountEventsBetweenAllStartStopEventsPairVisitor<PARSERS.Clr.MethodJittingStartedTraceData, PARSERS.Clr.MethodLoadUnloadVerboseTraceData, PARSERS.Kernel.CSwitchTraceData, MethodUniqueIdentifier>(matchingCriteria: x => x.OldThreadID == threadId);
                var hardFaultsPerMethodVisitor = new GetCountEventsBetweenAllStartStopEventsPairVisitor<PARSERS.Clr.MethodJittingStartedTraceData, PARSERS.Clr.MethodLoadUnloadVerboseTraceData, PARSERS.Kernel.MemoryHardFaultTraceData, MethodUniqueIdentifier>();
                var moduleJittingCountVisitor = new GetCountEventsBetweenAllStartStopEventsPairVisitor<PARSERS.Clr.MethodJittingStartedTraceData, PARSERS.Clr.MethodLoadUnloadVerboseTraceData, PARSERS.Clr.ModuleLoadUnloadTraceData, MethodUniqueIdentifier>(false);
                var potentiallyUnnecessarySwitchVisitor = new UnnecessaryContextSwitchesVisitor(threadId);
                var ILCalculatorVisitor = new ILSizeVisitor();

                Controller.RunVisitorForResult(jitTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(perceivedJitTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(jitMethodVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(contextSwitchesPerMethodVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(hardFaultsPerMethodVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(moduleJittingCountVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(potentiallyUnnecessarySwitchVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(ILCalculatorVisitor, data.GetThreadTimeline(threadId));

                if (jitTimeVisitor.State == VisitorState.Error
                    || perceivedJitTimeVisitor.State ==VisitorState.Error
                    || jitMethodVisitor.State == VisitorState.Error
                    || contextSwitchesPerMethodVisitor.State == VisitorState.Error
                    || hardFaultsPerMethodVisitor.State == VisitorState.Error
                    || moduleJittingCountVisitor.State == VisitorState.Error
                    || potentiallyUnnecessarySwitchVisitor.State == VisitorState.Error
                    || ILCalculatorVisitor.State == VisitorState.Error)
                {
                    return false;
                }

                var methodUniqueId = (jitMethodVisitor.Result == null) ? null : new MethodUniqueIdentifier(jitMethodVisitor.Result);
                _methodJitStatsPerThread.Add(threadId, ZipResults(jitTimeVisitor.Result, perceivedJitTimeVisitor.Result));
                _firstMethodJitted.Add(threadId, methodUniqueId);
                _modulesLoadCountPerThread.Add(threadId, moduleJittingCountVisitor.Result.Values.Aggregate(0, (long accum, long val)=> (accum + val)));
                _unnecessaryContextSwitchesForMethodPerThread.Add(threadId, potentiallyUnnecessarySwitchVisitor.Result);
                _contextSwitchesPerMethod.Add(threadId, contextSwitchesPerMethodVisitor.Result);
                _hardFaultsPerMethod.Add(threadId, hardFaultsPerMethodVisitor.Result);
                _ILSizeMap = _ILSizeMap.Union(ILCalculatorVisitor.Result).ToDictionary(k => k.Key, k => k.Value);
            }
            return true;
        }

        public bool Persist(string folderPath)
        {
            using (var writer = new PlainTextWriter(System.IO.Path.Combine(folderPath, Name)))
            {
                writer.WriteTitle("Jit time statistics per thread");
                writer.Write($"\nThe process used {_methodJitStatsPerThread.Count} thread(s) to jit {_methodCount} methods as follows:");

                foreach (var threadInfo in _methodJitStatsPerThread)
                {
                    JitTimeInfo threadJitTimes = AccumulateMethodTimes(threadInfo.Value);
                    var efficiency = (threadJitTimes.PerceivedJitTime == 0) ?
                        100 :
                        threadJitTimes.JitTimeUsed / threadJitTimes.PerceivedJitTime * 100;

                    writer.WriteHeader("Thread " + threadInfo.Key);

                    writer.AddIndentationLevel();
                    writer.WriteLine(String.Format(FormatString, "Total jitted methods [-]", threadInfo.Value.Count));
                    if (_firstMethodJitted.TryGetValue(threadInfo.Key, out var methodUniqueId))
                    {
                        var firstJittedMethod = methodUniqueId == null ? "<none>" : methodUniqueId.FullyQualifiedName;
                        writer.WriteLine($"First jitted method '{firstJittedMethod}'.");
                    }
                    writer.WriteLine(String.Format(FormatString, "Effective jitting time [ms]", threadJitTimes.JitTimeUsed));
                    writer.WriteLine(String.Format(FormatString, "Nominal jitting time [ms]", threadJitTimes.PerceivedJitTime));
                    writer.WriteLine(String.Format(FormatString, "Jit time usage efficiency [%]", efficiency));
                    writer.WriteLine(String.Format(FormatString, "Total context switches", _contextSwitchesPerMethod[threadInfo.Key].Values.Aggregate(0, (long accum, long val) => (accum + val))));
                    writer.WriteLine(String.Format(FormatString, "Potentially unnecessary context switches", _unnecessaryContextSwitchesForMethodPerThread[threadInfo.Key].Values.Aggregate(0, (long accum, long val) => accum + val)));
                    writer.WriteLine(String.Format(FormatString, "Total page faults", _hardFaultsPerMethod[threadInfo.Key].Values.Aggregate(0, (long accum, long val) => (accum + val))));
                    writer.WriteLine(String.Format(FormatString, "Modules loaded while jitting", _modulesLoadCountPerThread[threadInfo.Key]));
                    writer.RemoveIndentationLevel();
                }

                writer.SkipLine();
                writer.SkipLine();

                writer.WriteTitle("Jitting statistics per method");

                foreach (var methodsInThread in _methodJitStatsPerThread)
                {
                    foreach (var methodIdJitTimePair in methodsInThread.Value)
                    {
                        writer.WriteHeader("Method " + methodIdJitTimePair.Key);

                        writer.AddIndentationLevel();

                        double jitTime = methodIdJitTimePair.Value.JitTimeUsed;
                        double perceivedTime = methodIdJitTimePair.Value.PerceivedJitTime;
                        writer.WriteLine(String.Format(FormatString, "Method IL size [B]", _ILSizeMap[methodIdJitTimePair.Key]));
                        writer.WriteLine(String.Format(FormatString, "Effective jitting time [ms]", jitTime));
                        writer.WriteLine(String.Format(FormatString, "Perceived jitting time [ms]", perceivedTime));
                        writer.WriteLine(String.Format(FormatString, "Jit time usage efficiency [%]", 100.0 * jitTime / perceivedTime));
                        writer.WriteLine(String.Format(FormatString, "Total context switches", _contextSwitchesPerMethod[methodsInThread.Key][methodIdJitTimePair.Key]));
                        writer.WriteLine(String.Format(FormatString, "Potentially unnecessary context switches", _unnecessaryContextSwitchesForMethodPerThread[methodsInThread.Key][methodIdJitTimePair.Key]));
                        writer.WriteLine(String.Format(FormatString, "Total page faults", _hardFaultsPerMethod[methodsInThread.Key][methodIdJitTimePair.Key]));

                        writer.RemoveIndentationLevel();
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Accumulates times per method into a helper structure.
        /// </summary>
        private JitTimeInfo AccumulateMethodTimes(Dictionary<MethodUniqueIdentifier, JitTimeInfo> threadMethodJitTimes)
        {
            double threadJitTime = threadMethodJitTimes.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.JitTimeUsed);
            double threadPerceivdJitTime = threadMethodJitTimes.Values.Aggregate(0.0, (accumulator, value) => accumulator + value.PerceivedJitTime);
            return new JitTimeInfo(threadJitTime, threadPerceivdJitTime);
        }
        
        /// <summary>
        /// Aggregates results into a helper structure.
        /// </summary>
        /// <returns> Dictiounary of aggregate structures mapped by method. </returns>
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
