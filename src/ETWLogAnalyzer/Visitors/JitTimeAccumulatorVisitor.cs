using System;
using System.Collections.Generic;
using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace MusicStore.ETWLogAnalyzer.Visitors
{
    public class JitTimeAccumulatorVisitor : IEventVisitor<double>
    {
        private enum VisitorState { Uninitialized, JitRunning, JitFinished };
        private static readonly List<Type> RelevantTypes = new List<Type> {
            typeof(PARSERS.Clr.MethodJittingStartedTraceData),
            typeof(PARSERS.Kernel.CSwitchTraceData),
            typeof(PARSERS.Clr.MethodLoadUnloadVerboseTraceData) };
        
        private VisitorState _state;
        private double _accumulator = 0;
        private double _lastStart;
        private int _threadId;

        public JitTimeAccumulatorVisitor(int threadId)
        {
            _state = VisitorState.Uninitialized;
            _lastStart = 0;
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
                _lastStart = jitStartEv.TimeStampRelativeMSec;
            }
            else if (ev is PARSERS.Kernel.CSwitchTraceData cSwitchEv && _state == VisitorState.JitRunning)
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
                System.Diagnostics.Debug.Assert(_state == VisitorState.JitRunning);
                _state = VisitorState.JitFinished;
                _accumulator += jitEndEv.TimeStampRelativeMSec - _lastStart;
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
