using TRACING = Microsoft.Diagnostics.Tracing;

namespace MusicStore.ETWLogAnalyzer.Visitors
{
    public interface IEventVisitor<T>
    {
        void Visit(TRACING.TraceEvent ev);
        
        bool IsRelevant(TRACING.TraceEvent ev);

        bool Result(out T result);
    }
}
