﻿using Microsoft.ETWLogAnalyzer.Abstractions;
using System.Collections.Generic;
using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.Framework.EventFilters
{
    /// <summary>
    /// Classifies ready thread events to see if the awakened process is the process under test
    /// </summary>
    class ReadyThreadFilter : IEventFilter
    {
        private int _pid;

        public ReadyThreadFilter(int pid)
        {
            _pid = pid;
        }

        public bool IsRelevant(TRACING.TraceEvent ev, out List<int> relevantThreadList)
        {
            var readyThreadEv = ev as PARSERS.Kernel.DispatcherReadyThreadTraceData;
            
            if (readyThreadEv.AwakenedProcessID == _pid)
            {
                relevantThreadList = new List<int> { readyThreadEv.AwakenedThreadID };
                return true;
            }

            relevantThreadList = null;
            return false;
        }
    }
}
