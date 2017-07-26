using Microsoft.ETWLogAnalyzer.Abstractions;
using System.Collections.Generic;
using TRACING = Microsoft.Diagnostics.Tracing;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    /// <summary>
    /// Visitor that collects the events of a given type.
    /// </summary>
    /// <typeparam name="T"> Event type to collect. </typeparam>
    public class CollectEventsVisitor<T> : EventVisitor<List<T>>
        where T : TRACING.TraceEvent
    {
        public CollectEventsVisitor()
        {
            Result = new List<T>();
            AddRelevantTypes(new List<System.Type> { typeof(T) });
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (ev is T evAsT)
            {
                Result.Add(evAsT);
                return;
            }

            State = VisitorState.Error;
        }
    }
}
