using System;
using System.Collections.Generic;
using Microsoft.ETWLogAnalyzer.Abstractions;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

namespace Microsoft.ETWLogAnalyzer.Framework
{
    /// <summary>
    /// Data model for JIT performance analysis.
    /// </summary>
    public sealed class ETWData  : IEventModel
    {
        private readonly Dictionary<int, SortedList<double, TRACING.TraceEvent>> _threadSchedule;
        private readonly SortedList<double, TRACING.TraceEvent> _overallEvents;
        private readonly Dictionary<MethodUniqueIdentifier, int> _methodToThreadMap;
        public int TestTarget { get => ProcessStart.ProcessID; }
        public double TimeBase { get => ProcessStart.TimeStampRelativeMSec; }
        public ProcessTraceData ProcessStart { get; private set; }
        public ProcessTraceData ProcessStop { get; private set; }

        /// <summary>
        /// For serialization purposes only.
        /// </summary>
        private ETWData()
        { }

        /// <summary>
        /// Constructor for ETWData.
        /// </summary>
        /// <param name="procStart"> Process start event </param>
        /// <param name="procStop"> Process stop event </param>
        /// <param name="events"> ETWEventsHold holding the classified data by thread. </param>
        public ETWData(ProcessTraceData procStart, PARSERS.Kernel.ProcessTraceData procStop, Helpers.ETWEventsHolder events)
        {
            ProcessStart = procStart;
            ProcessStop = procStop;
            _threadSchedule = events.ThreadSchedule;
            _overallEvents = events.EventSchedule;
            _methodToThreadMap = GetMethodToThreadCache();
        }

        /// <summary>
        /// Returns true if the process jitted a method with the given identifier, false otherwise
        /// </summary>
        /// <param name="methodUniqueIdentifier"> identifier for method </param>
        /// <param name="threadId"> int identifier of the jitting thread </param>
        /// <returns> True if method jitted by the process under examination, false otherwise </returns>
        public bool GetJittingThreadForMethod(MethodUniqueIdentifier methodUniqueIdentifier, out int threadId)
        {
            return _methodToThreadMap.TryGetValue(methodUniqueIdentifier, out threadId);
        }

        /// <summary>
        /// Retrieve a IEnumerable of methods jitted by the process.
        /// </summary>
        public IEnumerable<MethodUniqueIdentifier> JittedMethodsList => _methodToThreadMap.Keys;

        /// <summary>
        /// Retrieve a list of threads used by the process.
        /// </summary>
        public IEnumerable<int> ThreadList => _threadSchedule.Keys;

        /// <summary>
        /// Gets an IEnumerator to a given thread's timeline, or throws ArgumentException if the thread isn't used by the process.
        /// </summary>
        /// <param name="threadId"> Thread ID of the thread to get the event timeline for. </param>
        /// <returns> IEnumerator of the thread's timeline </returns>
        public IEnumerator<TRACING.TraceEvent> GetThreadTimeline(int threadId)
        {
            if (!_threadSchedule.TryGetValue(threadId, out var threadTimeline))
            {
                throw new ArgumentException($"Process {TestTarget} didn't use thread {threadId}.");
            }

            return threadTimeline.Values.GetEnumerator();
        }

        /// <summary>
        /// Generates a cache of the jitting thread for each method.
        /// </summary>
        /// <returns> Dictionary that maps method identifiers to thread id's </returns>
        private Dictionary<MethodUniqueIdentifier, int> GetMethodToThreadCache()
        {
            var methodToThreadCache = new Dictionary<MethodUniqueIdentifier, int>();
            foreach (var ev in _overallEvents.Values)
            {
                if (ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData loadVerbEv)
                {
                    var methodUniqueId = new MethodUniqueIdentifier(loadVerbEv);

                    if (!methodToThreadCache.ContainsKey(methodUniqueId))
                    {
                        methodToThreadCache.Add(methodUniqueId, loadVerbEv.ThreadID);
                    }
                    else
                    {
                        Console.WriteLine($"Method {methodUniqueId.ToString()} jitted twice.\n\t" +
                            $"First in thread {methodToThreadCache[methodUniqueId]}, and again in thread {loadVerbEv.ThreadID}.");
                    }
                }
            }
            return methodToThreadCache;
        }
    }
}
