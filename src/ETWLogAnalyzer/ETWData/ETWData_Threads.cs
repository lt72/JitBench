using System.Collections.Generic;
using System.Linq;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace MusicStore.ETWLogAnalyzer
{
    internal partial class ETWData_thread
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

            internal TRACING.TraceEvent Data
            {
                get
                {
                    return _etwData;
                }
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
                    (ev) => (ev is PARSERS.Kernel.CSwitchTraceData && (ev as PARSERS.Kernel.CSwitchTraceData).NewThreadID == threadId));

            var contextSwitchOutList =
                GetMatchingEventsForThread(
                    threadId,
                    (ev) => (ev is PARSERS.Kernel.CSwitchTraceData && (ev as PARSERS.Kernel.CSwitchTraceData).OldThreadID == threadId));

            System.Diagnostics.Debug.Assert(contextSwitchInList.Count == contextSwitchOutList.Count);

            var activeIntervals = contextSwitchInList
                        .Zip(contextSwitchOutList,
                            (cSwitchIn, cSwitchOut) => new ETWTimeInterval(this, cSwitchIn.TimeStampRelativeMSec, cSwitchOut.TimeStampRelativeMSec))
                        .ToList();

            return activeIntervals;
        }
    }
}
