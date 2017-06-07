using TRACING = Microsoft.Diagnostics.Tracing;

namespace MusicStore.ETWLogAnalyzer.EventFilters
{
    internal interface IEventFilter
    {
        bool IsRelevant(TRACING.TraceEvent ev, out int relevantThread);
    }
}
