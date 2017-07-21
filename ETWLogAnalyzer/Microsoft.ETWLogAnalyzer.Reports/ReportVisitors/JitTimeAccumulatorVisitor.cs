using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ETWLogAnalyzer.Abstractions;
using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    /// <summary>
    /// Calculates the jitting time of a thread by method.
    /// </summary>
    public class JitTimeAccumulatorVisitor : EventVisitor<Dictionary<MethodUniqueIdentifier, double>>
    {
        private enum InternalState { Ready, JitRunning, JitFinished };
        
        private static readonly List<Type> RelevantTypes = new List<Type> {
            typeof(PARSERS.Clr.MethodJittingStartedTraceData),
            typeof(PARSERS.Kernel.CSwitchTraceData),
            typeof(PARSERS.Clr.MethodLoadUnloadVerboseTraceData) };
        
        private InternalState _internalState;
        private PARSERS.Clr.MethodJittingStartedTraceData _methodJitting;
        private double _lastStart;
        private readonly int _threadId;
        private double _accumulator;

        public JitTimeAccumulatorVisitor(int threadId) : base()
        {
            Result = new Dictionary<MethodUniqueIdentifier, double>();
            _internalState = InternalState.Ready;
            _lastStart = 0;
            _threadId = threadId;
            AddRelevantTypes(RelevantTypes);
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (ev is PARSERS.Clr.MethodJittingStartedTraceData jitStartEv)
            {
                _internalState = InternalState.JitRunning;
                _lastStart = jitStartEv.TimeStampRelativeMSec;
                _methodJitting = jitStartEv;
                _accumulator = 0.0;
            }
            else if (ev is PARSERS.Kernel.CSwitchTraceData cSwitchEv && _internalState == InternalState.JitRunning)
            {
                if (cSwitchEv.OldThreadID == _threadId) // Context switch out
                {
                    _accumulator += cSwitchEv.TimeStampRelativeMSec - _lastStart;
                }
                else
                {
                    _lastStart = cSwitchEv.TimeStampRelativeMSec;
                }
            }
            else if (ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData jitEndEv)
            {
                if (_internalState != InternalState.JitRunning || jitEndEv.MethodID != _methodJitting.MethodID)
                {
                    Debug.Assert(false, "Method jitted doesn't match with the event start.");
                    State = VisitorState.Error;
                    return;
                }

                _accumulator += jitEndEv.TimeStampRelativeMSec - _lastStart;
                Result.Add(new MethodUniqueIdentifier(jitEndEv), _accumulator);

                _internalState = InternalState.JitFinished;
                _methodJitting = null;
            }
        }
    }
}
