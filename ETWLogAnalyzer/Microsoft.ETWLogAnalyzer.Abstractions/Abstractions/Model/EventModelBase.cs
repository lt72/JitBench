using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System.Collections.Generic;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public abstract class EventModelBase
    {
        public int PidUnderTest { get => ProcessStart.ProcessID; }

        public double TimeBase { get => ProcessStart.TimeStampRelativeMSec;  }

        public ProcessTraceData ProcessStart { get; protected set; }

        public ProcessTraceData ProcessStop { get; protected set; }

        public abstract List<MethodUniqueIdentifier> GetJittedMethodsList { get; }

        public abstract List<int> GetThreadList { get; }

        public abstract IEnumerator<TraceEvent> GetThreadTimeline(int threadId);

        public abstract bool GetJittingThreadForMethod(MethodUniqueIdentifier methodUniqueIdentifier, out int threadId);
    }
}
