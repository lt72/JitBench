using System;
using System.Collections.Generic;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.ETWLogAnalyzer.Abstractions;
using System.Diagnostics;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    public class UnscheduledTimeClassifierVisitor : EventVisitor<Dictionary<MethodUniqueIdentifier, (double idleTime, double ioTime, double otherUnscheduledTime)>>
    {
        private bool _withinJit;
        private bool _isIO;

        public UnscheduledTimeClassifierVisitor()
        {
            AddRelevantTypes(new List<Type> {
                typeof(PARSERS.Kernel.CSwitchTraceData),
                typeof(PARSERS.Clr.MethodJittingStartedTraceData),
                typeof(PARSERS.Clr.MethodLoadUnloadVerboseTraceData),
                typeof(PARSERS.Kernel.DispatcherReadyThreadTraceData),
                typeof(PARSERS.Kernel.DiskIOInitTraceData)
            });

            Result = new Dictionary<MethodUniqueIdentifier, (double idleTime, double ioTime, double otherUnscheduledTime)>();

            _withinJit = false;
            _isIO = false;
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (ev is PARSERS.Clr.MethodJittingStartedTraceData jitStartEv)
            {
                _withinJit = true;
                return;
            }

            if (!_withinJit)
            {
                return;
            }

            throw new NotImplementedException();
        }
    }
}

