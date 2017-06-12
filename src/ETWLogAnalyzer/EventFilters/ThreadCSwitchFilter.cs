﻿using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using System.Collections.Generic;

namespace MusicStore.ETWLogAnalyzer.EventFilters
{
    internal class ThreadCSwitchFilter : IEventFilter
    {
        private int _pid;

        internal ThreadCSwitchFilter(int pid)
        {
            _pid = pid;
        }

        bool IEventFilter.IsRelevant(TRACING.TraceEvent ev, out List<int> relevantThreadList)
        {
            var castEvent = ev as PARSERS.Kernel.CSwitchTraceData;
            relevantThreadList = new List<int>();

            if (castEvent.NewProcessID == _pid) // Our process gets switched in.
            {
                relevantThreadList.Add(castEvent.NewThreadID);
            }

            if (castEvent.OldProcessID == _pid)
            {
                relevantThreadList.Add(castEvent.OldThreadID);
            }

            return relevantThreadList.Count > 0;  
        }
    }
}
