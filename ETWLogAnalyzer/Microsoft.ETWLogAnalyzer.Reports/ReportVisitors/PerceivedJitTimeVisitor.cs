using System;
using System.Collections.Generic;
using Microsoft.ETWLogAnalyzer.Abstractions;
using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    /// <summary>
    /// This visitor calculates the time between the JittingStarted event and the Method/LoadVerbose
    /// events for each method.
    /// </summary>
    public class PerceivedJitTimeVisitor : EventVisitor<Dictionary<MethodUniqueIdentifier, double>>
    {
        private static readonly List<Type> RelevantTypes = new List<Type> {
            typeof(PARSERS.Clr.MethodJittingStartedTraceData),
            typeof(PARSERS.Clr.MethodLoadUnloadVerboseTraceData) };

        private readonly int _threadId;
        private readonly Dictionary<long, PARSERS.Clr.MethodJittingStartedTraceData> _methodToJitStart;
        
        public PerceivedJitTimeVisitor(int threadId) : base()
        {
            Result = new Dictionary<MethodUniqueIdentifier, double>();
            _methodToJitStart = new Dictionary<long, PARSERS.Clr.MethodJittingStartedTraceData>();
            _threadId = threadId;
            AddRelevantTypes(RelevantTypes);
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (ev is PARSERS.Clr.MethodJittingStartedTraceData jitStartEv)
            {
                _methodToJitStart.Add(jitStartEv.MethodID, jitStartEv);
            }
            else if (ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData jitEndEv)
            {
                if (!_methodToJitStart.TryGetValue(jitEndEv.MethodID, out var matchJitStart))
                {
                    System.Diagnostics.Debug.Assert(false, "Unmatched LoadVerbose event found on thread.");
                    State = VisitorState.Error;
                    return;
                }

                _methodToJitStart.Remove(jitEndEv.MethodID);
                Result.Add(new MethodUniqueIdentifier(jitEndEv), 
                    jitEndEv.TimeStampRelativeMSec - matchJitStart.TimeStampRelativeMSec);
            }
        }
    }
}
