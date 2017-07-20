using System;
using System.Collections.Generic;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.ETWLogAnalyzer.EventFilters;
using Microsoft.ETWLogAnalyzer.Abstractions;
using System.Diagnostics;

namespace Microsoft.ETWLogAnalyzer.Framework.Helpers
{
    /// <summary>
    /// Holder for events. The only purpose is to sanitize and classify data so that it can be used
    /// to instantiate an ETWData object.
    /// </summary>
    public class ETWEventsHolder
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
        /// <summary>
        /// Overall event timeline.
        /// </summary>
        internal SortedList<double, TRACING.TraceEvent> EventSchedule { get => _events; }
        /// <summary>
        /// Event schedule by thread sorted by time.
        /// </summary>
        internal Dictionary<int, SortedList<double, TRACING.TraceEvent>> ThreadSchedule { get => _threadSchedule; }

        /// <summary>
        /// Initializes the fields and sets IEventFilters based on the event's type to correctly classify it into the 
        /// right thread timelines.
        /// </summary>
        /// <param name="pid"> PID of the process to trace </param>
        public ETWEventsHolder(int pid)
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
                    { typeof(PARSERS.Kernel.MemoryHardFaultTraceData), threadIDFilter},
                    { typeof(PARSERS.Kernel.DiskIOInitTraceData), ioFilter},
                    { typeof(PARSERS.Kernel.DiskIOTraceData), ioFilter},
                    { typeof(PARSERS.Clr.MethodJittingStartedTraceData), threadIDFilter },
                    { typeof(PARSERS.Clr.MethodLoadUnloadVerboseTraceData), threadIDFilter },
                    { typeof(PARSERS.Clr.AssemblyLoadUnloadTraceData), threadIDFilter },
                    { typeof(PARSERS.Kernel.DispatcherReadyThreadTraceData), new ReadyThreadFilter(_pidUnderTest) }
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
        public void StoreIfRelevant(TRACING.TraceEvent data, IEventFilter eventFilter = null)
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
        /// <summary>
        /// Gets a thread timeline, or creates it if it doesn't exist yet.
        /// </summary>
        /// <param name="relevantThread"> Thread ID of the timeline needed. </param>
        /// <returns> Timeline as a sorted list of events. </returns>
        private SortedList<double, TRACING.TraceEvent> GetThreadTimeline(int relevantThread)
        {
            if (!_threadSchedule.TryGetValue(relevantThread, out SortedList<double, TRACING.TraceEvent> relevantThreadTimeline))
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
        /// <param name="eventList"> List to add the event to. </param>
        /// <param name="ev"> Event to add. </param>
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
}
