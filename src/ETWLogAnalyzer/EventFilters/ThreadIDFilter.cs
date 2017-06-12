using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using System.Collections.Generic;

namespace MusicStore.ETWLogAnalyzer.EventFilters
{
    internal class ThreadIDFilter : IEventFilter
    {
        private int _pid;

        internal ThreadIDFilter(int pid)
        {
            _pid = pid;
        }

        bool IEventFilter.IsRelevant(TRACING.TraceEvent ev, out List<int> relevantThreadList)
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
