using System;
using System.Collections.Generic;
using System.Linq;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;


namespace MusicStore.ETWLogAnalyzer
{
    internal partial class ETWData_jit
    {
        internal class JitEvent : ThreadEvent
        {
            internal class JitDurationComparer : IComparer<JitEvent>
            {
                public int Compare(JitEvent x, JitEvent y)
                {
                    //
                    // Reverse order, longest to shortest
                    //
                    if (x.Duration > y.Duration)
                    {
                        return -1;
                    }
                    else if (x.Duration < y.Duration)
                    {
                        return 1;
                    }

                    return 0;
                }
            }

            internal JitEvent(PARSERS.Clr.MethodJittingStartedTraceData etwData) : base(etwData)
            {

            }
            
            internal int MethodToken
            {
                get
                {
                    var jit = (PARSERS.Clr.MethodJittingStartedTraceData)Data;

                    return jit.MethodToken;
                }
            }

            internal string MethodName
            {
                get
                {
                    var jit = (PARSERS.Clr.MethodJittingStartedTraceData)Data;

                    return jit.MethodName;
                }
            }

            internal long MethodId
            {
                get
                {
                    var jit = (PARSERS.Clr.MethodJittingStartedTraceData)Data;

                    return jit.MethodID;
                }
            }

            internal string FullyQualifiedMethodName
            {
                get
                {
                    var jit = (PARSERS.Clr.MethodJittingStartedTraceData)Data;

                    return $"{jit.MethodNamespace}::{jit.MethodName}";
                }
            }

            public override string ToString()
            {
                return $"At {BeginTime} for {Duration}: {FullyQualifiedMethodName}";
            }
        }

        public List<JitEvent> GetJitInfoForThread(int threadId)
        {
            var jitMap = new Dictionary<long, JitEvent>();

            var jitStartEvents = GetMatchingEventsForThread(
                threadId,
                (ev) => (ev is PARSERS.Clr.MethodJittingStartedTraceData));
            
            var jitFinishedEvents = GetMatchingEventsForThread(
                threadId,
                (ev) => (ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData));
            
            foreach(var jitStart in jitStartEvents)
            {
                var castEvent = jitStart as PARSERS.Clr.MethodJittingStartedTraceData;
                jitMap.Add(castEvent.MethodID, new JitEvent(castEvent));
            }

            foreach(var jitStop in jitFinishedEvents)
            {
                var castEvent = jitStop as PARSERS.Clr.MethodLoadUnloadVerboseTraceData;
                if (!jitMap.TryGetValue(castEvent.MethodID, out JitEvent jitInfo))
                {
                    throw new ArgumentException($"The method {castEvent.MethodName} did not have a matching jit start event in the thread.");
                }
                jitInfo.Duration = castEvent.TimeStampRelativeMSec - jitInfo.BeginTime;
            }

            return jitMap.Values.ToList();
        }

        public double GetEffectiveJitTime(JitEvent jitEvent)
        {
            int threadId = jitEvent.ThreadId;
            double totalTime = 0;
            double jitStartTime = jitEvent.BeginTime;
            double jitEndTime = jitEvent.BeginTime + jitEvent.Duration;

            var contextSwitchInList =
                GetMatchingEventsForThread(
                    threadId,
                    (ev) => (ev is PARSERS.Kernel.CSwitchTraceData
                            && ev.TimeStampRelativeMSec >= jitStartTime && ev.TimeStampRelativeMSec <= jitEndTime
                            && (ev as PARSERS.Kernel.CSwitchTraceData).NewThreadID == threadId));

            var contextSwitchOutList =
                GetMatchingEventsForThread(
                    threadId,
                    (ev) => (ev is PARSERS.Kernel.CSwitchTraceData
                            && ev.TimeStampRelativeMSec >= jitStartTime && ev.TimeStampRelativeMSec <= jitEndTime
                            && (ev as PARSERS.Kernel.CSwitchTraceData).NewThreadID != threadId));

            System.Diagnostics.Debug.Assert(contextSwitchInList.Count == contextSwitchOutList.Count);

            // The edge case where a method jits in one quantum is seen often. Escape in this case immediately.
            if (contextSwitchInList.Count == 0)
            {
                return jitEvent.Duration;
            }

            // Counts the time in the intervals, with the edge cases of jit start to the first context switch out and
            // context switch in to jit finish (LoadVerbose event).
            totalTime += contextSwitchOutList[0].TimeStampRelativeMSec - jitStartTime; 

            for (int index = 1; index < contextSwitchOutList.Count; index++)
            {
                totalTime += contextSwitchOutList[index].TimeStampRelativeMSec - contextSwitchInList[index - 1].TimeStampRelativeMSec;
            }

            totalTime += jitEndTime - contextSwitchInList[contextSwitchInList.Count - 1].TimeStampRelativeMSec;

            return totalTime;
        }
    }
}
