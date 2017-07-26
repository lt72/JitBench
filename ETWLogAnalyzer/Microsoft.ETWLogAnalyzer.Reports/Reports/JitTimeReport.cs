﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private static readonly string FormatString = "{0, -35}:\t{1:F2}";

        private Dictionary<int, Dictionary<MethodUniqueIdentifier, JitTimeInfo>> _methodJitStatsPerThread;
        private Dictionary<int, MethodUniqueIdentifier> _firstMethodJitted;
        private Dictionary<int, long> _contextSwitchesPerThread;
        private Dictionary<int, long> _hardFaultsPerThread;
        private Dictionary<MethodUniqueIdentifier, long> _contextSwitchesPerMethod;
        private Dictionary<MethodUniqueIdentifier, long> _hardFaultsPerMethod;
        public string Name => "jit_time_report.txt";

        public JitTimeReport()
        {
            _methodJitStatsPerThread = new Dictionary<int, Dictionary<MethodUniqueIdentifier, JitTimeInfo>>();
            _firstMethodJitted = new Dictionary<int, MethodUniqueIdentifier>();
            _contextSwitchesPerThread = new Dictionary<int, long>( );
            _hardFaultsPerThread = new Dictionary<int, long>( );
            _contextSwitchesPerMethod = new Dictionary<MethodUniqueIdentifier, long>( );
            _hardFaultsPerMethod = new Dictionary<MethodUniqueIdentifier, long>( );
        }

        public bool Analyze(IEventModel data)
        {
            foreach (int threadId in data.ThreadList)
            {
                var jitTimeVisitor = new JitTimeAccumulatorVisitor(threadId);
                var perceivedJitTimeVisitor = new PerceivedJitTimeVisitor(threadId);
                var jitMethodVisitor = new GetFirstMatchingEventVisitor<PARSERS.Clr.MethodLoadUnloadVerboseTraceData>();
                var contextSwitchesPerThreadVisitor = new GetCountEventsBetweenStartStopEventsPairVisitor<PARSERS.Kernel.ThreadTraceData, PARSERS.Kernel.ThreadTraceData, PARSERS.Kernel.CSwitchTraceData>(true);
                var hardFaultsPerThreadVisitor = new GetCountEventsBetweenStartStopEventsPairVisitor<PARSERS.Kernel.ThreadTraceData, PARSERS.Kernel.ThreadTraceData, PARSERS.Kernel.MemoryHardFaultTraceData>(true);
                var contextSwitchesPerMethodVisitor = new GetCountEventsBetweenAllStartStopEventsPairVisitor<PARSERS.Clr.MethodJittingStartedTraceData, PARSERS.Clr.MethodLoadUnloadVerboseTraceData, PARSERS.Kernel.CSwitchTraceData, MethodUniqueIdentifier>( false );
                var hardFaultsPerMethodVisitor = new GetCountEventsBetweenAllStartStopEventsPairVisitor<PARSERS.Clr.MethodJittingStartedTraceData, PARSERS.Clr.MethodLoadUnloadVerboseTraceData, PARSERS.Kernel.MemoryHardFaultTraceData, MethodUniqueIdentifier>( false );

                Controller.RunVisitorForResult(jitTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(perceivedJitTimeVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(jitMethodVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(contextSwitchesPerThreadVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(hardFaultsPerThreadVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(contextSwitchesPerMethodVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(hardFaultsPerMethodVisitor, data.GetThreadTimeline(threadId));

                if (jitTimeVisitor.State == VisitorState.Error
                    || perceivedJitTimeVisitor.State ==VisitorState.Error
                    || jitMethodVisitor.State == VisitorState.Error
                    || contextSwitchesPerMethodVisitor.State == VisitorState.Error
                    || contextSwitchesPerThreadVisitor.State == VisitorState.Error
                    || hardFaultsPerMethodVisitor.State == VisitorState.Error
                    || hardFaultsPerThreadVisitor.State == VisitorState.Error)
                {
                    return false;
                }

                var methodUniqueId = (jitMethodVisitor.Result == null) ? null : new MethodUniqueIdentifier(jitMethodVisitor.Result);
                _methodJitStatsPerThread.Add(threadId, ZipResults(jitTimeVisitor.Result, perceivedJitTimeVisitor.Result));
                _firstMethodJitted.Add(threadId, methodUniqueId);
                _contextSwitchesPerThread.Add(threadId, contextSwitchesPerThreadVisitor.Result);
                _hardFaultsPerThread.Add(threadId, hardFaultsPerThreadVisitor.Result);

                foreach (var item in contextSwitchesPerMethodVisitor.Result)
                {
                    _contextSwitchesPerMethod.Add(item.Key, item.Value);
                }

                foreach (var item in hardFaultsPerMethodVisitor.Result)
                {
                    _hardFaultsPerMethod.Add(item.Key, item.Value);
                }
            }
            return true;
        }

        public bool Persist(string folderPath)
        {
            using (var writer = new PlainTextWriter(System.IO.Path.Combine(folderPath, Name)))
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
                    if (_firstMethodJitted.TryGetValue(threadInfo.Key, out var methodUniqueId))
                    {
                        var firstJittedMethod = methodUniqueId == null ? "<none>" : methodUniqueId.FullyQualifiedName;

                        writer.WriteLine($"First jitted method '{firstJittedMethod}'.");
                    }
                    writer.WriteLine(String.Format(FormatString, "Effective jitting time [ms]", threadJitTimes.JitTimeUsed));
                    writer.WriteLine(String.Format(FormatString, "Nominal jitting time [ms]", threadJitTimes.PerceivedJitTime));
                    writer.WriteLine(String.Format(FormatString, "Jit time usage efficiency [%]", efficiency));
                    writer.WriteLine(String.Format(FormatString, "Total context switches", _contextSwitchesPerThread[ threadInfo.Key ]));
                    writer.WriteLine(String.Format(FormatString, "Total page faults", _hardFaultsPerThread[threadInfo.Key]));
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
                        writer.WriteLine(String.Format(FormatString, "Total context switches", _contextSwitchesPerMethod[methodIdJitTimePair.Key]));
                        writer.WriteLine(String.Format(FormatString, "Total page faults", _hardFaultsPerMethod[methodIdJitTimePair.Key]));

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