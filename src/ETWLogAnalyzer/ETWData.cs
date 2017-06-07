using System;
using System.Collections.Generic;
using System.Diagnostics;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using MusicStore.ETWLogAnalyzer.EventFilters;

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
            /// Schedule of all the events that affect a given thread.
            /// </summary>
            private readonly Dictionary<int, SortedList<double, TRACING.TraceEvent>> _threadSchedule;
            /// <summary>
            /// Set of filters that can be used to determine if an event is relevant to the thread.
            /// </summary>
            private readonly Dictionary<Type, IEventFilter> _filters;


            public ETWEventsHolder(int pid)
            {
                _pidUnderTest = pid;
                _events = new SortedDictionary<double, List<TRACING.TraceEvent>>();
                _threadSchedule = new Dictionary<int, SortedList<double, TRACING.TraceEvent>>();

                var threadIDFilter = new ThreadIDFilter(_pidUnderTest);
                var ioFilter = new IOFilter(_pidUnderTest);
                _filters = new Dictionary<Type, IEventFilter>
                {
                    { typeof(PARSERS.Kernel.CSwitchTraceData), new ThreadCSwitchFilter(_pidUnderTest) },
                    { typeof(PARSERS.Kernel.ThreadTraceData), threadIDFilter },
                    { typeof(PARSERS.Kernel.DiskIOInitTraceData), ioFilter},
                    { typeof(PARSERS.Kernel.DiskIOTraceData), ioFilter},
                    { typeof(PARSERS.Clr.MethodJittingStartedTraceData), threadIDFilter },
                    { typeof(PARSERS.Clr.MethodLoadUnloadVerboseTraceData), threadIDFilter },
                    { typeof(PARSERS.Clr.AssemblyLoadUnloadTraceData), threadIDFilter },
                    // { typeof(PARSERS.DynamicManifestTraceEvent), new DynamicFilter(_pidUnderTest) }
                    // Need to add ready thread filter
                };
            }

            public SortedDictionary<double, List<TRACING.TraceEvent>>.ValueCollection Values
            {
                get
                {
                    return _events.Values;
                }
            }

            public Dictionary<int, SortedList<double, TRACING.TraceEvent>> ThreadSchedule
            {
                get
                {
                    return _threadSchedule;
                }
            }

            /// <summary>
            /// Stores the events relevant for the process under tracing.
            /// </summary>
            /// <param name="data"> Event parsed from the logs </param>
            internal void DiscardOrRecord(TRACING.TraceEvent data)
            {
                if (!_filters.TryGetValue(data.GetType(), out IEventFilter eventFilter))
                {
                    // Return immediately if no filter exists.
                    // TODO: Decide on either a default classifier (i.e. use ThreadID property)
                    //       or throw an exception/log the problem and open API to register classifiers that
                    //       implement IEventFilter.
                    return;
                }

                if (eventFilter.IsRelevant(data, out int relevantThread))
                {
                    // Log into linear event timeline
                    if (!_events.TryGetValue(data.TimeStampRelativeMSec, out List<TRACING.TraceEvent> events))
                    {
                        _events.Add(data.TimeStampRelativeMSec, new List<TRACING.TraceEvent>());
                    }

                    _events[data.TimeStampRelativeMSec].Add(data.Clone());

                    // Log into per thread timeline
                    if (!_threadSchedule.TryGetValue(relevantThread, out SortedList<double, TRACING.TraceEvent> relevantThreadTimeline))
                    {
                        _threadSchedule.Add(relevantThread, new SortedList<double, TRACING.TraceEvent>());
                    }

                    // Try to add the event to the timeline. If there's a time conflict try to add it at the next nanosecond.
                    double time = data.TimeStampRelativeMSec;
                    while (_threadSchedule[relevantThread].TryGetValue(time, out _))
                    {
                        time += 1e-6;
                    }
                    _threadSchedule[relevantThread].Add(time, data.Clone());
                }
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

        /// <summary>
        /// Data holder for performance metrics querying
        /// </summary>
        /// <param name="data"> ProcessTraceData for the event under examination </param>
        /// <param name="events"> ETWEventsHolder with the handled events </param>
        public ETWData(PARSERS.Kernel.ProcessTraceData data, ETWEventsHolder events)
        {
            PidUnderTest = data.ProcessID;
            TimeBase = data.TimeStampRelativeMSec;

            Analyze(events);
        }
        
        /// <summary>
        /// Cache and preprocess necessary elements 
        /// </summary>
        /// <param name="events"> Container used to store elements </param>
        private void Analyze(ETWEventsHolder events)
        {
            _threadSchedule = events.ThreadSchedule;

            foreach (var threadInfo in _threadSchedule)
            {
                int threadID = threadInfo.Key;
                SortedList <double, TRACING.TraceEvent> threadEventList = threadInfo.Value;

                // TODO: Determine if we really want to persist schedule. Could be generated in a lazy manner
                //       as the per thread sorted event list renders this trivial.
                // For a thread to exist, we must have found one element
                Debug.Assert(threadEventList.Count > 0);

                double startTime = threadEventList.Values[0].TimeStampRelativeMSec;
                double endTime = threadEventList.Values[threadEventList.Count - 1].TimeStampRelativeMSec;
                _threadLifetimes.Add(threadID, new ETWTimeInterval(this, startTime, endTime));

                // RecordJit(eventList); -> generic method works better with predicate and linq -> getEvent(, predicate)
                // TODO: Determine if we want to perform caching or separation of events. Could be done here.
            }

            Debug.Assert(_threadSchedule.Keys.Count == _threadLifetimes.Keys.Count); 
        }

        internal int PidUnderTest { get; private set; }

        internal double TimeBase { get; private set; }

        /// <summary>
        /// Get a dictionary of events relevant to a thread sorted by time using the thread number as key
        /// </summary>
        public Dictionary<int, SortedList<double, TRACING.TraceEvent>> ThreadSchedule => _threadSchedule;

        /// <summary>
        /// Returns a list of the threads used by the process.
        /// </summary>
        public Dictionary<int, SortedList<double, TRACING.TraceEvent>>.KeyCollection ThreadList
        {
            get
            {
                return _threadSchedule.Keys;
            }
        }
    }
}
