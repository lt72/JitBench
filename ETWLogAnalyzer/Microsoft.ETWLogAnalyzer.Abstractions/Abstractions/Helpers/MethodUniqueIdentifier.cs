using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public sealed class MethodUniqueIdentifier
    {
        private readonly long _methodId;
        private readonly long _moduleId;
        private readonly string _fullyQualName;
        private readonly int _methodToken;

        public long MethodId { get => _methodId; }

        public string FullyQualifiedName { get => _fullyQualName; }

        public int MethodToken { get => _methodToken; }

        public long ModuleId { get => _moduleId; }

        public MethodUniqueIdentifier(PARSERS.Clr.MethodJittingStartedTraceData jitEv)
        {
            _methodToken = jitEv.MethodToken;
            _moduleId = jitEv.ModuleID;
            _methodId = jitEv.MethodID;
            _fullyQualName = $"{jitEv.MethodNamespace}.{jitEv.MethodName}";
        }

        public MethodUniqueIdentifier(PARSERS.Clr.MethodLoadUnloadVerboseTraceData jitEv)
        {
            _methodToken = jitEv.MethodToken;
            _moduleId = jitEv.ModuleID;
            _methodId = jitEv.MethodID;
            _fullyQualName = $"{jitEv.MethodNamespace}.{jitEv.MethodName}";
        }

        public override int GetHashCode()
        {
            return _methodId.GetHashCode() 
                ^ _fullyQualName.GetHashCode()
                ^ _moduleId.GetHashCode()
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
                && MethodToken == castObj.MethodToken
                && FullyQualifiedName == castObj.FullyQualifiedName;
        }

        public override string ToString()
        {
            return $"{FullyQualifiedName} (MethodID {MethodId}, MethodToken {MethodToken}, ModuleId {ModuleId})";
        }
    }
}
