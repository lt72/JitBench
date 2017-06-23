using System;
using System.Collections.Generic;
using System.Linq;
using MusicStore.ETWLogAnalyzer.Abstractions;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;


namespace MusicStore.ETWLogAnalyzer
{
    public class ETWData : EventModelBase
    {
        private readonly Dictionary<int, SortedList<double, TRACING.TraceEvent>> _threadSchedule;

        private readonly SortedList<double, TRACING.TraceEvent> _overallEvents;

        private readonly Dictionary<MethodUniqueIdentifier, int> _methodToThreadMap;

        /// <summary>
        /// Data holder for performance metrics querying
        /// </summary>
        /// <param name="data"> ProcessTraceData for the event under examination </param>
        /// <param name="events"> ETWEventsHolder with the handled events </param>
        public ETWData(PARSERS.Kernel.ProcessTraceData procStart, PARSERS.Kernel.ProcessTraceData procStop, Helpers.ETWEventsHolder events)
        {
            ProcessStart = procStart;
            ProcessStop = procStop;
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
        public override bool GetJittingThreadForMethod(MethodUniqueIdentifier methodUniqueIdentifier, out int threadId)
        {
            return _methodToThreadMap.TryGetValue(methodUniqueIdentifier, out threadId);
        }

        /// <summary>
        /// Retrieve a list of methods jitted by the process.
        /// </summary>
        /// <returns> list of(identifier, fully quallified name) pair for methods jitted</returns>
        public override List<MethodUniqueIdentifier> GetJittedMethodsList => _methodToThreadMap.Keys.ToList();

        /// <summary>
        /// Retrieve a list of threads used by the process.
        /// </summary>
        /// <returns> list of threads used by the process </returns>
        public override List<int> GetThreadList => _threadSchedule.Keys.ToList();

        public override IEnumerator<TRACING.TraceEvent> GetThreadTimeline(int threadId)
        {
            if (!_threadSchedule.TryGetValue(threadId, out var threadTimeline))
            {
                throw new ArgumentException($"Process {PidUnderTest} didn't use thread {threadId}.");
            }

            return threadTimeline.Values.GetEnumerator();
        }
    }
}
