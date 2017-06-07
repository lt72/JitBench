using System;
using System.Collections.Generic;
using System.Diagnostics;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace MusicStore.ETWLogAnalyzer.EventFilters
{
    class IOFilter : IEventFilter
    {
        // TODO: Double check on opcode
        static TRACING.TraceEventOpcode READ = (TRACING.TraceEventOpcode)10;
        static TRACING.TraceEventOpcode READ_INIT = (TRACING.TraceEventOpcode)12;
        private int _pidUnderTest;
        private Dictionary<UInt64, int> _IRPToThread;

        public IOFilter(int pidUnderTest)
        {
            _pidUnderTest = pidUnderTest;
            _IRPToThread = new Dictionary<UInt64, int>();
        }

        public bool IsRelevant(TRACING.TraceEvent ev, out int relevantThread)
        {
            if (ev is PARSERS.Kernel.DiskIOInitTraceData ioStartEv)
            {
                // Remember the thread that started the request
                _IRPToThread[ioStartEv.Irp] = ioStartEv.ThreadID;
                relevantThread = ioStartEv.ThreadID;
                return ioStartEv.ProcessID == _pidUnderTest && ioStartEv.Opcode == READ_INIT;
            }
            else if (ev is PARSERS.Kernel.DiskIOTraceData ioFinishEv)
            {
                // Try to report the thread that issued the request matched by IRP
                // otherwise log the thread reported in the request finish event.
                if (!_IRPToThread.TryGetValue(ioFinishEv.Irp, out relevantThread))
                {
                    _IRPToThread.Remove(ioFinishEv.Irp);
                }
                else
                {
                    relevantThread = ioFinishEv.ThreadID;
                }
                return ioFinishEv.ProcessID == _pidUnderTest && ioFinishEv.Opcode == READ;
            }

            // This means we passed a non io event to this filter...
            relevantThread = -1;
            Debug.Assert(false);
            return false;
        }
    }
}
