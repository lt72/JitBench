using System;
using System.Collections.Generic;

using TRACING = Microsoft.Diagnostics.Tracing;
using Microsoft.ETWLogAnalyzer.Abstractions;
using System.Diagnostics;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    public class GetCountEventsBetweenAllStartStopEventsPairVisitor<S,E,T,K> : EventVisitor<Dictionary<K,long>>
        where S : TRACING.TraceEvent
        where E : TRACING.TraceEvent
        where K : IConstructable<K, E>, new()
    {
        private bool _active;
        private long _curCount;
        private readonly bool _checkOpcode;

        public GetCountEventsBetweenAllStartStopEventsPairVisitor(bool checkOpcode) : base()
        {
            _active = false;
            _checkOpcode = checkOpcode;
            Result = new Dictionary<K, long>();

            AddRelevantTypes(new List<Type> { typeof(T), typeof(S), typeof(E) });
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if (_active == false && ev.GetType() != typeof(S))
            {
                return;
            }

            if (ev.GetType() == typeof(S))
            {
                if (MarksStart(ev))
                {
                    _curCount = 0;
                }
            }

            //
            // if started events are nested, reset the total count.
            //
            if(_active == true && ev.GetType() == typeof(S))
            {
                if (MarksStart(ev))
                {
                    _curCount = 0;
                }
            }

            if (_active == false && ev.GetType() == typeof(S))
            {
                if (MarksStart(ev))
                {
                    _active = true;
                }
            }

            if (ev.GetType() == typeof(T))
            {
                _curCount += 1;
            }

            if (ev is E evAsE)
            {
                if (MarksStop(ev))
                {
                    _active = false;
                    Result.Add(new K().Create(evAsE), _curCount);
                }
            }
        }

        private bool MarksStart(TRACING.TraceEvent ev)
        {
            if (_checkOpcode == false || (_checkOpcode == true && ev.Opcode == TRACING.TraceEventOpcode.Start))
            {
                return true;
            }

            return false;
        }

        private bool MarksStop(TRACING.TraceEvent ev)
        {
            if (_checkOpcode == false || (_checkOpcode == true && ev.Opcode == TRACING.TraceEventOpcode.Stop))
            {
                return true;
            }

            return false;
        }
    }
}
