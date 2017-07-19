namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    public interface IReport 
    {
        string Name { get; }
        bool IsInErrorState { get; }
        
        IReport Analyze(IEventModel data);

        bool Persist(string folderPath);
    }
}
