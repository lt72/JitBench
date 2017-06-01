using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using TRACING = Microsoft.Diagnostics.Tracing;
using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;
using MusicStore.ETWLogAnalyzer.ReportWriters;

namespace MusicStore.ETWLogAnalyzer
{
    internal class Modules : ReportBase
    {
        private List<string> _assembliesLoaded;
        private List<string> _modulesLoaded;


        public Modules()
        {
            _assembliesLoaded = new List<string>();
            _modulesLoaded = new List<string>();
        }

        public override ReportBase Analyze(ETWData data)
        {
            foreach (var k in data.ThreadEvents.Keys)
            {
                var th = data.ThreadEvents[k];
                    
                foreach (var ev in th)
                {

                    if (ev is PARSERS.Clr.AssemblyLoadUnloadTraceData)
                    {
                        var tag = Normalize(((PARSERS.Clr.AssemblyLoadUnloadTraceData)ev).FullyQualifiedAssemblyName);

                        _assembliesLoaded.Add(tag);
                    }
                    else if (ev is PARSERS.Clr.ModuleLoadUnloadTraceData)
                    {
                        var tag = Normalize(((PARSERS.Clr.ModuleLoadUnloadTraceData)ev).ModuleILPath);

                        _modulesLoaded.Add(tag);
                    }
                }
            }
            return this;
        }

        public override void Persist(TextReportWriter writer, bool dispose)
        {
            writer.WriteHeader("Assemblies loaded");

            writer.WriteLine($"Assemblies loaded: {_assembliesLoaded.Count}");
            foreach (var assm in _assembliesLoaded)
            {
                writer.WriteLine($"Assembly: {assm}");
            }

            writer.WriteHeader("Modules loaded");

            writer.WriteLine($"Modules loaded: {_modulesLoaded.Count}");
            foreach (var mdl in _modulesLoaded)
            {
                writer.WriteLine($"Module: {mdl}");
            }
        }
    }
}
