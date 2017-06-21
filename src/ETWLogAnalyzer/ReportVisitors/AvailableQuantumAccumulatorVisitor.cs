using System;
using System.Collections.Generic;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using MusicStore.ETWLogAnalyzer.AbstractBases;

namespace MusicStore.ETWLogAnalyzer.ReportVisitors
{
    class AvailableQuantumAccumulatorVisitor : EventVisitor<Dictionary<ETWData.MethodUniqueIdentifier, double>>
    {
        private enum InternalState { Ready, JitRunning, JitFinished };
        private static readonly List<Type> RelevantTypes = new List<Type> {
            typeof(PARSERS.Clr.MethodJittingStartedTraceData),
            typeof(PARSERS.Kernel.CSwitchTraceData),
            typeof(PARSERS.Clr.MethodLoadUnloadVerboseTraceData) };
        private static readonly double QuantumLength = 20;

        private InternalState _internalState;
        private PARSERS.Clr.MethodJittingStartedTraceData _methodJitting;
        private double _accumulator = 0;
        private double _lastSwitchinOrJitStart;
        private double _lastSwitchIn;
        private int _threadId;

        public AvailableQuantumAccumulatorVisitor(int threadId) : base()
        {
            _internalState = InternalState.Ready;
            _lastSwitchinOrJitStart = 0;
            _lastSwitchIn = 0;
            _threadId = threadId;
            Result = new Dictionary<ETWData.MethodUniqueIdentifier, double>();
            AddRelevantTypes(RelevantTypes);
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            System.Diagnostics.Debug.Assert(State != VisitorState.Error);
            if (ev is PARSERS.Clr.MethodJittingStartedTraceData jitStartEv)
            {
                _internalState = InternalState.JitRunning;
                _lastSwitchinOrJitStart = jitStartEv.TimeStampRelativeMSec;
                _methodJitting = jitStartEv;
            }
            else if (ev is PARSERS.Kernel.CSwitchTraceData cSwitchEv)
            {
                if (cSwitchEv.OldThreadID == _threadId) // Context switch out
                {
                    _accumulator += cSwitchEv.TimeStampRelativeMSec - _lastSwitchinOrJitStart; // Used time

                    System.Diagnostics.Debug.Assert(QuantumLength - (cSwitchEv.TimeStampRelativeMSec - _lastSwitchIn) >= 0);
                    _accumulator += QuantumLength - (cSwitchEv.TimeStampRelativeMSec - _lastSwitchIn); // Wasted quantum available for jitting
                }
                else
                {
                    // We always need to remember the context switch so we can remove that part of the quantum used for calculations.
                    _lastSwitchinOrJitStart = cSwitchEv.TimeStampRelativeMSec;
                    _lastSwitchIn = cSwitchEv.TimeStampRelativeMSec;
                }
            }
            else if (ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData jitEndEv)
            {
                System.Diagnostics.Debug.Assert(jitEndEv.MethodID == _methodJitting.MethodID);
                if (_internalState != InternalState.JitRunning)
                {
                    State = VisitorState.Error;
                    return;
                }

                _accumulator += jitEndEv.TimeStampRelativeMSec - _lastSwitchinOrJitStart;
                string fullyQualName = _methodJitting.MethodNamespace + _methodJitting.MethodName;
                Result.Add(
                    new ETWData.MethodUniqueIdentifier(_methodJitting.MethodID, fullyQualName), _accumulator);

                _internalState = InternalState.JitFinished;
                _accumulator = 0;
                _methodJitting = null;
            }
        }
    }
}
