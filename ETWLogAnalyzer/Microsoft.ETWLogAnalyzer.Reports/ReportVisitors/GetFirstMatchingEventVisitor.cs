using System;
using System.Collections.Generic;

using TRACING = Microsoft.Diagnostics.Tracing;
using Microsoft.ETWLogAnalyzer.Abstractions;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    public class GetFirstMatchingEventVisitor : EventVisitor<TRACING.TraceEvent>
    {
        private Predicate<TRACING.TraceEvent> _matchPredicate;
        public GetFirstMatchingEventVisitor(Predicate<TRACING.TraceEvent> condition) : base()
        {
            Result = null;
            AddRelevantTypes(new List<Type> { typeof(TRACING.TraceEvent) });
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (_matchPredicate(ev))
            {
                Result = ev;
                State = VisitorState.Done;
            }
        }
    }
}
