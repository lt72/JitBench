using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ETWLogAnalyzer.Abstractions;
using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    public class ILSizeVisitor : EventVisitor<Dictionary<MethodUniqueIdentifier, int>>
    {
        PARSERS.Clr.MethodJittingStartedTraceData _lastMethodJitting;
        public ILSizeVisitor():base()
        {
            Result = new Dictionary<MethodUniqueIdentifier, int>();
            AddRelevantTypes(new List<Type> { typeof(PARSERS.Clr.MethodJittingStartedTraceData), typeof(PARSERS.Clr.MethodLoadUnloadVerboseTraceData) });
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (ev is PARSERS.Clr.MethodJittingStartedTraceData jitStartEv)
            {
                _lastMethodJitting = jitStartEv;
                return;
            }
            else if (ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData jitEndEv)
            {
                if (_lastMethodJitting.MethodID != jitEndEv.MethodID)
                {
                    Debug.Assert(false, "Method jitting doesn't match last jitting method.");
                    State = VisitorState.Error;
                    return;
                }
                Result.Add(new MethodUniqueIdentifier(jitEndEv), _lastMethodJitting.MethodILSize);
                return;
            }

            State = VisitorState.Error;
            return;
        }
    }
}
