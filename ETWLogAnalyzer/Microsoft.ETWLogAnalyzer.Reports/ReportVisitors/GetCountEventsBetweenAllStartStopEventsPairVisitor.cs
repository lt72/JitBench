﻿using System;
using System.Collections.Generic;
using Microsoft.ETWLogAnalyzer.Abstractions;
using TRACING = Microsoft.Diagnostics.Tracing;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    /// <summary>
    /// Counts the number of a specific type of event in a window delimited by two types of events.
    /// </summary>
    /// <typeparam name="S"> Event type that denotes start of the window. </typeparam>
    /// <typeparam name="E"> Event type that denotes end of the window. Must be different from S. </typeparam>
    /// <typeparam name="T"> Type of event to be counted </typeparam>
    /// <typeparam name="K"> Key to map the count in a window to. Must implement IConstructable&lt;K, E&gt; and have a default constructor.</typeparam>
    public class GetCountEventsBetweenAllStartStopEventsPairVisitor<S,E,T,K> : EventVisitor<Dictionary<K,long>>
        where S : TRACING.TraceEvent
        where E : TRACING.TraceEvent
        where K : IConstructable<K, E>, new()
    {
        private bool _active;
        private long _curCount;
        private readonly bool _checkOpcode;
        private static Predicate<T> DefaultTrue = x => true;
        private readonly Predicate<T> _matchingCondition;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="checkOpcode"> Set to true if S and E are marked with TraceEventOpcode.Start and TraceEventOpcode.End and must be checked. </param>
        public GetCountEventsBetweenAllStartStopEventsPairVisitor(bool checkOpcode = false, Predicate<T> matchingCriteria = null) : base()
        {
            Result = new Dictionary<K, long>();
            AddRelevantTypes(new List<Type> { typeof(T), typeof(S), typeof(E) });
            _active = false;
            _checkOpcode = checkOpcode;
            _matchingCondition = matchingCriteria ?? DefaultTrue;
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if(ev is S && MarksStart(ev))
            {
                _curCount = 0;
                _active = true;
                return;
            }

            if (!_active)
            {
                // Ignore elements not in the window.
                return;
            }

            if (ev is T evAsT && _matchingCondition(evAsT))
            {
                _curCount += 1;
            }

            if (ev is E evAsE && MarksStop(ev))
            {
                _active = false;
                Result.Add(new K().Create(evAsE), _curCount);
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
