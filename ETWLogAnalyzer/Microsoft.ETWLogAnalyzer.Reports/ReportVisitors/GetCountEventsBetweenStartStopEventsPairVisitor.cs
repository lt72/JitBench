using System;
using System.Collections.Generic;
using Microsoft.ETWLogAnalyzer.Abstractions;
using TRACING = Microsoft.Diagnostics.Tracing;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    /// <summary>
    /// Counts the events of a given type that fall within the first window of the start-stop events.
    /// </summary>
    /// <typeparam name="S"> Event type that denotes start of the window. </typeparam>
    /// <typeparam name="E"> Event type that denotes end of the window. Must be different from S by at least the opcode. </typeparam>
    /// <typeparam name="T"> Type of event to be counted </typeparam>
    public class GetCountEventsBetweenStartStopEventsPairVisitor<S,E,T> : EventVisitor<long>
        where S : TRACING.TraceEvent
        where E : TRACING.TraceEvent
    {
        private bool _active;
        private readonly bool _checkOpcode;

        public GetCountEventsBetweenStartStopEventsPairVisitor(bool checkOpcode) : base()
        {
            _active = false;
            _checkOpcode = checkOpcode;
            Result = 0;
            AddRelevantTypes(new List<Type> { typeof(T), typeof(S), typeof(E) });
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if(State == VisitorState.Done || (!_active && ev.GetType() != typeof(S)))
            {
                // Skip if done or if event is not in the window.
                return;
            }

            if(ev.GetType() == typeof(S) && MarksStart(ev))
            {
                if (_active)
                {
                    // Reset the count if the start events are nested.
                    Result = 0;
                }
                _active = true;
            }

            if (ev.GetType() == typeof(T))
            {
                Result += 1;
            }

            if (ev.GetType() == typeof(E) && MarksStop(ev))
            {
                State = VisitorState.Done;
            }
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
        /// Checks if the event has a start opcode if the visitor is constructed to check.
        /// </summary>
        /// <param name="ev"> Event </param>
        /// <returns> True if unchecked or if it's checked and has start opcode </returns>
        private bool MarksStop(TRACING.TraceEvent ev)
        {
            return !_checkOpcode || (_checkOpcode && ev.Opcode == TRACING.TraceEventOpcode.Stop);
        }
    }
}
