using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    /// <summary>
    /// This structure holds basic information needed to identify a method.
    /// </summary>
    public sealed class MethodUniqueIdentifier : IConstructable<MethodUniqueIdentifier, MethodLoadUnloadVerboseTraceData>
    {
        public long MethodId { get; private set; }
        public string FullyQualifiedName { get; private set; }
        public int MethodToken { get; private set; }
        public long ModuleId { get; private set; }
        public long ReJITID { get; private set; }
        private bool _isDefaultConstructed;

        /// <summary>
        /// Default constructor. Sets the fields to defaults; this is to be used as a placeholder
        /// until Create can be used.
        /// </summary>
        public MethodUniqueIdentifier()
        {
            MethodToken = default(int);
            ModuleId = default(long);
            MethodId = default(long);
            ReJITID = default(long);
            FullyQualifiedName = default(string);
            _isDefaultConstructed = true;
        }

        public MethodUniqueIdentifier(MethodLoadUnloadVerboseTraceData jitEv)
        {
            _isDefaultConstructed = true;
            Create(jitEv);
        }

        /// <summary>
        /// Modifier method to allow a default object to be changed to identify the method jitted. If the 
        /// identifier was not default constructed or this method was previously called this method can't be used
        /// and will throw an InvalidOperationException.
        /// </summary>
        /// <param name="jitEv"> Method to change the modifier to identify </param>
        /// <returns> Reference to the identifier itself. </returns>
        public MethodUniqueIdentifier Create(MethodLoadUnloadVerboseTraceData jitEv)
        {
            if (!_isDefaultConstructed)
            {
                throw new System.InvalidOperationException("This method can only be called on default initialized identifiers.");
            }

            FullyQualifiedName = $"{jitEv.MethodNamespace}.{jitEv.MethodName}";
            MethodToken = jitEv.MethodToken;
            ModuleId = jitEv.ModuleID;
            MethodId = jitEv.MethodID;
            ReJITID = jitEv.ReJITID;
            _isDefaultConstructed = false;
            return this;
        }

        public override int GetHashCode()
        {
            return MethodId.GetHashCode() 
                ^ FullyQualifiedName.GetHashCode()
                ^ ModuleId.GetHashCode()
                ^ ReJITID.GetHashCode()
                ^ MethodToken.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            var castObj = obj as MethodUniqueIdentifier;
            return MethodId == castObj.MethodId
                && ModuleId == castObj.ModuleId
                && ReJITID == castObj.ReJITID
                && MethodToken == castObj.MethodToken
                && FullyQualifiedName == castObj.FullyQualifiedName;
        }

        public override string ToString()
        {
            return $"{FullyQualifiedName} [ReJITID {ReJITID}] (MethodID {MethodId}, MethodToken {MethodToken}, ModuleId {ModuleId})";
        }
    }
}
