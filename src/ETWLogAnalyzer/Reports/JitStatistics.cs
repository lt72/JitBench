using System;
using System.Collections.Generic;
using MusicStore.ETWLogAnalyzer.ReportWriters;

using PARSERS = Microsoft.Diagnostics.Tracing.Parsers;

namespace MusicStore.ETWLogAnalyzer.Reports
{
    internal class JitStatistics : ReportBase
    {
        private class ThreadJitInfo
        {
            public int MethodCount = 0;
            public double EffectiveTime = 0;
            public double NominalTime = 0;
            public List<ETWData.JitEvent> MethodJitList { get; internal set; }
            public double Efficiency { get => (NominalTime == 0) ? 1 : EffectiveTime / NominalTime; }
        }

        private class MethodJitInfo
        {
            internal class JitDurationComparer : IComparer<MethodJitInfo>
            {
                public int Compare(MethodJitInfo x, MethodJitInfo y)
                {
                    if (x.EffectiveTime * y.NominalTime < y.EffectiveTime * x.NominalTime)
                        return -1;
                    if (x.EffectiveTime * y.NominalTime < y.EffectiveTime * x.NominalTime)
                        return 0;
                    return 0;
                    //if (x._endTime < y._endTime)
                    //{
                    //    return -1;
                    //}
                    //else if (x._endTime < y._endTime)
                    //{
                    //    return 1;
                    //}

                    //return 0;
                }
            }

            public string FullyQualifiedMethodName { get; private set; }
            public long MethodId { get; private set; }
            public int ThreadID { get; private set; }
            public double NominalTime { get; private set; }
            public double EffectiveTime { get; private set; }
            private double _endTime;

            public MethodJitInfo(ETWData.JitEvent jitEvent)
            {
                FullyQualifiedMethodName = jitEvent.FullyQualifiedMethodName;
                MethodId = jitEvent.MethodId;
                NominalTime = 0;
                EffectiveTime = 0;
                ThreadID = jitEvent.ThreadId;
                _endTime = jitEvent.BeginTime + jitEvent.Duration;
            }

            public void UpdateMethodTimes(double effectiveTime, double nominalTime)
            {
                NominalTime += nominalTime;
                EffectiveTime += effectiveTime;
            }
        }

        private readonly Dictionary<int, ThreadJitInfo> _threadJitInfoList;
        private readonly Dictionary<long, MethodJitInfo> _methodJitTime;
        
        public JitStatistics()
        {
            _threadJitInfoList = new Dictionary<int, ThreadJitInfo>();
            _methodJitTime = new Dictionary<long, MethodJitInfo>();
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

                    double effectiveJitTime = data.GetEffectiveJitTime(jitEvent);
                    jitInfo.EffectiveTime += effectiveJitTime;
                    GetMethodJitInfo(jitEvent).UpdateMethodTimes(effectiveJitTime, jitEvent.Duration);
                }

                _threadJitInfoList.Add(threadId, jitInfo);
            }
            return this;
        }

        private MethodJitInfo GetMethodJitInfo(ETWData.JitEvent jitEvent)
        {
            if (!_methodJitTime.TryGetValue(jitEvent.MethodId, out MethodJitInfo jitTimeInfo))
            {
                jitTimeInfo = new MethodJitInfo(jitEvent);
                _methodJitTime.Add(jitEvent.MethodId, jitTimeInfo);
            }

            return jitTimeInfo;
        }

        public override void Persist(TextReportWriter writer, bool dispose)
        {
            writer.WriteTitle("Jitting statistics per thread");
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

            writer.SkipLine();
            writer.SkipLine();

            writer.WriteTitle("Jitting statistics per method");
            writer.Write($"\nThe process jitted {_methodJitTime.Count} method(s) as in the following order:");

            var methodJitList = new List<MethodJitInfo>(_methodJitTime.Values);
            methodJitList.Sort(new MethodJitInfo.JitDurationComparer());

            foreach (var methodJitInfo in methodJitList)
            {
                writer.WriteHeader($"{methodJitInfo.FullyQualifiedMethodName} (Method ID: {methodJitInfo.MethodId}, thread: {methodJitInfo.ThreadID})");
                writer.AddIndentationLevel();

                writer.WriteLine(String.Format(formatString, "Effective jit time [ms]", methodJitInfo.EffectiveTime));
                writer.WriteLine(String.Format(formatString, "Nominal jit time [ms]", methodJitInfo.NominalTime));
                
                writer.RemoveIndentationLevel();
            }

            if (dispose)
            {
                writer.Dispose();
            }
        }
    }
}
