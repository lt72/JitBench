namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public interface IReport 
    {
        string Name { get; }
        
        IReport Analyze(EventModelBase data);

       void Persist(ReportWriters.TextReportWriter writer, bool dispose);
    }
}
