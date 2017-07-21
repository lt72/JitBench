using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.ETWLogAnalyzer.Abstractions;
using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    /// <summary>
    /// This visitor calculates the available time for jitting as described by the documentation.
    /// </summary>
    public class AvailableQuantumAccumulatorVisitor : EventVisitor<Dictionary<MethodUniqueIdentifier, double>>
    {
        /// <summary>
        /// Different states that the visitor will find itself in as it receives events.
        /// </summary>
        private enum InternalState { Ready, JitRunning, JitFinished };
        private static readonly List<Type> RelevantTypes = new List<Type> {
            typeof(PARSERS.Clr.MethodJittingStartedTraceData),
            typeof(PARSERS.Kernel.CSwitchTraceData),
            typeof(PARSERS.Clr.MethodLoadUnloadVerboseTraceData) };
        // TODO: Research call to get this dynamically. https://aka.ms/perf_blog might be useful. Issue #23
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
            Result = new Dictionary<MethodUniqueIdentifier, double>();
            AddRelevantTypes(RelevantTypes);
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            Debug.Assert(State != VisitorState.Error);
            if (ev is PARSERS.Clr.MethodJittingStartedTraceData jitStartEv)
            {
                _internalState = InternalState.JitRunning;
                _lastSwitchinOrJitStart = jitStartEv.TimeStampRelativeMSec;
                _methodJitting = jitStartEv;
                _accumulator = 0.0;
                return;
            }
            else if (ev is PARSERS.Kernel.CSwitchTraceData cSwitchEv)
            {
                if (cSwitchEv.OldThreadID == _threadId) // Context switch out
                {
                    _accumulator += cSwitchEv.TimeStampRelativeMSec - _lastSwitchinOrJitStart; // Used time

                    double wastedQuantum = QuantumLength - (cSwitchEv.TimeStampRelativeMSec - _lastSwitchIn);
                    // Adjust for quantums extended by the system.
                    _accumulator += (wastedQuantum > 0) ? wastedQuantum : 0;
                }
                else
                {
                    // We always need to remember the context switch so we can remove that part of the quantum used for calculations.
                    _lastSwitchinOrJitStart = cSwitchEv.TimeStampRelativeMSec;
                    _lastSwitchIn = cSwitchEv.TimeStampRelativeMSec;
                }
                return;
            }
            else if (ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData jitEndEv)
            {
                if (_internalState != InternalState.JitRunning || jitEndEv.MethodID != _methodJitting.MethodID)
                {
                    // If we don't have a matching jitting started, we've hit a bug and the code must be revised.
                    Debug.Assert(false, "Method end doesn't match last seen start");
                    State = VisitorState.Error;
                    return;
                }

                _accumulator += jitEndEv.TimeStampRelativeMSec - _lastSwitchinOrJitStart;
                Result.Add(new MethodUniqueIdentifier(jitEndEv), _accumulator);

                _internalState = InternalState.JitFinished;
                _methodJitting = null;
                return;
            }
            State = VisitorState.Error;
        }
    }
}
