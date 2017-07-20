using System.Collections.Generic;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    /// <summary>
    /// This interface is used to determine what type of threads are relevant for a given event.
    /// </summary>
    public interface IEventFilter
    {
        /// <summary>
        /// This method will determine if the event is relevant to the process under tracing and what threads it
        /// is relevant to.
        /// </summary>
        /// <param name="ev"> Event to evaluate for relevance. </param>
        /// <param name="relevantThread"> Threads that are relevant for this thread. Normally there will be one or two. </param>
        /// <returns> True if the event is relevant to the process, false otherwise. </returns>
        bool IsRelevant(Microsoft.Diagnostics.Tracing.TraceEvent ev, out List<int> relevantThreadList);
    }
}
