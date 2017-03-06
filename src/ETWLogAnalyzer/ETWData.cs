using System;
using System.Collections.Generic;
using System.Diagnostics;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace MusicStore.ETWLogAnalyzer
{
    internal partial class ETWData
    {
        internal class ETWEventsHolder
        {
            /// <summary>
            /// Remember what process we are tracing, so we can filter all other events.
            /// </summary>
            private readonly int _pidUnderTest;
            /// <summary>
            /// We will sort events by their relative time stamp (relative to the start of the process under test).
            /// </summary>
            private readonly SortedDictionary<double, List<TRACING.TraceEvent>> _events;
            /// <summary>
            /// Keep track of all context switches, to and from the process under test.
            /// </summary>
            private readonly Dictionary<int, SortedList<double, PARSERS.Kernel.CSwitchTraceData>> _contextSwitches;
            /// <summary>
            /// Record all custom events we created
            /// </summary>
            private readonly List<TRACING.TraceEvent> _customEvents = new List<TRACING.TraceEvent>();

            public ETWEventsHolder(int pid)
            {
                _pidUnderTest = pid;
                _events = new SortedDictionary<double, List<Microsoft.Diagnostics.Tracing.TraceEvent>>();
                _contextSwitches = new Dictionary<int, SortedList<double, PARSERS.Kernel.CSwitchTraceData>>(); 
            }

            public SortedDictionary<double, List<TRACING.TraceEvent>>.ValueCollection Values
            {
                get
                {
                    return _events.Values;
                }
            }

            public Dictionary<int, SortedList<double, PARSERS.Kernel.CSwitchTraceData>> ContextSwitches
            {
                get
                {
                    return _contextSwitches;
                }
            }

            public List<TRACING.TraceEvent> CustomEvents
            {
                get
                {
                    return _customEvents;
                }
            }

            internal void DiscardOrRecord(TRACING.TraceEvent data)
            {
                //
                // Record context switches to and from the process under test
                // 
                if(data is PARSERS.Kernel.CSwitchTraceData)
                {
                    PARSERS.Kernel.CSwitchTraceData ev = (PARSERS.Kernel.CSwitchTraceData)data;

                    int processorNumber = ev.ProcessorNumber;

                    if(_contextSwitches.ContainsKey(processorNumber) == false)
                    {
                        _contextSwitches.Add(processorNumber, new SortedList<double, PARSERS.Kernel.CSwitchTraceData>()); 
                    }

                    var switches = _contextSwitches[processorNumber];

                    if (switches.ContainsKey(data.TimeStampRelativeMSec))
                    {
                        switches[data.TimeStampRelativeMSec] = (PARSERS.Kernel.CSwitchTraceData)data.Clone();
                    }
                    else
                    {
                        switches.Add(data.TimeStampRelativeMSec, (PARSERS.Kernel.CSwitchTraceData)data.Clone());
                    }
                }
                else if (data.ProviderName == "aspnet-JitBench-MusicStore")
                {
                    _customEvents.Add(data.Clone());
                }

                //
                // Filter all other events and keep only the ones from the process under test
                //
                if (FilterProcessById(data, _pidUnderTest))
                {
                    return;
                }

                Record(data);
            }
            
            private void Record(TRACING.TraceEvent ev)
            {
                List<TRACING.TraceEvent> events = null;
                if (_events.TryGetValue(ev.TimeStampRelativeMSec, out events) == false)
                {
                    _events.Add(ev.TimeStampRelativeMSec, new List<TRACING.TraceEvent>());
                }

                _events[ev.TimeStampRelativeMSec].Add((TRACING.TraceEvent)ev.Clone());
            }

            private static bool FilterProcessById(TRACING.TraceEvent ev, int pidUnderTest)
            {
                return ev.ProcessID != pidUnderTest;
            }
        }

        internal class ETWTimeInterval : IComparable<ETWTimeInterval>
        {
            internal class TimeIntervalComparer : IComparer<ETWTimeInterval>
            {
                public int Compare(ETWTimeInterval x, ETWTimeInterval y)
                {
                    return x.CompareTo(y);
                }
            }
            
            private readonly ETWData _owner;
            private readonly double _begin;
            private readonly double _duration;

            internal ETWTimeInterval(ETWData owner, double begin, double end)
            {
                _owner = owner;
                _begin = begin;
                _duration = end - begin;
            }

            internal bool IsOverlapping(ETWTimeInterval interval)
            {
                return this.Begin <= interval.Begin && this.End >= interval.End;
            }

            public int CompareTo(ETWTimeInterval other)
            {
                //
                // Order by start time
                //
                if (_begin < other._begin)
                {
                    return -1;
                }
                else if (_begin > other._begin)
                {
                    return 1;
                }

                return 0;
            }

            public double Begin
            {
                get
                {
                    return _begin;
                }
            }

            public double End
            {
                get
                {
                    return _owner.TimeBase + _duration;
                }
            }

            public override string ToString()
            {
                return $"[ {_begin} , {_duration} ]";
            }
        }
        
        private List<TRACING.TraceEvent> _customEvents;

        public ETWData(PARSERS.Kernel.ProcessTraceData data, ETWEventsHolder events)
        {
            this.PidUnderTest = data.ProcessID;
            this.TimeBase = data.TimeStampRelativeMSec;

            Analyze(events); 
        }
        
        public List<TRACING.TraceEvent> CustomEvents
        {
            get
            {
                return _customEvents;
            }
        }

        private void Analyze(ETWEventsHolder events)
        {
            //
            // Get ahold of all CTX switches and custom events
            //
            _contextSwitches = events.ContextSwitches;
            _customEvents = events.CustomEvents;

            Dictionary<int, ThreadEvent> threadStartEvents = new Dictionary<int, ThreadEvent>();

            foreach (var list in events.Values)
            {
                foreach (TRACING.TraceEvent ev in list)
                {
                    if (ev is PARSERS.Kernel.ThreadTraceData)
                    {
                        var ev1 = (PARSERS.Kernel.ThreadTraceData)ev;

                        switch (ev1.Opcode)
                        {
                            case TRACING.TraceEventOpcode.Start:
                                {
                                    threadStartEvents.Add(ev1.ThreadID, new ThreadEvent(ev1));

                                    break;
                                }
                            case TRACING.TraceEventOpcode.Stop:
                                {
                                    Debug.Assert(threadStartEvents.ContainsKey(ev1.ThreadID));

                                    var interval = new ETWTimeInterval(this, threadStartEvents[ev1.ThreadID].BeginTime, ev1.TimeStampRelativeMSec);

                                    _schedule.Add(ev1.ThreadID, interval);

                                    break;
                                }
                            default:
                                Debug.Assert(false);
                                break;
                        }
                    }
                    else
                    {
                        RecordRange(list);
                    }
                }
            }

            Debug.Assert(_threadEvents.Keys.Count == _schedule.Keys.Count); 
        }
        
        internal int PidUnderTest { get; private set; }

        internal double TimeBase { get; private set; }
    }
}
