using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ETWLogAnalyzer.Abstractions;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

namespace Microsoft.ETWLogAnalyzer.Framework
{
    public sealed class ETWData  : IEventModel
    {
        private ProcessTraceData _processStart;
        
        private ProcessTraceData _processStop;

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
            _processStart = procStart;
            _processStop = procStop;
            _threadSchedule = events.ThreadSchedule;
            _overallEvents = events.EventSchedule;
            _methodToThreadMap = GetMethodToThreadCache();
        }

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


        public int TestTarget { get => _processStart.ProcessID; }

        public double TimeBase { get => _processStart.TimeStampRelativeMSec; }

        public ProcessTraceData ProcessStart { get => _processStart; }

        public ProcessTraceData ProcessStop { get => _processStop; }

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
        public List<MethodUniqueIdentifier> GetJittedMethodsList => _methodToThreadMap.Keys.ToList();

        /// <summary>
        /// Retrieve a list of threads used by the process.
        /// </summary>
        /// <returns> list of threads used by the process </returns>
        public List<int> GetThreadList => _threadSchedule.Keys.ToList();

        public IEnumerator<TRACING.TraceEvent> GetThreadTimeline(int threadId)
        {
            if (!_threadSchedule.TryGetValue(threadId, out var threadTimeline))
            {
                throw new ArgumentException($"Process {TestTarget} didn't use thread {threadId}.");
            }

            return threadTimeline.Values.GetEnumerator();
        }
    }
}
