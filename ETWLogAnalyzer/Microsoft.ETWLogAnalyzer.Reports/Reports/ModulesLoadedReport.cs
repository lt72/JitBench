using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.ETWLogAnalyzer.Abstractions;
using Microsoft.ETWLogAnalyzer.Framework;
using Microsoft.ETWLogAnalyzer.Reports.ReportVisitors;
using Microsoft.ETWLogAnalyzer.ReportWriters;


namespace Microsoft.ETWLogAnalyzer.Reports.Reports
{
    /// <summary>
    /// Report containing the loaded modules during the runtime of an app. 
    /// </summary>
    public class ModulesLoadedReport : IReport
    {
        /// <summary>
        /// Helper class to compare modules.
        /// </summary>
        private class ModuleComparer : IEqualityComparer<ModuleLoadUnloadTraceData>
        {
            public bool Equals(ModuleLoadUnloadTraceData x, ModuleLoadUnloadTraceData y)
            {
                if (x == null)
                {
                    return y == null;
                }

                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                return x.ModuleILPath == y.ModuleILPath;
            }

            public int GetHashCode(ModuleLoadUnloadTraceData obj)
            {
                return obj.GetHashCode();
            }
        }

        public string Name => "loaded_modules_report.txt";
        private Dictionary<MethodUniqueIdentifier, List<ModuleLoadUnloadTraceData>> _methodToModuleMap;
        private List<ModuleLoadUnloadTraceData> _modulesOutsideJitting;
        private int _moduleCount = 0;

        public ModulesLoadedReport()
        {
            _methodToModuleMap = new Dictionary<MethodUniqueIdentifier, List<ModuleLoadUnloadTraceData>>();
            _modulesOutsideJitting = new List<ModuleLoadUnloadTraceData>();
            _moduleCount = 0;
        }

        public bool Analyze(IEventModel data)
        {
            foreach(var threadId in data.ThreadList)
            {
                var modulesLoadedOnJitVisitor = new CollectEventsInWindowVisitor<MethodJittingStartedTraceData, MethodLoadUnloadVerboseTraceData, ModuleLoadUnloadTraceData, MethodUniqueIdentifier>();
                var overallModulesLoadedVisitor = new CollectEventsVisitor<ModuleLoadUnloadTraceData>();

                Controller.RunVisitorForResult(modulesLoadedOnJitVisitor, data.GetThreadTimeline(threadId));
                Controller.RunVisitorForResult(overallModulesLoadedVisitor, data.GetThreadTimeline(threadId));

                if (modulesLoadedOnJitVisitor.State == VisitorState.Error 
                    || overallModulesLoadedVisitor.State == VisitorState.Error)
                {
                    System.Diagnostics.Debug.Assert(false, "Module loading visitor reported error");
                    return false;
                }

                IEnumerable<ModuleLoadUnloadTraceData> modulesLoadedInThreadOutsideJit = overallModulesLoadedVisitor.Result.AsEnumerable();
                _moduleCount += overallModulesLoadedVisitor.Result.Count;

                foreach(var keyValPair in modulesLoadedOnJitVisitor.Result)
                {
                    _methodToModuleMap.Add(keyValPair.Key, keyValPair.Value);
                    // Discard modules loaded in jitting.
                    modulesLoadedInThreadOutsideJit = modulesLoadedInThreadOutsideJit.Except(keyValPair.Value, new ModuleComparer()).ToList();
                }

                _modulesOutsideJitting.AddRange(modulesLoadedInThreadOutsideJit);
            }

            return true;
        }

        public bool Persist(string folderPath)
        {
            using (var writer = new PlainTextWriter(System.IO.Path.Combine(folderPath, Name)))
            {
                writer.WriteTitle("Module Loading Report");
                writer.WriteLine($"Modules loaded:                 {_moduleCount}");
                writer.WriteLine($"Modules loaded while jitting:   {_moduleCount - _modulesOutsideJitting.Count}");
                writer.WriteLine($"Other modules loaded:           {_modulesOutsideJitting.Count}");
                writer.SkipLine();

                writer.WriteTitle("Modules loaded on Method Jitting");

                foreach (var methodModuleListPair in _methodToModuleMap)
                {
                    writer.WriteHeader(methodModuleListPair.Key.ToString());
                    writer.WriteLine($"Loaded {methodModuleListPair.Value.Count} modules.");

                    writer.AddIndentationLevel();
                    for (int index = 0; index < methodModuleListPair.Value.Count; index++)
                    {
                        var module = methodModuleListPair.Value[index];
                        writer.WriteLine($"{index + 1}) {module.ModuleILPath}");
                    }
                    writer.RemoveIndentationLevel();
                }

                writer.SkipLine();
                writer.WriteTitle("Modules loaded outside jitting");

                for (int index = 0; index < _modulesOutsideJitting.Count; index++)
                {
                    var module = _modulesOutsideJitting[index];
                    writer.WriteLine($"{index + 1}) {module.ModuleILPath}");
                }
            }

            return true;
        }
    }
}
