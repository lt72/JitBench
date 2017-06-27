using System.Collections.Generic;
using TRACING = Microsoft.Diagnostics.Tracing;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public interface IEventFilter
    {
        /// <summary>
        /// This method will determine if the event is relevant to the process under tracing
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="relevantThread"> Outs the relevant thread. In case there's more than one thread  </param>
        /// <returns></returns>
        bool IsRelevant(TRACING.TraceEvent ev, out List<int> relevantThreadList);
    }
}
