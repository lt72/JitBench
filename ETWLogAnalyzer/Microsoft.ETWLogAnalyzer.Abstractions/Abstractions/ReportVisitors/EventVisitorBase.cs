using System;
using System.Collections.Generic;
using TRACING = Microsoft.Diagnostics.Tracing;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public abstract class EventVisitor<T>
    {
        public enum VisitorState { Continue, Done, Error };

        private readonly List<Type> _relevantTypes;

        public VisitorState State { get; protected set; }

        public EventVisitor()
        {
            _relevantTypes = new List<Type>();
            State = VisitorState.Continue;
        }

        public abstract void Visit(TRACING.TraceEvent ev);

        public virtual bool IsRelevant(TRACING.TraceEvent ev)
        {
            return _relevantTypes.Contains(ev.GetType());
        }

        public virtual T Result { get; protected set; }

        protected void AddRelevantTypes(List<Type> relevantTypesToAdd)
        {
            _relevantTypes.AddRange(relevantTypesToAdd);
        }
    }
}
