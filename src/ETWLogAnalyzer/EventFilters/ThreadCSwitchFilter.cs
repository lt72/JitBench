using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace MusicStore.ETWLogAnalyzer.EventFilters
{
    internal class ThreadCSwitchFilter : IEventFilter
    {
        private int _pid;

        internal ThreadCSwitchFilter(int pid)
        {
            _pid = pid;
        }

        bool IEventFilter.IsRelevant(TRACING.TraceEvent ev, out int relevantThread)
        {
            var castEvent = ev as PARSERS.Kernel.CSwitchTraceData;
            
            if (castEvent.NewProcessID == _pid) // Our process gets switched in.
            {
                relevantThread = castEvent.NewThreadID;
                return true;
            }

            // We assign the out thread, and determine if the relevant process is switched out.
            relevantThread = castEvent.OldThreadID; 
            return castEvent.OldProcessID == _pid;  
        }
    }
}
