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
            internal class JitStartComparer : IComparer<JitEvent>
            {
                public int Compare(JitEvent x, JitEvent y)
                {
                    //
                    // Reverse order, longest to shortest
                    //
                    if (x.BeginTime > y.BeginTime)
                    {
                        return -1;
                    }
                    else if (x.BeginTime < y.BeginTime)
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
                return $"{FullyQualifiedMethodName} (Token:{MethodToken})";
            }
        }

        private readonly Dictionary<int, ETWTimeInterval> _schedule = new Dictionary<int, ETWTimeInterval>();
        private readonly Dictionary<int, List<TRACING.TraceEvent>> _threadEvents = new Dictionary<int, List<TRACING.TraceEvent>>();
        private          Dictionary<int, SortedList<double, PARSERS.Kernel.CSwitchTraceData>>  _contextSwitches; 
        private readonly Dictionary<int, SortedList<long,JitEvent>> _jitEvents = new Dictionary<int, SortedList<long,JitEvent>>();

        public Dictionary<int, ETWTimeInterval> Schedule
        {
            get
            {
                return _schedule;
            }
        }

        public Dictionary<int, List<TRACING.TraceEvent>> ThreadEvents
        {
            get
            {
                return _threadEvents;
            }
        }

        public Dictionary<int, SortedList<double, PARSERS.Kernel.CSwitchTraceData>> ContextSwitches
        {
            get
            {
                return _contextSwitches;
            }
        }

        public Dictionary<int, SortedList<long,JitEvent>> JitEvents
        {
            get
            {
                return _jitEvents;
            }
        }
        
        private void RecordRange(List<TRACING.TraceEvent> range)
        {
            GetThreadList(range[0].ThreadID).AddRange(range);

            foreach (var ev in range)
            {
                if (ev is PARSERS.Clr.MethodJittingStartedTraceData)
                {
                    var methodID = ((PARSERS.Clr.MethodJittingStartedTraceData)ev).MethodID;

                    var threadList = GetJitThreadList(ev.ThreadID);

                    if (threadList.ContainsKey(methodID) == false)
                    {
                        threadList.Add(methodID, new JitEvent((PARSERS.Clr.MethodJittingStartedTraceData)ev));
                    }
                    else
                    {
                        Console.WriteLine($"Duplicated method ID: {methodID}"); 
                    }
                }
                else if (ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData)
                {
                    var methodID = ((PARSERS.Clr.MethodLoadUnloadVerboseTraceData)ev).MethodID;

                    var jit = GetJitThreadList(ev.ThreadID)[methodID];

                    jit.Duration = ev.TimeStampRelativeMSec - jit.BeginTime;
                }
            }
        }

        private List<TRACING.TraceEvent> GetThreadList(int id)
        {
            List<TRACING.TraceEvent> events = null;
            if (_threadEvents.TryGetValue(id, out events) == false)
            {
                _threadEvents.Add(id, new List<TRACING.TraceEvent>());
            }

            return _threadEvents[id];
        }

        private SortedList<long, JitEvent> GetJitThreadList(int id)
        {
            SortedList<long, JitEvent> events = null;
            if (_jitEvents.TryGetValue(id, out events) == false)
            {
                _jitEvents.Add(id, new SortedList<long, JitEvent>());
            }

            return _jitEvents[id];
        }
    }
}
