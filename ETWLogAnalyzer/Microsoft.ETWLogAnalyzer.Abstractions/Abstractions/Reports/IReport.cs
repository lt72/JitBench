namespace Microsoft.ETWLogAnalyzer.Abstractions
{
    /// <summary>
    /// Interface that must be implemented by all reports so the controller can correctly instantiate and run them.
    /// </summary>
    public interface IReport 
    {
        /// <summary>
        /// Name of the report. Will be used by the controller for status logging.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// This method should perform all the visiting into the model using visitors that extend EventVisitorBase
        /// and should cache all the results as needed and report success or failure. 
        /// </summary>
        /// <param name="data"> Model containing the logged events for the process under trace. </param>
        /// <returns> True if analysis was successful, false otherwise. </returns>
        bool Analyze(IEventModel data);
        /// <summary>
        /// This method should take the cached data from analysis and persist it however the user might deem it useful.
        /// </summary>
        /// <param name="folderPath"> Path to save the report to. </param>
        /// <returns> True if persistence was successful, false otherwise. </returns>
        bool Persist(string folderPath);
    }
}
