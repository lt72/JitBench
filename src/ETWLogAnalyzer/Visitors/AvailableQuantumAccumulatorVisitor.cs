using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace MusicStore.ETWLogAnalyzer.Visitors
{
    class AvailableQuantumAccumulatorVisitor : IEventVisitor<double>
    {
        private enum VisitorState { Uninitialized, JitRunning, JitFinished };
        private static readonly List<Type> RelevantTypes = new List<Type> {
            typeof(PARSERS.Clr.MethodJittingStartedTraceData),
            typeof(PARSERS.Kernel.CSwitchTraceData),
            typeof(PARSERS.Clr.MethodLoadUnloadVerboseTraceData) };
        private static readonly double QuantumLength = 20;

        private VisitorState _state;
        private double _accumulator = 0;
        private double _lastRelevantIntervalStart;
        private double _lastSwitchIn;
        private int _threadId;

        public AvailableQuantumAccumulatorVisitor(int threadId)
        {
            _state = VisitorState.Uninitialized;
            _lastRelevantIntervalStart = 0;
            _lastSwitchIn = 0;
            _threadId = threadId;
        }

        public bool IsRelevant(TRACING.TraceEvent ev)
        {
            return RelevantTypes.Contains(ev.GetType());
        }

        public void Visit(TRACING.TraceEvent ev)
        {
            if (ev is PARSERS.Clr.MethodJittingStartedTraceData jitStartEv)
            {
                System.Diagnostics.Debug.Assert(_state == VisitorState.JitFinished || _state == VisitorState.Uninitialized);
                _state = VisitorState.JitRunning;
                _lastRelevantIntervalStart = jitStartEv.TimeStampRelativeMSec;
            }
            else if (ev is PARSERS.Kernel.CSwitchTraceData cSwitchEv)
            {
                if (cSwitchEv.OldThreadID == _threadId) // Context switch out
                {
                    _accumulator += cSwitchEv.TimeStampRelativeMSec - _lastRelevantIntervalStart + ;
                }
                else
                {
                    // We always need to remember the context switch so we can remove that part of the quantum used for calculations.
                    _lastRelevantIntervalStart = cSwitchEv.TimeStampRelativeMSec;
                    _lastSwitchIn = cSwitchEv.TimeStampRelativeMSec;
                }
            }
            else if (ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData jitEndEv)
            {
                System.Diagnostics.Debug.Assert(_state == VisitorState.JitRunning);
                _state = VisitorState.JitFinished;
                _accumulator += jitEndEv.TimeStampRelativeMSec - _lastRelevantIntervalStart;
            }

            System.Diagnostics.Debug.Assert(false, $"Unxepected event of type {ev.GetType().Name} not filtered.");
        }

        public bool Result(out double result)
        {
            result = _accumulator;
            return _state == VisitorState.JitFinished;
        }
    }
}
