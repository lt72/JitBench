using System;
using System.Collections.Generic;
using System.Diagnostics;

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

        private readonly Dictionary<int, ETWTimeInterval> _threadLifetimes = new Dictionary<int, ETWTimeInterval>();
        private          Dictionary<int, SortedList<double, TRACING.TraceEvent>> _threadSchedule;

        public Dictionary<int, ETWTimeInterval> ThreadLifeIntervals
        {
            get
            {
                return _threadLifetimes;
            }
        }

        private SortedList<double, TRACING.TraceEvent> GetThreadList(int id)
        {
            if (_threadSchedule.TryGetValue(id, out _) == false)
            {
                _threadSchedule.Add(id, new SortedList<double, TRACING.TraceEvent>());
            }

            return _threadSchedule[id];
        }
    }
}
