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
        where K : IConstructable<K, S>, new()
    {
        private bool _active;
        private K _current;
        private readonly bool _checkOpcode;

        public GetCountEventsBetweenAllStartStopEventsPairVisitor(bool checkOpcode) : base()
        {
            _active = false;
            _current = default(K);
            _checkOpcode = checkOpcode;

            AddRelevantTypes(new List<Type> { typeof(T), typeof(S), typeof(E) });

            Result = new Dictionary<K, long>( );
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
                    _current = new K().Create((S)ev);

                    Debug.Assert(Result.ContainsKey(_current) == false);

                    Result.Add(_current, 0);
                }
            }

            //
            // if started events are nested, reset the total count.
            //
            if(_active == true && ev.GetType() == typeof(S))
            {
                if (MarksStart(ev))
                {
                    Debug.Assert(Result.ContainsKey(_current) == true);

                    Result[_current] = 0;
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
                Result[_current] += 1;
            }

            if (ev.GetType() == typeof(E))
            {
                if (MarksStop(ev))
                {
                    _active = false;
                    _current = default(K);
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
