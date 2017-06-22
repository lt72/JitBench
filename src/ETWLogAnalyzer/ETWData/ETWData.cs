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
        public class MethodUniqueIdentifier
        {
            public long MethodId { get; private set; }
            public string FullyQualifiedName { get; private set; }

            public MethodUniqueIdentifier(long methodId, string fullyQualifiedName)
            {
                MethodId = methodId;
                FullyQualifiedName = fullyQualifiedName;
            }

            public MethodUniqueIdentifier(PARSERS.Clr.MethodJittingStartedTraceData jitEv)
            {
                MethodId = jitEv.MethodID;
                FullyQualifiedName = $"{jitEv.MethodNamespace}::{jitEv.MethodName}";
            }

            public MethodUniqueIdentifier(PARSERS.Clr.MethodLoadUnloadVerboseTraceData jitEv)
            {
                MethodId = jitEv.MethodID;
                FullyQualifiedName = $"{jitEv.MethodNamespace}::{jitEv.MethodName}";
            }

            public override int GetHashCode()
            {
                return MethodId.GetHashCode() ^ FullyQualifiedName.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }

                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                return GetHashCode() == obj.GetHashCode();
            }

            public override string ToString()
            {
                return $"{FullyQualifiedName} (MethodID {MethodId})";
            }
        }

        private readonly Dictionary<int, SortedList<double, TRACING.TraceEvent>> _threadSchedule;

        private readonly SortedList<double, TRACING.TraceEvent> _overallEvents;

        private readonly Dictionary<MethodUniqueIdentifier, int> _methodToThreadMap;

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

        private Dictionary<MethodUniqueIdentifier, int> GetMethodToThreadCache()
        {
            return _overallEvents.Values
                .Where(ev => ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData)
                .Select(ev => ev as PARSERS.Clr.MethodLoadUnloadVerboseTraceData)
                .ToDictionary(x => new MethodUniqueIdentifier(x), x => x.ThreadID);
        }

        /// <summary>
        /// Returns true if the process jitted a method with the given (ID, name) tuple, false otherwise
        /// </summary>
        /// <param name="methodUniqueIdentifier"> (identifier, fully quallified name) pair for method </param>
        /// <param name="threadId"> int identifier of the jitting thread </param>
        /// <returns> true if method jitted by process </returns>
        public bool GetJittingThreadForMethod(MethodUniqueIdentifier methodUniqueIdentifier, out int threadId)
        {
            return _methodToThreadMap.TryGetValue(methodUniqueIdentifier, out threadId);
        }

        /// <summary>
        /// Retrieve a list of methods jitted by the process.
        /// </summary>
        /// <returns> list of(identifier, fully quallified name) pair for methods jitted</returns>
        public List<MethodUniqueIdentifier> GetJittedMethodsList() => _methodToThreadMap.Keys.ToList();

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
