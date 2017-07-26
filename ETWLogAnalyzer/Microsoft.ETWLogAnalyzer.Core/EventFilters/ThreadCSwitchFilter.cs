using System.Collections.Generic;
using Microsoft.ETWLogAnalyzer.Abstractions;
using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.Framework.EventFilters
{
    /// <summary>
    /// Classifies the process's context switches into both the leaving thread and the entering thread as necessary.
    /// </summary>
    public class ThreadCSwitchFilter : IEventFilter
    {
        private int _pid;

        public  ThreadCSwitchFilter(int pid)
        {
            _pid = pid;
        }

        public bool IsRelevant(TRACING.TraceEvent ev, out List<int> relevantThreadList)
        {
            var castEvent = ev as PARSERS.Kernel.CSwitchTraceData;
            relevantThreadList = new List<int>();

            if (castEvent.NewProcessID == _pid) // Our process gets switched in.
            {
                relevantThreadList.Add(castEvent.NewThreadID);
            }

            if (castEvent.OldProcessID == _pid)
            {
                relevantThreadList.Add(castEvent.OldThreadID);
            }

            return relevantThreadList.Count > 0;  
        }
    }
}
