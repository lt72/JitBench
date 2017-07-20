using System.Collections.Generic;
using TRACING = Microsoft.Diagnostics.Tracing;
using Microsoft.ETWLogAnalyzer.Abstractions;

namespace Microsoft.ETWLogAnalyzer.EventFilters
{
    /// <summary>
    /// Classifies as relevant by comparing by process ID and 
    /// returns only the ThreadID of the event as relevant. This classifies most
    /// events as needed. However, operations that might affect two threads shouldn't
    /// use this filter.
    /// </summary>
    public class ThreadIDFilter : IEventFilter
    {
        private int _pid;

        public ThreadIDFilter(int pid)
        {
            _pid = pid;
        }

        public bool IsRelevant(TRACING.TraceEvent ev, out List<int> relevantThreadList)
        {
            if (ev.ProcessID == _pid)
            {
                relevantThreadList = new List<int>();
                relevantThreadList.Add(ev.ThreadID);
                return true;
            }

            relevantThreadList = null;
            return false;
        }
    }
}
