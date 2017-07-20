using System;
using System.Collections.Generic;

using TRACING = Microsoft.Diagnostics.Tracing;
using Microsoft.ETWLogAnalyzer.Abstractions;

namespace Microsoft.ETWLogAnalyzer.ReportVisitors
{
    public class GetCountEventsBetweenStartStopEventsPairVisitor<S,E,T> : EventVisitor<long>
        where S : TRACING.TraceEvent
        where E : TRACING.TraceEvent
    {
        private bool _active;
        private bool _done;
        private readonly bool _checkOpcode;

        public GetCountEventsBetweenStartStopEventsPairVisitor(bool checkOpcode) : base()
        {
            _active = false;
            _done = false;
            _checkOpcode = checkOpcode;
            Result = 0;

            AddRelevantTypes(new List<Type> { typeof(T), typeof(S), typeof(E) });
        }

        public override void Visit(TRACING.TraceEvent ev)
        {
            if(_done)
            {
                return;
            }

            if (_active == false && ev.GetType() != typeof(S))
            {
                return;
            }

            //
            // if start events are nested, reset the total count.
            //
            if(_active == true && ev.GetType() == typeof(S))
            {
                if (MarksStart(ev))
                {
                    Result = 0;
                }
            }

            if (ev.GetType() == typeof(S))
            {
                if (MarksStart(ev))
                {
                    _active = true;
                }
            }

            if (ev.GetType() == typeof(T))
            {
                Result += 1;
            }

            if (ev.GetType() == typeof(E))
            {
                if (MarksStop(ev))
                {
                    _done = true;
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
