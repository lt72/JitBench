using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Collections.Generic;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    /// <summary>
    /// Interface all models for the ETWLogAnalysis framework must follow.
    /// </summary>
    public interface IEventModel
    {
        /// <summary>
        /// Process ID of the process under tracing.
        /// </summary>
        int TestTarget { get; }
        /// <summary>
        /// Relative time from which the process started running.
        /// </summary>
        double TimeBase { get; }
        /// <summary>
        /// Event that marks the start of the process under tracing.
        /// </summary>
        ProcessTraceData ProcessStart { get; }
        /// <summary>
        /// Event that marks the halt of the process under tracing.
        /// </summary>
        ProcessTraceData ProcessStop { get; }
        /// <summary>
        /// Accessor property for the list of identifiers of methods jitted by the process.
        /// </summary>
        IEnumerable<MethodUniqueIdentifier> JittedMethodsList { get; }
        /// <summary>
        /// Accessor propery for the list of threads used by the process under trace.
        /// </summary>
        IEnumerable<int> ThreadList { get; }

        /// <summary>
        /// Returns an IEnumerator relevant to the given thread ordered by time.
        /// </summary>
        /// <param name="threadId"> Thread to get a timeline for. </param>
        /// <returns> IEnumerator to thread's event timeline. </returns>
        IEnumerator<TraceEvent> GetThreadTimeline(int threadId);
        /// <summary>
        /// Gets the thread identifier for the thread that jitted the givne method.
        /// </summary>
        /// <param name="methodUniqueIdentifier"> Identifier of method to look for. </param>
        /// <param name="threadId"> Thread identifier of the jitting thread for the given method. </param>
        /// <returns> True if such method was jitted by the process, false otherwise. </returns>
        bool GetJittingThreadForMethod(MethodUniqueIdentifier methodUniqueIdentifier, out int threadId);
    }
}
