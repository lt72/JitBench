using System;
using System.Collections.Generic;
using System.Diagnostics;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.ETWLogAnalyzer.Abstractions;

namespace Microsoft.ETWLogAnalyzer.Framework.EventFilters
{
    /// <summary>
    /// Filter for I/O events.
    /// </summary>
    public class IOFilter : IEventFilter
    {
        private int _pidUnderTest;
        private Dictionary<UInt64, int> _IRPToThread;

        public IOFilter(int pidUnderTest)
        {
            _pidUnderTest = pidUnderTest;
            _IRPToThread = new Dictionary<UInt64, int>();
        }

        /// <summary>
        /// I/O events some times are completed in another thread, so they must be matched by the IRP 
        /// back to the thread that initiated the request. This filter chaches the information and returns
        /// if it's relevant.
        /// </summary>
        /// <param name="ev"> I/O event </param>
        /// <param name="relevantThreadList"> Threads that initiated the I/O </param>
        /// <exception cref=""> Event is not I/O reading events end up in argument invalid exceptions. </exception>
        /// <returns> True if the I/O belongs </returns>
        public bool IsRelevant(TRACING.TraceEvent ev, out List<int> relevantThreadList)
        {
            if (ev is PARSERS.Kernel.DiskIOInitTraceData ioStartEv)
            {
                relevantThreadList = new List<int>();
                // Remember the thread that started the request
                _IRPToThread[ioStartEv.Irp] = ioStartEv.ThreadID;
                relevantThreadList.Add(ioStartEv.ThreadID);
                return ioStartEv.ProcessID == _pidUnderTest;
            }
            else if (ev is PARSERS.Kernel.DiskIOTraceData ioFinishEv)
            {
                relevantThreadList = new List<int>();
                // Try to report the thread that issued the request matched by IRP
                // otherwise log the thread reported in the request finish event.
                if (!_IRPToThread.TryGetValue(ioFinishEv.Irp, out int relevantThreadId))
                {
                    _IRPToThread.Remove(ioFinishEv.Irp);
                }
                else
                {
                    relevantThreadId = ioFinishEv.ThreadID;
                }
                relevantThreadList.Add(relevantThreadId);
                return ioFinishEv.ProcessID == _pidUnderTest;
            }

            throw new ArgumentException($"Event {ev} is not an I/O event.");
        }
    }
}
