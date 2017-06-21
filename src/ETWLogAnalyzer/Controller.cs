using System.Collections.Generic;
using TRACING = Microsoft.Diagnostics.Tracing;
using MusicStore.ETWLogAnalyzer.AbstractBases;

namespace MusicStore.ETWLogAnalyzer
{
    internal static class Controller
    {
        internal static void RunVisitorForResult<T>(EventVisitor<T> visitor, IEnumerator<TRACING.TraceEvent> iterator)
        {
            while (iterator.MoveNext() && visitor.State == EventVisitor<T>.VisitorState.Continue)
            {
                if (visitor.IsRelevant(iterator.Current))
                {
                    visitor.Visit(iterator.Current);
                }
            }
        }
    }
}
