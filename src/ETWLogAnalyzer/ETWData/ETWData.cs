using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using MusicStore.ETWLogAnalyzer.EventFilters;

namespace MusicStore.ETWLogAnalyzer
{
    public class ETWData
    {
        private readonly Dictionary<int, SortedList<double, TRACING.TraceEvent>> _threadSchedule;

        private readonly SortedList<double, TRACING.TraceEvent> _overallEvents;

        private readonly Dictionary<Tuple<long, string>, int> _methodToThreadMap;

        public int PidUnderTest { get; private set; }

        public double TimeBase { get; private set; }

        /// <summary>
        /// Data holder for performance metrics querying
        /// </summary>
        /// <param name="data"> ProcessTraceData for the event under examination </param>
        /// <param name="events"> ETWEventsHolder with the handled events </param>
        public ETWData(PARSERS.Kernel.ProcessTraceData data, Helpers.ETWEventsHolder events)
        {
            PidUnderTest = data.ProcessID;
            TimeBase = data.TimeStampRelativeMSec;
            _threadSchedule = events.ThreadSchedule;
            _overallEvents = events.EventSchedule;
            _methodToThreadMap = GetMethodToThreadCache();
        }

        private Dictionary<Tuple<long, string>, int> GetMethodToThreadCache()
        {
            return _overallEvents.Values
                .Where(ev => ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData)
                .Select(ev => ev as PARSERS.Clr.MethodLoadUnloadVerboseTraceData)
                .ToDictionary(x => new Tuple<long, string>(x.MethodID, x.MethodName + x.MethodSignature), x => x.ThreadID);
        }

        /// <summary>
        /// Returns true if the process jitted a method with the given (ID, name) tuple, false otherwise
        /// </summary>
        /// <param name="methodIdNamePair"> (identifier, fully quallified name) pair for method </param>
        /// <param name="threadId"> int identifier of the jitting thread </param>
        /// <returns> true if method jitted by process </returns>
        public bool GetJittingThreadForMethod(Tuple<long, string> methodIdNamePair, out int threadId)
        {
            return _methodToThreadMap.TryGetValue(methodIdNamePair, out threadId);
        }

        /// <summary>
        /// Retrieve a list of methods jitted by the process.
        /// </summary>
        /// <returns> list of(identifier, fully quallified name) pair for methods jitted</returns>
        public List<Tuple<long, string>> GetJittedMethodsList() => _methodToThreadMap.Keys.ToList();

        /// <summary>
        /// Retrieve a list of threads used by the process.
        /// </summary>
        /// <returns> list of threads used by the process </returns>
        public List<int> GetThreadList() => _threadSchedule.Keys.ToList();

        public IEnumerator<TRACING.TraceEvent> GetThreadTimeline(int threadId)
        {
            if (!_threadSchedule.TryGetValue(threadId, out var threadTimeline))
            {
                throw new ArgumentException($"Process {PidUnderTest} didn't use thread {threadId}.");
            }

            return threadTimeline.Values.GetEnumerator();
        }

        // Query Methods
        
        /// <summary>
        /// Returns all the matching events of a thread that match the predicate.
        /// </summary>
        /// <param name="threadId"> Thread that the events should be extracted from </param>
        /// <param name="condition"> Predicate used as matching criteria </param>
        /// <returns> List of events that match the predicate </returns>
        private List<TRACING.TraceEvent> GetMatchingEventsForThread(int threadId, Predicate<TRACING.TraceEvent> condition)
        {
            if (!_threadSchedule.TryGetValue(threadId, out var threadEventList))
            {
                throw new ArgumentException($"Thread {threadId} is not relevant to the process under tracing");
            }

            return (from threadEvent in threadEventList.Values
                    where condition(threadEvent)
                    select threadEvent).ToList();
        }

        private List<TRACING.TraceEvent> GetMatchingEventsForProcess(Predicate<TRACING.TraceEvent> condition)
        {
            return (from threadEvent in _overallEvents.Values
                    where condition(threadEvent)
                    select threadEvent).ToList();
        }
    }
}
