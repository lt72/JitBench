using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;

namespace MusicStore.ETWLogAnalyzer.EventFilters
{
    class DynamicFilter : IEventFilter
    {
        private int _pidUnderTest;

        public DynamicFilter(int pidUnderTest)
        {
            _pidUnderTest = pidUnderTest;
        }

        public bool IsRelevant(TraceEvent ev, out int relevantThread)
        {
            throw new NotImplementedException();
        }
    }
}
