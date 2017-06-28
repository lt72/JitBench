using System;
using System.Collections.Generic;

using TRACING = Microsoft.Diagnostics.Tracing;
using Microsoft.ETWLogAnalyzer.Abstractions;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    public class GetFirstEventOfTypeVisitor<T> : EventVisitor<T> where T : TRACING.TraceEvent 
    {

        public GetFirstEventOfTypeVisitor() : base()
        {
            Result = null;
            AddRelevantTypes(new List<Type> { typeof(T) });
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            State = VisitorState.Done;
            Result = ev as T;
        }
    }
}
