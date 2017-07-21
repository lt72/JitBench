using System;
using System.Collections.Generic;
using Microsoft.ETWLogAnalyzer.Abstractions;
using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    /// <summary>
    /// This visitor classifies the unscheduled time that falls between a method's JittingStarted and LoadVerbose
    /// as one of: idle time, I/O time, non-I/O unscheduled time.
    /// </summary>
    public class UnscheduledTimeClassifierVisitor : EventVisitor<Dictionary<MethodUniqueIdentifier, (double idleTime, double ioTime, double otherUnscheduledTime)>>
    {
        private bool _withinJit;
        private bool _isIO;
        private double _methodIOTime;
        private double _methodIdleTime;
        private double _methodOtherUnschedTime;
        private double _lastEventTime;
        private int _threadID;
        private PARSERS.Clr.MethodJittingStartedTraceData _methodJitting;

        public UnscheduledTimeClassifierVisitor(int threadId)
        {
            AddRelevantTypes(new List<Type> {
                typeof(PARSERS.Kernel.CSwitchTraceData),
                typeof(PARSERS.Clr.MethodJittingStartedTraceData),
                typeof(PARSERS.Clr.MethodLoadUnloadVerboseTraceData),
                typeof(PARSERS.Kernel.DispatcherReadyThreadTraceData),
                typeof(PARSERS.Kernel.DiskIOInitTraceData)
            });
            Result = new Dictionary<MethodUniqueIdentifier, (double idleTime, double ioTime, double otherUnscheduledTime)>();
            _threadID = threadId;
            _withinJit = false;
            _isIO = false;
            _methodJitting = null;
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (ev is PARSERS.Clr.MethodJittingStartedTraceData jitStartEv)
            {
                _withinJit = true;
                _isIO = false;
                _methodIdleTime = 0.0;
                _methodIOTime = 0.0;
                _methodOtherUnschedTime = 0.0;
                _methodJitting = jitStartEv;
                return;
            }

            if (!_withinJit)
            {
                return;
            }

            if (ev is PARSERS.Kernel.DispatcherReadyThreadTraceData readyThreadEv)
            {
                if (_isIO)
                {
                    _methodIOTime += (readyThreadEv.TimeStampRelativeMSec - _lastEventTime);
                }
                else
                {
                    _methodOtherUnschedTime += (readyThreadEv.TimeStampRelativeMSec - _lastEventTime);
                }

                // Update state so we can calculate idle time.
                _lastEventTime = readyThreadEv.TimeStampRelativeMSec;
                return;
            }

            if (ev is PARSERS.Kernel.CSwitchTraceData cSwitchEv)
            {
                if (cSwitchEv.NewThreadID == _threadID)
                {
                    // Switched in again to jit. Accumulate the idle time since last ReadyThread event.
                    _methodIdleTime += (cSwitchEv.TimeStampRelativeMSec - _lastEventTime); 
                    _isIO = false;
                }
                else
                {
                    // A context swith out's time is needed to calculate both I/O and non I/O unscheduled times.
                    _lastEventTime = cSwitchEv.TimeStampRelativeMSec;
                }

                return;
            }

            if (ev is PARSERS.Kernel.DiskIOInitTraceData readInitEv)
            {
                _isIO = true;
                return;
            }

            if (ev is PARSERS.Clr.MethodLoadUnloadVerboseTraceData jitEndEv)
            {
                if (jitEndEv.MethodID != _methodJitting.MethodID)
                {
                    System.Diagnostics.Debug.Assert(false, $"LoadVerbose event doesn't match method that started jitting. " +
                        $"LoadVerbose Method ID: {jitEndEv.MethodID}, Jitting method ID: {_methodJitting.MethodID}");
                    State = VisitorState.Error;
                    return;
                }

                _withinJit = false;
                Result.Add(new MethodUniqueIdentifier(jitEndEv), (_methodIdleTime, _methodIOTime, _methodOtherUnschedTime));
                return;
            }

            System.Diagnostics.Debug.Assert(false, "Visitor received unexpected event.");
            State = VisitorState.Error;
        }
    }
}

