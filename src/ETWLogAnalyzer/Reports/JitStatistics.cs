using System;
using System.Collections.Generic;
using MusicStore.ETWLogAnalyzer.ReportWriters;

using TRACING = Microsoft.Diagnostics.Tracing;

namespace MusicStore.ETWLogAnalyzer.Reports
{
    class JitStatistics : ReportBase
    {
        class ThreadJitInfo
        {
            public int MethodCount = 0;
            public double EffectiveTime = 0;
            public double NominalTime = 0;
            public List<ETWData.JitEvent> MethodJitList { get; internal set; }
            public double Efficiency { get => (NominalTime == 0) ? 1 : EffectiveTime / NominalTime; }
        }

        private readonly Dictionary<int, ThreadJitInfo> _threadJitInfoList;
        
        public JitStatistics()
        {
            _threadJitInfoList = new Dictionary<int, ThreadJitInfo>();
        }

        public override ReportBase Analyze(ETWData data)
        {
            foreach (int threadId in data.ThreadList)
            {
                List<ETWData.JitEvent> jitEventList = data.GetJitInfoForThread(threadId);
                var jitInfo = new ThreadJitInfo();
                jitInfo.MethodCount = jitEventList.Count;
                jitInfo.MethodJitList = jitEventList;

                foreach(var jitEvent in jitEventList)
                {
                    jitInfo.NominalTime += jitEvent.Duration;
                    jitInfo.EffectiveTime += data.GetEffectiveJitTime(jitEvent);
                }

                _threadJitInfoList.Add(threadId, jitInfo);
            }
            return this;
        }

        public override void Persist(TextReportWriter writer, bool dispose)
        {
            writer.WriteTitle("Jitting statistics");
            writer.Write($"\nThe process used {_threadJitInfoList.Count} thread(s) as follows:");
            var formatString = "{0,-35}:\t{1,9}";

            foreach (var threadInfo in _threadJitInfoList)
            {
                writer.WriteHeader("Thread " + threadInfo.Key.ToString());
                writer.AddIndentationLevel();

                var jitInfoForThread = threadInfo.Value;
                writer.WriteLine(String.Format(formatString, "Methods jitted [-]", jitInfoForThread.MethodCount));
                writer.WriteLine(String.Format(formatString, "Nominal thread jit time [ms]", jitInfoForThread.NominalTime));
                writer.WriteLine(String.Format(formatString, "Effective thread jit time [ms]", jitInfoForThread.EffectiveTime));
                writer.WriteLine(String.Format(formatString, "Jit Efficiency [%]", jitInfoForThread.Efficiency * 100));

                writer.RemoveIndentationLevel();
            }

            if (dispose)
            {
                writer.Dispose();
            }
        }
    }
}
