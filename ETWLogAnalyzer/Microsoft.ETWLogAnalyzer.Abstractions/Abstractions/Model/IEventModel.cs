using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Collections.Generic;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public interface IEventModel
    {
        int TestTarget { get; }

        double TimeBase { get; }

        ProcessTraceData ProcessStart { get; }

        ProcessTraceData ProcessStop { get; }

        List<MethodUniqueIdentifier> JittedMethodsList { get; }

        List<int> ThreadList { get; }

        IEnumerator<TraceEvent> GetThreadTimeline(int threadId);

        bool GetJittingThreadForMethod(MethodUniqueIdentifier methodUniqueIdentifier, out int threadId);
    }
}
