using System;
using System.Collections.Generic;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.ETWLogAnalyzer.Abstractions;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    public class UnscheduledTimeClassifierVisitor : EventVisitor<Dictionary<MethodUniqueIdentifier, (double idleTime, double ioTime, double otherUnscheduledTime)>>
    {
        private bool _withinJit;
        private bool _isIO;
        private double _methodIOTime;
        private double _methodIdleTime;
        private double _methodOtherUnschedTime;
        private double _lastEventTime;
        private int _threadID;

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
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (ev is PARSERS.Clr.MethodJittingStartedTraceData jitStartEv)
            {
                _withinJit = true;
                _isIO = false;
                _methodIdleTime = 0;
                _methodIOTime = 0;
                _methodOtherUnschedTime = 0;
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

                _lastEventTime = readyThreadEv.TimeStampRelativeMSec;
                return;
            }

            if (ev is PARSERS.Kernel.CSwitchTraceData cSwitchEv)
            {
                if (cSwitchEv.NewThreadID == _threadID) // Switched in
                {
                    _methodIdleTime += (cSwitchEv.TimeStampRelativeMSec - _lastEventTime);
                    _isIO = false;
                }
                else
                {
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
                _withinJit = false;
                Result.Add(new MethodUniqueIdentifier(jitEndEv), (_methodIdleTime, _methodIOTime, _methodOtherUnschedTime));
            }
        }
    }
}

