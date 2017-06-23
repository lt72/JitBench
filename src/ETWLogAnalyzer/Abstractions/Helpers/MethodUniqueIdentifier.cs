using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace MusicStore.ETWLogAnalyzer.Abstractions
{
    public class MethodUniqueIdentifier
    {
        public long MethodId { get; private set; }
        public string FullyQualifiedName { get; private set; }

        public MethodUniqueIdentifier(long methodId, string fullyQualifiedName)
        {
            MethodId = methodId;
            FullyQualifiedName = fullyQualifiedName;
        }

        public MethodUniqueIdentifier(PARSERS.Clr.MethodJittingStartedTraceData jitEv)
        {
            MethodId = jitEv.MethodID;
            FullyQualifiedName = $"{jitEv.MethodNamespace}::{jitEv.MethodName}";
        }

        public MethodUniqueIdentifier(PARSERS.Clr.MethodLoadUnloadVerboseTraceData jitEv)
        {
            MethodId = jitEv.MethodID;
            FullyQualifiedName = $"{jitEv.MethodNamespace}::{jitEv.MethodName}";
        }

        public override int GetHashCode()
        {
            return MethodId.GetHashCode() ^ FullyQualifiedName.GetHashCode();
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

            return GetHashCode() == obj.GetHashCode();
        }

        public override string ToString()
        {
            return $"{FullyQualifiedName} (MethodID {MethodId})";
        }
    }
}
