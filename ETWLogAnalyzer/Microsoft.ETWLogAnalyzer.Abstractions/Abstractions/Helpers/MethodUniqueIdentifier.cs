using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public sealed class MethodUniqueIdentifier : IConstructable<MethodUniqueIdentifier, MethodLoadUnloadVerboseTraceData>
    {
        private long _methodId;
        private long _moduleId;
        private string _fullyQualName;
        private int _methodToken;
        private long _reJitId;

        public long MethodId { get => _methodId; }
        public string FullyQualifiedName { get => _fullyQualName; }
        public int MethodToken { get => _methodToken; }
        public long ModuleId { get => _moduleId; }
        public long ReJITID { get => _reJitId; }

        public MethodUniqueIdentifier()
        {
            _methodToken = default(int);
            _moduleId = default(long);
            _methodId = default(long);
            _reJitId = default(long);
            _fullyQualName = default(string);
        }

        public MethodUniqueIdentifier Create(MethodLoadUnloadVerboseTraceData jitEv)
        {
            _methodToken = jitEv.MethodToken;
            _moduleId = jitEv.ModuleID;
            _methodId = jitEv.MethodID;
            _fullyQualName = $"{jitEv.MethodNamespace}.{jitEv.MethodName}";
            _reJitId = jitEv.ReJITID;
            return this;
        }

        public MethodUniqueIdentifier(MethodLoadUnloadVerboseTraceData jitEv)
        {
            _methodToken = jitEv.MethodToken;
            _moduleId = jitEv.ModuleID;
            _methodId = jitEv.MethodID;
            _reJitId = jitEv.ReJITID;
            _fullyQualName = $"{jitEv.MethodNamespace}.{jitEv.MethodName}";
        }

        public override int GetHashCode()
        {
            return _methodId.GetHashCode() 
                ^ _fullyQualName.GetHashCode()
                ^ _moduleId.GetHashCode()
                ^ _reJitId.GetHashCode()
                ^ _methodToken.GetHashCode();
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
