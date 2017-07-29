using System;
using System.Collections.Generic;
using Microsoft.ETWLogAnalyzer.Abstractions;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    /// <summary>
    /// Counts the number of a context switches out in a jitting window if the quantum had not been consumed.
    /// </summary>
    public class UnnecessaryContextSwitchesVisitor: EventVisitor<Dictionary<MethodUniqueIdentifier, long>>
    {
        // TODO: Research call to get this dynamically. https://aka.ms/perf_blog might be useful. Issue #23
        private static readonly double QuantumLength = 20;
        private int _threadId;
        private bool _active;
        private long _curCount;
        private CSwitchTraceData _cachedSwitch;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="checkOpcode"> Set to true if S and E are marked with TraceEventOpcode.Start and TraceEventOpcode.End and must be checked. </param>
        public UnnecessaryContextSwitchesVisitor(int threadId) : base()
        {
            Result = new Dictionary<MethodUniqueIdentifier, long>();
            AddRelevantTypes(new List<Type> { typeof(MethodJittingStartedTraceData), typeof(MethodLoadUnloadVerboseTraceData), typeof(CSwitchTraceData) });
            _threadId = threadId;
            _active = false;
        }

        public override void Visit(TraceEvent ev)
        {
            if (ev is MethodJittingStartedTraceData)
            {
                _active = true;
                _curCount = 0;
                return;
            }

            if (ev is CSwitchTraceData evAsCs)
            {
                if (evAsCs.NewThreadID == _threadId) // Constext Switch in
                {
                    _cachedSwitch = evAsCs;
                }
                // We are context switched out. See if we are switched out before we consume the quantum. 
                else if (evAsCs.TimeStampRelativeMSec - _cachedSwitch.TimeStampRelativeMSec < QuantumLength && _active)
                {
                    _curCount++;
                }
                return;
            }

            if (!_active)
            {
                return;
            }

            if (ev is MethodLoadUnloadVerboseTraceData evAsLoadVerbose)
            {
                _active = false;
                Result.Add(new MethodUniqueIdentifier(evAsLoadVerbose), _curCount);
                return;
            }

            State = VisitorState.Error;
            return;
        }
    }
}
