namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public interface IReport 
    {
        string Name { get; }
        
        IReport Analyze(IEventModel data);

       void Persist(string folderPath);
    }
}
