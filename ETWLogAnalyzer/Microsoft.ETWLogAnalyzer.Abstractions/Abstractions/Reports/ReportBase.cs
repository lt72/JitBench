namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public abstract class ReportBase 
    {
        public string Name { get; protected set; }

        protected ReportBase()
        {
        }

        public abstract ReportBase Analyze(EventModelBase data);

        public abstract void Persist(ReportWriters.TextReportWriter writer, bool dispose);

        protected static string Normalize(string name)
        {
            return name.Replace('\\', '/');
        }
    }
}
