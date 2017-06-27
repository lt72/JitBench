using System;
using System.Collections.Generic;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.ETWLogAnalyzer.Abstractions;
using System.Diagnostics;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
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
            _internalState = InternalState.Ready;
            _lastStart = 0;
            Result = new Dictionary<MethodUniqueIdentifier, double>();
            _threadId = threadId;
            AddRelevantTypes(RelevantTypes);
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            State = VisitorState.Continue;
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
                Debug.Assert(jitEndEv.MethodID == _methodJitting.MethodID);
                if (_internalState != InternalState.JitRunning)
                {
                    State = VisitorState.Error;
                    return;
                }

                _accumulator += jitEndEv.TimeStampRelativeMSec - _lastStart;
                Result.Add(new MethodUniqueIdentifier(_methodJitting), _accumulator);

                _internalState = InternalState.JitFinished;
                _methodJitting = null;
            }
        }
    }
}
