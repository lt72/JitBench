using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
            private readonly SortedList<double, TRACING.TraceEvent> _events;
            /// <summary>
            /// Schedule of all the events that affect a given thread.
            /// </summary>
            private readonly Dictionary<int, SortedList<double, TRACING.TraceEvent>> _threadSchedule;
            /// <summary>
            /// Set of filters that can be used to determine if an event is relevant to the thread.
            /// </summary>
            private readonly Dictionary<Type, IEventFilter> _filters;

            internal IList<TRACING.TraceEvent> EventSchedule { get => _events.Values; }

            internal Dictionary<int, SortedList<double, TRACING.TraceEvent>> ThreadSchedule { get => _threadSchedule; }
            
            internal ETWEventsHolder(int pid)
            {
                _pidUnderTest = pid;
                _events = new SortedList<double, TRACING.TraceEvent>();
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
                    { typeof(PARSERS.Clr.AssemblyLoadUnloadTraceData), threadIDFilter }
                    // TODO: Add ready thread filter
                };
            }

            /// <summary>
            /// Stores the events relevant for the process under tracing.
            /// </summary>
            /// <param name="data"> Event traced by the parser </param>
            /// <param name="eventFilter">
            ///     Optional parameter to use a specific filter to classify the event. If the filter is not
            ///     provided the program will try to find a proper one if registered, otherwise using one that
            ///     just classifies by process and thread id.
            /// </param>
            /// </summary>
            internal void StoreIfRelevant(TRACING.TraceEvent data, IEventFilter eventFilter = null)
            {
                // If we don't have a filter registered and we are not given one, assign the generic one.
                if (eventFilter == null && !_filters.TryGetValue(data.GetType(), out eventFilter))
                {
                    eventFilter = new ThreadIDFilter(_pidUnderTest);
                }

                if (eventFilter.IsRelevant(data, out List<int> relevantThreadList))
                {
                    TRACING.TraceEvent eventCopy = data.Clone();

                    // Log into linear event timeline
                    AddUniqueTime(_events, data);

                    // Log into per thread timeline
                    foreach (var relevantThread in relevantThreadList)
                    {
                        AddUniqueTime(GetThreadTimeline(relevantThread), data);
                    }
                }
            }

            // Helpers

            private SortedList<double, TRACING.TraceEvent> GetThreadTimeline(int relevantThread)
            {
                SortedList<double, TRACING.TraceEvent> relevantThreadTimeline;
                if (!_threadSchedule.TryGetValue(relevantThread, out relevantThreadTimeline))
                {
                    relevantThreadTimeline = new SortedList<double, TRACING.TraceEvent>();
                    _threadSchedule.Add(relevantThread, relevantThreadTimeline);
                }

                return relevantThreadTimeline;
            }

            /// <summary>
            /// Adds event in the next empty nanosecond. As it's extremely unlikely that this happens,
            /// this is a plausible approach. 
            /// </summary>
            /// <param name="eventList"></param>
            /// <param name=""></param>
            private void AddUniqueTime(SortedList<double, TRACING.TraceEvent> eventList, TRACING.TraceEvent ev)
            {
                Debug.Assert(eventList != null);
                Debug.Assert(ev != null);
                
                double time = ev.TimeStampRelativeMSec;

                // Try to add the event to the timeline. If there's a time conflict try to add it at the next nanosecond.
                while (eventList.ContainsKey(time))
                {
                    time += 1e-6;
                }

                eventList.Add(time, ev.Clone());
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

            public double Begin { get => _begin; }
            public double End { get => _owner.TimeBase + _duration; }
            public double Duration { get => _duration; }

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
            
            public override string ToString()
            {
                return $"[ {_begin} , {_duration} ]";
            }
        }

        private readonly Dictionary<int, ETWTimeInterval> _threadLifetimes = new Dictionary<int, ETWTimeInterval>();
        private readonly Dictionary<int, SortedList<double, TRACING.TraceEvent>> _threadSchedule;
        private readonly List<TRACING.TraceEvent> _overallEvents;

        internal int PidUnderTest { get; private set; }

        internal double TimeBase { get; private set; }

        /// <summary>
        /// Data holder for performance metrics querying
        /// </summary>
        /// <param name="data"> ProcessTraceData for the event under examination </param>
        /// <param name="events"> ETWEventsHolder with the handled events </param>
        public ETWData(PARSERS.Kernel.ProcessTraceData data, ETWEventsHolder events)
        {
            PidUnderTest = data.ProcessID;
            TimeBase = data.TimeStampRelativeMSec;
            _threadSchedule = events.ThreadSchedule;
            _overallEvents = events.EventSchedule.ToList();

            Analyze(events);
        }
        
        /// <summary>
        /// Cache and preprocess necessary elements 
        /// </summary>
        /// <param name="events"> Container used to store elements </param>
        private void Analyze(ETWEventsHolder events)
        {
            foreach (var threadInfo in _threadSchedule)
            {
                int threadID = threadInfo.Key;
                SortedList <double, TRACING.TraceEvent> threadEventList = threadInfo.Value;

                // For a thread to exist, we must have found one element in the queue.
                Debug.Assert(threadEventList.Count > 0);

                double startTime = threadEventList.Values[0].TimeStampRelativeMSec;
                double endTime = threadEventList.Values[threadEventList.Count - 1].TimeStampRelativeMSec;
                _threadLifetimes.Add(threadID, new ETWTimeInterval(this, startTime, endTime));
            }

            Debug.Assert(_threadSchedule.Keys.Count == _threadLifetimes.Keys.Count); 
        }

        // Query Methods
        
        /// <summary>
        /// Returns all the matching events of a thread that match the predicate.
        /// </summary>
        /// <param name="threadId"> Thread that the events should be extracted from </param>
        /// <param name="condition"> Predicate used as matching criteria </param>
        /// <returns> List of events that match the predicate </returns>
        public List<TRACING.TraceEvent> GetMatchingEventsForThread(int threadId, Predicate<TRACING.TraceEvent> condition)
        {
            if (!_threadSchedule.TryGetValue(threadId, out var threadEventList))
            {
                throw new ArgumentException($"Thread {threadId} is not relevant to the process under tracing");
            }

            return (from threadEvent in threadEventList.Values
                    where condition(threadEvent)
                    select threadEvent).ToList();
        }

        public List<TRACING.TraceEvent> GetMatchingEventsForProcess(Predicate<TRACING.TraceEvent> condition)
        {
            return (from threadEvent in _overallEvents
                    where condition(threadEvent)
                    select threadEvent).ToList();
        }
    }
}
