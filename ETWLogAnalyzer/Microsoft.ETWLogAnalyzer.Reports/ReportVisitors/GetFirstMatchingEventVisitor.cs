using System;
using System.Collections.Generic;

using TRACING = Microsoft.Diagnostics.Tracing;
using Microsoft.ETWLogAnalyzer.Abstractions;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    public class GetFirstMatchingEventVisitor<R> : EventVisitor<R> where R : TRACING.TraceEvent 
    {
        private static Predicate<R> DefaultTrue = x => true;
        private readonly Predicate<R> _matchingCondition;

        public GetFirstMatchingEventVisitor(Predicate<R> condition = null) : base()
        {
            _matchingCondition = condition ?? DefaultTrue;
            Result = null;

            AddRelevantTypes(new List<Type> { typeof(R) });
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (_matchingCondition((R)ev))
            {
                State = VisitorState.Done;
                Result = ev as R;
            }
        }
    }
}
