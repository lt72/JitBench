using System.Collections.Generic;
using System.Linq;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace MusicStore.ETWLogAnalyzer
{
    internal partial class ETWData
    {
        internal class ThreadEvent
        {
            private readonly TRACING.TraceEvent _etwData;

            public ThreadEvent(TRACING.TraceEvent etwData)
            {
                _etwData = etwData;
            }

            public int ThreadId
            {
                get
                {
                    return _etwData.ThreadID;
                }
            }

            public double BeginTime
            {
                get
                {
                    return _etwData.TimeStampRelativeMSec;
                }
            }

            public double Duration { get; internal set; }

            public override int GetHashCode()
            {
                return 
                    _etwData.ProcessID.GetHashCode() ^ 
                    _etwData.ThreadID.GetHashCode() ^ 
                    _etwData.ActivityID.GetHashCode() ^ 
                    _etwData.TimeStamp.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if(obj == null || (obj is ThreadEvent) == false)
                {
                    return false;
                }

                if(ReferenceEquals(obj, this ))
                {
                    return true;
                }

                return obj.GetHashCode() == this.GetHashCode();
            }

            protected TRACING.TraceEvent Data
            {
                get
                {
                    return _etwData;
                }
            }
        }
        
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
                return $"{FullyQualifiedMethodName}: @{BeginTime} for {Duration}";
            }
        }

        /// <summary>
        /// Get a dictionary of events relevant to a thread sorted by time using the thread number as key
        /// </summary>
        public Dictionary<int, SortedList<double, TRACING.TraceEvent>> ThreadSchedule { get => _threadSchedule; }

        /// <summary>
        /// Returns a list of the threads used by the process.
        /// </summary>
        public Dictionary<int, SortedList<double, TRACING.TraceEvent>>.KeyCollection ThreadList { get => _threadSchedule.Keys; }

        public Dictionary<int, ETWTimeInterval> ThreadLifeIntervals { get => _threadLifetimes; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="threadId"></param>
        /// <returns></returns>
        public List<ETWTimeInterval> GetActiveIntervalsForThread(int threadId)
        {
            var contextSwitchInList =
                GetMatchingEventsForThread(
                    threadId,
                    (ev) => (ev is PARSERS.Kernel.CSwitchTraceData && (ev as PARSERS.Kernel.CSwitchTraceData).NewProcessID == PidUnderTest));

            var contextSwitchOutList =
                GetMatchingEventsForThread(
                    threadId,
                    (ev) => (ev is PARSERS.Kernel.CSwitchTraceData && (ev as PARSERS.Kernel.CSwitchTraceData).NewProcessID != PidUnderTest));

            var activeIntervals = contextSwitchInList
                        .Zip(contextSwitchOutList,
                            (cSwitchIn, cSwitchOut) => new ETWTimeInterval(this, cSwitchIn.TimeStampRelativeMSec, cSwitchOut.TimeStampRelativeMSec))
                        .ToList();

            return activeIntervals;
        }
    }
}
