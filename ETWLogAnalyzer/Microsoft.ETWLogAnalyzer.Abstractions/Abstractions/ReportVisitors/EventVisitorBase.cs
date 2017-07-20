using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    /// <summary>
    /// Valid states for a visitor. The controller will halt feeding the events to the visitor if the 
    /// state is not Continue.
    /// </summary>
    public enum VisitorState { Continue, Done, Error };

    /// <summary>
    /// All visitors used by the framework must extend this class.
    /// It provides an API that the Controller will use to run them safely on model.
    /// </summary>
    /// <typeparam name="R"> Result type to be obtained from the object at the end. </typeparam>
    public abstract class EventVisitor<R>
    {
        /// <summary>
        /// Relevant event types to the Visitor.
        /// </summary>
        private readonly List<Type> _relevantTypes;
        /// <summary>
        /// State accesor for the visitor. For behavior related to state check the documentation of VisitorState.
        /// </summary>
        public VisitorState State { get; protected set; }
        /// <summary>
        /// Property to access the Result of the visitor. If the State is Error, the result can not be verified and 
        /// might represent only a random value or a partial result.
        /// </summary>
        public virtual R Result { get; protected set; }
        /// <summary>
        /// Gives the default value of the result for comparison.
        /// </summary>
        public R DefaultResult { get { return default(R); } }

        protected EventVisitor()
        {
            _relevantTypes = new List<Type>();
            State = VisitorState.Continue;
            Result = default(R);
        }

        /// <summary>
        /// This method will receive the relevant events in the timeline in chronological order. This method
        /// must process the data, and update State and Result accordingly.
        /// </summary>
        /// <param name="ev"> Current event in the timeline that the object is visiting </param>
        public abstract void Visit(TraceEvent ev);

        /// <summary>
        /// This method determined if an event is relevant to the visitor. This method will be called by the 
        /// controller to determine if it should be fed into visit. Override only is you need a more complex behavior than
        /// filtering by type.
        /// </summary>
        /// <param name="ev"> Event under evaluation </param>
        /// <returns> True if the event is relevant to the this visitor, false otherwise. </returns>
        public virtual bool IsRelevant(TraceEvent ev)
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

        /// <summary>
        /// Use this method to register the types that the visitor. Only types that are registered
        /// will be fed into visit, unless IsRelevant was overriden. 
        /// </summary>
        /// <param name="relevantTypesToAdd"> List of types to register as relevant. </param>
        protected void AddRelevantTypes(List<Type> relevantTypesToAdd)
        {
#if DEBUG
            foreach(Type type in relevantTypesToAdd)
            {
                System.Diagnostics.Debug.Assert((type == typeof(TraceEvent) || type.IsSubclassOf(typeof(TraceEvent))));
            }
#endif
            _relevantTypes.AddRange(relevantTypesToAdd);
        }        
    }
}
