using System;
using System.Collections.Generic;

using TRACING = Microsoft.Diagnostics.Tracing;
using Microsoft.ETWLogAnalyzer.Abstractions;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    public class GetFirstMatchingEventVisitor<T> : EventVisitor<T> where T : TRACING.TraceEvent 
    {
        private static Predicate<T> DefaultTrue = x => true;
        private readonly Predicate<T> _matchingCondition;

        public GetFirstMatchingEventVisitor(Predicate<T> condition = null) : base()
        {
            _matchingCondition = condition ?? DefaultTrue;
            Result = null;
            AddRelevantTypes(new List<Type> { typeof(T) });
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (_matchingCondition((T)ev))
            {
                State = VisitorState.Done;
                Result = ev as T;
            }
        }
    }
}
