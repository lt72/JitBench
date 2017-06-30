using System;
using System.Collections.Generic;
using TRACING = Microsoft.Diagnostics.Tracing;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public abstract class EventVisitor<R>
    {
        public enum VisitorState { Continue, Done, Error };

        private readonly List<Type> _relevantTypes;

        public VisitorState State { get; protected set; }

        public EventVisitor()
        {
            _relevantTypes = new List<Type>();
            State = VisitorState.Continue;

            Result = default(R);
        }

        public abstract void Visit(TRACING.TraceEvent ev);

        public virtual bool IsRelevant(TRACING.TraceEvent ev)
        {
            foreach (var type in _relevantTypes)
            {
                if (ev.GetType() == type || ev.GetType().IsSubclassOf(type))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual R Result { get; protected set; }

        protected void AddRelevantTypes(List<Type> relevantTypesToAdd)
        {
            _relevantTypes.AddRange(relevantTypesToAdd);
        }

        public R DefaultResult { get { return default(R); } }
    }
}
