using System.Collections.Generic;
using Microsoft.ETWLogAnalyzer.Abstractions;
using TRACING = Microsoft.Diagnostics.Tracing;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    /// <summary>
    /// Collects events of a given type in a window delimited by two types of events.
    /// </summary>
    /// <typeparam name="S"> Event type that denotes start of the window. </typeparam>
    /// <typeparam name="E"> Event type that denotes end of the window. Must be different from S. </typeparam>
    /// <typeparam name="T"> Type of event to be collected </typeparam>
    /// <typeparam name="K"> Key to map the collection from the window to. Must implement IConstructable&lt;K, E&gt; and have a default constructor.</typeparam>
    public class CollectEventsInWindowVisitor<S, E, T, K> : EventVisitor<Dictionary<K, List<T>>>
        where S : TRACING.TraceEvent
        where E : TRACING.TraceEvent
        where T : TRACING.TraceEvent
        where K : IConstructable<K, E>, new()
    {
        private readonly bool _checkOpcode;
        private bool _withinWindow;
        private List<T> _partialCollection;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="checkOpcode"> Set to true if S and E are marked with TraceEventOpcode.Start and TraceEventOpcode.End and must be checked. </param>
        public CollectEventsInWindowVisitor(bool checkOpcode = false)
        {
            _checkOpcode = checkOpcode;
            _withinWindow = false;
            Result = new Dictionary<K, List<T>>();
            AddRelevantTypes(new List<System.Type> { typeof(S), typeof(T), typeof(E) });
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (ev is S)
            {
                _partialCollection = new List<T>();
                _withinWindow = true;
                return;
            }

            if (!_withinWindow)
            {
                return;
            }

            if (ev is T evAsT)
            {
                _partialCollection.Add(evAsT);
                return;
            }

            if (ev is E evAsE)
            {
                Result.Add(new K().Create(evAsE), _partialCollection);
                _partialCollection = null;
                _withinWindow = false;
                return;
            }

            System.Diagnostics.Debug.Assert(false, $"Unexpected type {ev.GetType().Name} reached the visitor.");
            State = VisitorState.Error;
        }

        /// <summary>
        /// Checks if the event has a start opcode if the visitor is constructed to check.
        /// </summary>
        /// <param name="ev"> Event </param>
        /// <returns> True if unchecked or if it's checked and has start opcode </returns>
        private bool MarksStart(TRACING.TraceEvent ev)
        {
            return !_checkOpcode || (_checkOpcode && ev.Opcode == TRACING.TraceEventOpcode.Start);
        }

        /// <summary>
        /// Checks if the event has a stop opcode if the visitor is constructed to check.
        /// </summary>
        /// <param name="ev"> Event </param>
        /// <returns> True if unchecked or if it's checked and has stop opcode </returns>
        private bool MarksStop(TRACING.TraceEvent ev)
        {
            return !_checkOpcode || (_checkOpcode && ev.Opcode == TRACING.TraceEventOpcode.Stop);
        }
    }
}
