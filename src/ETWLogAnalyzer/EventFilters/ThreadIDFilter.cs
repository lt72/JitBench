using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace MusicStore.ETWLogAnalyzer.EventFilters
{
    internal class ThreadIDFilter : IEventFilter
    {
        private int _pid;

        internal ThreadIDFilter(int pid)
        {
            _pid = pid;
        }

        bool IEventFilter.IsRelevant(TRACING.TraceEvent ev, out int relevantThread)
        {
            relevantThread = ev.ThreadID;
            return ev.ProcessID == _pid;
        }
    }
}
